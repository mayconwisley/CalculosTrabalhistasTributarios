using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Débito de pensão alimentícia: o saldo de cada parcela (devido menos pago) é corrigido mês a mês pelo índice escolhido,
/// do mês do vencimento até o mês anterior ao cálculo, e recebe juros de mora sobre o valor corrigido. As 3 parcelas
/// anteriores ao ajuizamento e as que vencem no curso do processo são cobradas pelo rito da prisão (CPC, art. 528, § 7º,
/// e Súmula 309 do STJ); as demais, pelo rito da penhora, que pode ter a multa e os honorários de 10% do art. 523 do CPC.
/// </summary>
public sealed class SimularPensaoAtrasoUseCase(ITributacaoConsulta tributacaoConsulta, IIndicesEconomicos indicesEconomicos) : ISimularPensaoAtrasoUseCase
{
    private const int MaximoParcelas = 240;
    private const int ParcelasDoRitoDaPrisao = 3;
    private const decimal PercentualMultaEHonorarios = 10m;

    /// <summary>
    /// Último dia dos juros de 1% ao mês: a taxa legal vale desde 30/08/2024, com a taxa de agosto para os dias 30 e 31
    /// (Lei 14.905/2024 e Resolução CMN 5.171/2024, art. 8º).
    /// </summary>
    private static readonly DateOnly FimJurosDeUmPorCento = new(2024, 8, 29);

    public async Task<Result<IReadOnlyList<ParcelaPensaoInformada>>> GerarParcelasAsync(GerarParcelasAtrasoRequest r, CancellationToken cancellationToken)
    {
        if (r.UltimaParcela < r.PrimeiraParcela)
            return Erro.Validacao("A última parcela deve ser igual ou posterior à primeira.");
        if (r.DiaVencimento is < 1 or > 31)
            return Erro.Validacao("O dia do vencimento deve estar entre 1 e 31.");
        var meses = (r.UltimaParcela.Year - r.PrimeiraParcela.Year) * 12 + r.UltimaParcela.Month - r.PrimeiraParcela.Month + 1;
        if (meses > MaximoParcelas)
            return Erro.Validacao($"Informe no máximo {MaximoParcelas} parcelas ({MaximoParcelas / 12} anos).");
        if (r.Base == BasePensao.ValorFixo && r.Valor <= 0m)
            return Erro.Validacao("Informe o valor mensal da pensão.");
        if (r.Base == BasePensao.SalarioMinimo && r.Percentual is <= 0m or > 1000m)
            return Erro.Validacao("Informe o percentual do salário mínimo, por exemplo 30.");
        if (r.Base is not (BasePensao.ValorFixo or BasePensao.SalarioMinimo))
            return Erro.Validacao("As parcelas em atraso são geradas por valor fixo ou por percentual do salário mínimo.");

        var parcelas = new List<ParcelaPensaoInformada>(meses);
        for (var mes = new DateOnly(r.PrimeiraParcela.Year, r.PrimeiraParcela.Month, 1); mes <= r.UltimaParcela; mes = mes.AddMonths(1))
        {
            var vencimento = new DateOnly(mes.Year, mes.Month, Math.Min(r.DiaVencimento, DateTime.DaysInMonth(mes.Year, mes.Month)));
            var devido = r.Valor;
            if (r.Base == BasePensao.SalarioMinimo)
            {
                var salarioMinimo = await SalarioMinimoAsync(mes, cancellationToken);
                if (salarioMinimo.Falhou)
                    return salarioMinimo.Erro;
                devido = CalculadoraTributacao.Arredondar(salarioMinimo.Valor * r.Percentual / 100m);
            }
            parcelas.Add(new ParcelaPensaoInformada(mes, vencimento, devido, 0m));
        }
        return Result.Ok<IReadOnlyList<ParcelaPensaoInformada>>(parcelas);
    }

    public async Task<Result<SimulacaoPensaoAtrasoDto>> CalcularAsync(SimularPensaoAtrasoRequest r, CancellationToken cancellationToken)
    {
        if (Validar(r) is { Falhou: true } invalido)
            return invalido.Erro;
        var mesAnteriorAoCalculo = new DateOnly(r.DataCalculo.Year, r.DataCalculo.Month, 1).AddMonths(-1);
        var observacoes = new List<string>();

        IReadOnlyDictionary<DateOnly, decimal>? indiceCorrecao = r.Correcao switch
        {
            CorrecaoMonetaria.Inpc => await indicesEconomicos.ObterAsync(IndiceEconomico.Inpc, cancellationToken),
            CorrecaoMonetaria.Ipca => await indicesEconomicos.ObterAsync(IndiceEconomico.Ipca, cancellationToken),
            _ => null
        };
        var nomeCorrecao = SimulacaoPensaoAtrasoDto.NomeCorrecao(r.Correcao);
        DateOnly? correcaoAte = null;
        if (indiceCorrecao is not null)
        {
            var ultimoMes = UltimoMesDisponivel(mesAnteriorAoCalculo, indiceCorrecao.Keys, nomeCorrecao, observacoes);
            if (ultimoMes.Falhou)
                return ultimoMes.Erro;
            correcaoAte = ultimoMes.Valor;
        }

        var usaTaxaLegal = r.Juros is JurosDeMora.TaxaLegal or JurosDeMora.UmPorCentoAteTaxaLegal;
        var taxaLegal = usaTaxaLegal ? await indicesEconomicos.ObterAsync(IndiceEconomico.TaxaLegal, cancellationToken) : null;
        var mesesSemTaxaLegal = new SortedSet<DateOnly>();

        // Rito da prisão: as 3 parcelas mais recentes vencidas antes do ajuizamento e todas as que vencem depois dele.
        var anteriores = r.Parcelas.Where(parcela => r.DataAjuizamento is not { } ajuizamento || parcela.Vencimento < ajuizamento)
            .OrderByDescending(parcela => parcela.Vencimento).ToArray();
        var ritoPrisao = anteriores.Take(ParcelasDoRitoDaPrisao).Concat(r.Parcelas.Except(anteriores)).ToHashSet();

        var deflacao = false;
        var parcelas = new List<ParcelaAtrasoDto>(r.Parcelas.Count);
        foreach (var parcela in r.Parcelas.OrderBy(parcela => parcela.Vencimento))
        {
            var saldo = parcela.Devido - parcela.Pago;
            var mesVencimento = new DateOnly(parcela.Vencimento.Year, parcela.Vencimento.Month, 1);
            var fatorAcumulado = indiceCorrecao is null ? 1m : FatorAcumulado(indiceCorrecao, mesVencimento, correcaoAte!.Value, nomeCorrecao);
            if (fatorAcumulado.Falhou)
                return fatorAcumulado.Erro;
            var fator = fatorAcumulado.Valor;
            // Os índices negativos entram no fator, mas a correção não reduz o valor nominal da parcela.
            if (fator < 1m)
            {
                deflacao = true;
                fator = 1m;
            }
            var corrigido = CalculadoraTributacao.Arredondar(saldo * fator);
            var taxaLegalNoPeriodo = usaTaxaLegal ? TaxaLegal(taxaLegal!, Maior(parcela.Vencimento, FimJurosDeUmPorCento), r.DataCalculo, mesesSemTaxaLegal) : 0m;
            if (taxaLegalNoPeriodo.Falhou)
                return taxaLegalNoPeriodo.Erro;
            var percentualJuros = r.Juros switch
            {
                JurosDeMora.UmPorCento => UmPorCentoAoMes(parcela.Vencimento, r.DataCalculo),
                JurosDeMora.TaxaLegal => taxaLegalNoPeriodo.Valor,
                JurosDeMora.UmPorCentoAteTaxaLegal => UmPorCentoAoMes(parcela.Vencimento, Menor(r.DataCalculo, FimJurosDeUmPorCento)) + taxaLegalNoPeriodo.Valor,
                _ => 0m
            };
            var juros = CalculadoraTributacao.Arredondar(corrigido * percentualJuros / 100m);
            parcelas.Add(new ParcelaAtrasoDto(parcela.Competencia, parcela.Vencimento, parcela.Devido, parcela.Pago, saldo, Math.Round(fator, 6), corrigido, percentualJuros, juros, corrigido + juros, ritoPrisao.Contains(parcela)));
        }

        if (deflacao)
            observacoes.Add("Em parcelas cujo período teve mais deflação que inflação, o fator ficou em 1: a correção não reduz o valor devido.");
        if (mesesSemTaxaLegal.Count > 0)
            observacoes.Add($"A taxa legal de {string.Join(", ", mesesSemTaxaLegal.Select(Formato.Competencia))} ainda não está na tabela: foi repetida a do último mês cadastrado. Atualize a tabela da taxa legal pela internet para usar a taxa publicada.");
        if (r.Juros == JurosDeMora.TaxaLegal && r.Parcelas.Any(parcela => parcela.Vencimento < FimJurosDeUmPorCento))
            observacoes.Add("A taxa legal só existe desde 30/08/2024: nas parcelas vencidas antes, os juros começam nessa data. Para cobrar 1% ao mês até 29/08/2024, escolha a opção que combina os dois.");
        if (parcelas.Any(parcela => parcela.Pago > 0m))
            observacoes.Add("Os pagamentos parciais foram abatidos do valor da parcela no vencimento.");

        // Multa e honorários só no rito da penhora; os honorários incidem sobre o débito, sem a multa (STJ, REsp 1.757.033/DF).
        var totalPenhora = parcelas.Where(parcela => !parcela.RitoPrisao).Sum(parcela => parcela.Total);
        var multa = r.Acrescimos == AcrescimosPenhora.Nenhum ? 0m : CalculadoraTributacao.Arredondar(totalPenhora * PercentualMultaEHonorarios / 100m);
        var honorarios = r.Acrescimos == AcrescimosPenhora.MultaEHonorarios ? CalculadoraTributacao.Arredondar(totalPenhora * PercentualMultaEHonorarios / 100m) : 0m;
        if (r.Acrescimos == AcrescimosPenhora.Nenhum)
            observacoes.Add("O cálculo não inclui multa nem honorários. No rito da penhora, se o débito não for pago em 15 dias da intimação, incidem multa de 10% e honorários de 10% (CPC, art. 523, § 1º).");
        else if (totalPenhora == 0m)
            observacoes.Add("Não há débito no rito da penhora: a multa e os honorários do art. 523 do CPC não se aplicam ao rito da prisão e ficaram em zero.");

        return new SimulacaoPensaoAtrasoDto(parcelas, r.DataCalculo, r.DataAjuizamento, r.Correcao, r.Juros, correcaoAte,
            Criterios(r, correcaoAte, nomeCorrecao, totalPenhora, multa, honorarios), observacoes, multa, honorarios);
    }

    private static Result Validar(SimularPensaoAtrasoRequest r)
    {
        if (r.Parcelas.Count is 0 or > MaximoParcelas)
            return Erro.Validacao($"Informe de 1 a {MaximoParcelas} parcelas.");
        foreach (var parcela in r.Parcelas)
        {
            if (parcela.Devido < 0m || parcela.Pago < 0m)
                return Erro.Validacao($"Os valores da parcela de {Formato.Competencia(parcela.Competencia)} não podem ser negativos.");
            if (parcela.Pago > parcela.Devido)
                return Erro.Validacao($"O valor pago na parcela de {Formato.Competencia(parcela.Competencia)} é maior que o valor devido.");
            if (parcela.Vencimento > r.DataCalculo)
                return Erro.Validacao($"A parcela de {Formato.Competencia(parcela.Competencia)} vence em {Formato.Data(parcela.Vencimento)}, depois da data do cálculo.");
        }
        if (r.DataAjuizamento is { } ajuizamento && ajuizamento > r.DataCalculo)
            return Erro.Validacao("A data do ajuizamento não pode ser posterior à data do cálculo.");
        return Result.Ok();
    }

    private async Task<Result<decimal>> SalarioMinimoAsync(DateOnly mes, CancellationToken cancellationToken) =>
        await tributacaoConsulta.ObterSalarioMinimoAsync(mes, cancellationToken) is { } salarioMinimo
            ? salarioMinimo
            : Erro.NaoEncontrado($"Não há salário mínimo cadastrado para {Formato.Competencia(mes)}.");

    /// <summary>
    /// Último mês do índice que pode ser usado: o anterior ao do cálculo ou, se ele ainda não foi publicado ou cadastrado,
    /// o mais recente da tabela, com um aviso.
    /// </summary>
    private static Result<DateOnly> UltimoMesDisponivel(DateOnly mesAnteriorAoCalculo, IEnumerable<DateOnly> meses, string nome, List<string> observacoes)
    {
        var disponiveis = meses.Where(mes => mes <= mesAnteriorAoCalculo).ToArray();
        if (disponiveis.Length == 0)
            return Erro.NaoEncontrado($"Não há valores de {nome} cadastrados até {Formato.Competencia(mesAnteriorAoCalculo)}. Atualize a tabela pela internet.");
        var ultimo = disponiveis.Max();
        if (ultimo < mesAnteriorAoCalculo)
            observacoes.Add($"A tabela de {nome} vai até {Formato.Competencia(ultimo)}: os meses seguintes, até {Formato.Competencia(mesAnteriorAoCalculo)}, ainda não foram publicados ou cadastrados e ficaram de fora. Atualize a tabela pela internet para incluí-los.");
        return ultimo;
    }

    private static Result<decimal> FatorAcumulado(IReadOnlyDictionary<DateOnly, decimal> indice, DateOnly inicio, DateOnly fim, string nome)
    {
        var fator = 1m;
        for (var mes = inicio; mes <= fim; mes = mes.AddMonths(1))
        {
            if (!indice.TryGetValue(mes, out var variacao))
                return Erro.NaoEncontrado($"Falta o {nome} de {Formato.Competencia(mes)} na tabela de índices. Cadastre o valor ou atualize a tabela pela internet.");
            fator *= 1m + variacao / 100m;
        }
        return fator;
    }

    /// <summary>1% ao mês, proporcional aos dias corridos depois do vencimento, em juros simples; zero se o fim não é posterior ao vencimento.</summary>
    private static decimal UmPorCentoAoMes(DateOnly vencimento, DateOnly fim) =>
        fim > vencimento ? Math.Round((fim.DayNumber - vencimento.DayNumber) / 30m, 6) : 0m;

    /// <summary>
    /// Taxa legal em juros simples, dos dias seguintes a <paramref name="inicio"/> até <paramref name="fim"/>: em cada mês, a
    /// taxa publicada dividida pelos dias corridos do mês e multiplicada pelos dias do período (Resolução CMN 5.171/2024,
    /// art. 6º). O mês ainda sem taxa na tabela repete a do último mês cadastrado e fica em <paramref name="mesesSemTaxa"/>.
    /// </summary>
    private static Result<decimal> TaxaLegal(IReadOnlyDictionary<DateOnly, decimal> taxas, DateOnly inicio, DateOnly fim, ISet<DateOnly> mesesSemTaxa)
    {
        var total = 0m;
        for (var dia = inicio.AddDays(1); dia <= fim;)
        {
            var mes = new DateOnly(dia.Year, dia.Month, 1);
            var ultimoDia = mes.AddMonths(1).AddDays(-1);
            var ate = fim < ultimoDia ? fim : ultimoDia;
            var taxaDoMes = TaxaDoMes(taxas, mes, mesesSemTaxa);
            if (taxaDoMes.Falhou)
                return taxaDoMes.Erro;
            total += Math.Round(taxaDoMes.Valor * (ate.DayNumber - dia.DayNumber + 1) / DateTime.DaysInMonth(mes.Year, mes.Month), 6);
            dia = ate.AddDays(1);
        }
        return total;
    }

    private static Result<decimal> TaxaDoMes(IReadOnlyDictionary<DateOnly, decimal> taxas, DateOnly mes, ISet<DateOnly> mesesSemTaxa)
    {
        if (taxas.TryGetValue(mes, out var taxa))
            return taxa;
        var anterior = taxas.Keys.Where(cadastrado => cadastrado < mes).DefaultIfEmpty().Max();
        if (anterior == default)
            return Erro.NaoEncontrado($"Não há taxa legal cadastrada para {Formato.Competencia(mes)}. Cadastre o valor ou atualize a tabela da taxa legal pela internet.");
        mesesSemTaxa.Add(mes);
        return taxas[anterior];
    }

    private static DateOnly Maior(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Menor(DateOnly a, DateOnly b) => a < b ? a : b;

    private static IReadOnlyList<string> Criterios(SimularPensaoAtrasoRequest r, DateOnly? correcaoAte, string nomeCorrecao, decimal totalPenhora, decimal multa, decimal honorarios)
    {
        var criterios = new List<string>
        {
            correcaoAte is { } ate
                ? $"Correção monetária pelo {nomeCorrecao}: o saldo de cada parcela é multiplicado pelo fator acumulado do índice, do mês do vencimento até {Formato.Competencia(ate)}."
                : "Sem correção monetária: o saldo de cada parcela fica pelo valor original.",
            r.Juros switch
            {
                JurosDeMora.UmPorCentoAteTaxaLegal => "Juros de mora simples sobre o valor corrigido: 1% ao mês, proporcional aos dias (dias ÷ 30), do vencimento até 29/08/2024; desde 30/08/2024, a taxa legal de cada mês publicada pelo Banco Central (Selic menos IPCA-15, nunca negativa), proporcional aos dias corridos do mês (Código Civil, art. 406, com a redação da Lei 14.905/2024, e Resolução CMN 5.171/2024).",
                JurosDeMora.UmPorCento => "Juros de mora simples de 1% ao mês sobre o valor corrigido, proporcionais aos dias entre o vencimento e o cálculo (dias ÷ 30).",
                JurosDeMora.TaxaLegal => "Juros de mora simples pela taxa legal sobre o valor corrigido: a taxa de cada mês publicada pelo Banco Central (Selic menos IPCA-15, nunca negativa), proporcional aos dias corridos do mês, do vencimento ao cálculo e a partir de 30/08/2024 (Código Civil, art. 406, com a redação da Lei 14.905/2024, e Resolução CMN 5.171/2024).",
                _ => "Sem juros de mora."
            },
            r.DataAjuizamento is { } ajuizamento
                ? $"Rito da prisão (CPC, art. 528, § 7º, e Súmula 309 do STJ): as 3 parcelas vencidas antes do ajuizamento, em {Formato.Data(ajuizamento)}, e as que venceram depois dele. As demais são cobradas pelo rito da penhora."
                : "Rito da prisão (CPC, art. 528, § 7º, e Súmula 309 do STJ): as 3 parcelas vencidas mais recentes, considerando o ajuizamento na data do cálculo. As demais são cobradas pelo rito da penhora."
        };
        if (r.Acrescimos == AcrescimosPenhora.MultaEHonorarios && totalPenhora > 0m)
            criterios.Add($"Multa de 10% ({Formato.Moeda(multa)}) e honorários de 10% ({Formato.Moeda(honorarios)}) sobre o débito do rito da penhora, de {Formato.Moeda(totalPenhora)}, não pago em 15 dias da intimação (CPC, art. 523, § 1º). Os honorários incidem sobre o débito, sem a multa (STJ, REsp 1.757.033/DF), e o rito da prisão não tem esses acréscimos.");
        else if (r.Acrescimos == AcrescimosPenhora.Multa && totalPenhora > 0m)
            criterios.Add($"Multa de 10% ({Formato.Moeda(multa)}) sobre o débito do rito da penhora, de {Formato.Moeda(totalPenhora)}, não pago em 15 dias da intimação (CPC, art. 523, § 1º), sem os honorários. O rito da prisão não tem a multa.");
        return criterios;
    }
}
