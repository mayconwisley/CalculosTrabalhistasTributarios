using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Atualização de débitos judiciais pelas fases definidas nos tribunais superiores e na Lei 14.905/2024:
/// <list type="bullet">
/// <item>Trabalhista (ADC 58 do STF e E-ED-RR-713-03.2010.5.04.0029 do TST): IPCA-E e juros pela TR antes do
/// ajuizamento; só a Selic do ajuizamento até 29/08/2024; IPCA e taxa legal depois.</item>
/// <item>Cível (Código Civil, arts. 389 e 406, e Tema 1.368 do STJ): o índice da decisão até o início dos juros; só a
/// Selic dele até 29/08/2024; IPCA e taxa legal depois.</item>
/// </list>
/// Os índices de correção são mensais, do mês inicial até o anterior ao final; a Selic, a TR e a taxa legal são
/// proporcionais aos dias corridos de cada mês e somadas em juros simples. Os juros incidem sobre o valor já atualizado.
/// </summary>
public sealed class SimularDebitoJudicialUseCase(IIndicesEconomicos indicesEconomicos) : ISimularDebitoJudicialUseCase
{
    private const int MaximoParcelas = 600;
    private const decimal PercentualMultaEHonorarios = 10m;

    /// <summary>Último dia das regras anteriores à Lei 14.905/2024; a taxa legal e o IPCA valem desde 30/08/2024.</summary>
    private static readonly DateOnly FimRegraAnterior = new(2024, 8, 29);

    public async Task<Result<SimulacaoDebitoJudicialDto>> CalcularAsync(SimularDebitoJudicialRequest r, CancellationToken cancellationToken)
    {
        if (Validar(r) is { Falhou: true } invalido)
            return invalido.Erro;
        var trabalhista = r.Natureza == NaturezaDebito.Trabalhista;
        var series = new Series(indicesEconomicos, cancellationToken);
        var parcelas = new List<ParcelaDebitoDto>(r.Parcelas.Count);
        foreach (var parcela in r.Parcelas.OrderBy(parcela => parcela.Vencimento))
            parcelas.Add(trabalhista ? await TrabalhistaAsync(parcela, r, series) : await CivelAsync(parcela, r, series));
        if (series.Ausente is { } ausente)
            return ausente;

        var total = parcelas.Sum(parcela => parcela.Total);
        var multa = !trabalhista && r.Acrescimos != AcrescimosPenhora.Nenhum ? CalculadoraTributacao.Arredondar(total * PercentualMultaEHonorarios / 100m) : 0m;
        var honorarios = !trabalhista && r.Acrescimos == AcrescimosPenhora.MultaEHonorarios ? CalculadoraTributacao.Arredondar(total * PercentualMultaEHonorarios / 100m) : 0m;
        return new SimulacaoDebitoJudicialDto(r.Natureza, r.DataCalculo, parcelas, Criterios(r, multa, honorarios), series.Observacoes(parcelas), multa, honorarios);
    }

    private static Result Validar(SimularDebitoJudicialRequest r)
    {
        if (r.Parcelas.Count is 0 or > MaximoParcelas)
            return Erro.Validacao($"Informe de 1 a {MaximoParcelas} parcelas.");
        foreach (var parcela in r.Parcelas)
        {
            if (parcela.Valor <= 0m)
                return Erro.Validacao($"Informe o valor da parcela de {Formato.Data(parcela.Vencimento)}.");
            if (parcela.Vencimento > r.DataCalculo)
                return Erro.Validacao($"A parcela de {Formato.Data(parcela.Vencimento)} vence depois da data do cálculo.");
        }
        if (r.DataAjuizamento is { } ajuizamento && ajuizamento > r.DataCalculo)
            return Erro.Validacao("A data do ajuizamento não pode ser posterior à data do cálculo.");
        if (r.Natureza == NaturezaDebito.Civel && r.InicioJuros == InicioJurosCivel.Citacao)
        {
            if (r.DataCitacao is not { } citacao)
                return Erro.Validacao("Informe a data da citação, que é o início dos juros.");
            if (citacao > r.DataCalculo)
                return Erro.Validacao("A data da citação não pode ser posterior à data do cálculo.");
        }
        return Result.Ok();
    }

    private static async Task<ParcelaDebitoDto> TrabalhistaAsync(ParcelaDebito p, SimularDebitoJudicialRequest r, Series series)
    {
        var calculo = r.DataCalculo;
        if (r.DataAjuizamento is not { } ajuizamento)
        {
            // Ainda sem ação: todo o período é pré-judicial.
            var fatorPre = await series.FatorAsync(IndiceEconomico.IpcaE, Mes(p.Vencimento), Mes(calculo));
            var trSemAcao = await series.TaxaAsync(IndiceEconomico.Tr, p.Vencimento, calculo);
            return Montar(p, fatorPre, 0m, trSemAcao);
        }

        var vesperaAjuizamento = ajuizamento.AddDays(-1);
        var fatorIpcaE = await series.FatorAsync(IndiceEconomico.IpcaE, Mes(p.Vencimento), Mes(Menor(ajuizamento, calculo)));
        var tr = await series.TaxaAsync(IndiceEconomico.Tr, p.Vencimento, Menor(vesperaAjuizamento, calculo));
        var inicioSelic = Maior(p.Vencimento, vesperaAjuizamento);
        var selic = await series.TaxaAsync(IndiceEconomico.Selic, inicioSelic, Menor(FimRegraAnterior, calculo));
        // Com a Selic até 29/08/2024, o IPCA começa em setembro; sem ela, no mês em que a fase atual começa.
        var inicioIpca = inicioSelic < FimRegraAnterior ? Mes(FimRegraAnterior).AddMonths(1) : Mes(Maior(Maior(p.Vencimento, ajuizamento), FimRegraAnterior.AddDays(1)));
        var fatorIpca = await series.FatorAsync(IndiceEconomico.Ipca, inicioIpca, Mes(calculo));
        var taxaLegal = await series.TaxaAsync(IndiceEconomico.TaxaLegal, Maior(Maior(p.Vencimento, vesperaAjuizamento), FimRegraAnterior), calculo);
        return Montar(p, fatorIpcaE * fatorIpca, selic, tr + taxaLegal);
    }

    private static async Task<ParcelaDebitoDto> CivelAsync(ParcelaDebito p, SimularDebitoJudicialRequest r, Series series)
    {
        var calculo = r.DataCalculo;
        var inicioJuros = r.InicioJuros == InicioJurosCivel.Citacao ? Maior(p.Vencimento, r.DataCitacao!.Value) : p.Vencimento;
        var indiceAnterior = r.IndiceAnterior == IndiceCivelAnterior.Ipca ? IndiceEconomico.Ipca : IndiceEconomico.Inpc;

        // Só correção até os juros começarem, ou até 29/08/2024; o mês do início fica com a fase seguinte.
        var fimCorrecaoAnterior = Menor(inicioJuros, FimRegraAnterior.AddDays(1));
        var fatorAnterior = await series.FatorAsync(indiceAnterior, Mes(p.Vencimento), Mes(Menor(fimCorrecaoAnterior, calculo)));
        var selic = await series.TaxaAsync(IndiceEconomico.Selic, inicioJuros, Menor(FimRegraAnterior, calculo));
        var inicioIpca = inicioJuros < FimRegraAnterior ? Mes(FimRegraAnterior).AddMonths(1) : Mes(Maior(p.Vencimento, FimRegraAnterior.AddDays(1)));
        var fatorIpca = await series.FatorAsync(IndiceEconomico.Ipca, inicioIpca, Mes(calculo));
        var taxaLegal = await series.TaxaAsync(IndiceEconomico.TaxaLegal, Maior(inicioJuros, FimRegraAnterior), calculo);
        return Montar(p, fatorAnterior * fatorIpca, selic, taxaLegal);
    }

    /// <summary>A deflação entra no fator, mas o valor atualizado nunca fica abaixo do nominal (Tema 678 do STJ).</summary>
    private static ParcelaDebitoDto Montar(ParcelaDebito p, decimal fatorCorrecao, decimal percentualSelic, decimal percentualJuros)
    {
        var atualizado = CalculadoraTributacao.Arredondar(p.Valor * Math.Max(1m, fatorCorrecao * (1m + percentualSelic / 100m)));
        var juros = CalculadoraTributacao.Arredondar(atualizado * percentualJuros / 100m);
        return new ParcelaDebitoDto(p.Descricao, p.Vencimento, p.Valor, Math.Round(fatorCorrecao, 6), percentualSelic, atualizado, percentualJuros, juros, atualizado + juros);
    }

    private static DateOnly Mes(DateOnly data) => new(data.Year, data.Month, 1);
    private static DateOnly Maior(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Menor(DateOnly a, DateOnly b) => a < b ? a : b;

    private static IReadOnlyList<string> Criterios(SimularDebitoJudicialRequest r, decimal multa, decimal honorarios)
    {
        var criterios = new List<string>();
        if (r.Natureza == NaturezaDebito.Trabalhista)
        {
            criterios.Add(r.DataAjuizamento is { } ajuizamento
                ? $"Fase pré-judicial, do vencimento até o ajuizamento em {Formato.Data(ajuizamento)}: correção pelo IPCA-E (a variação mensal do IPCA-15), do mês do vencimento até o anterior ao ajuizamento, e juros pela TR acumulada, proporcional aos dias (Lei 8.177/1991, art. 39, e ADC 58 do STF)."
                : "Sem ajuizamento informado, todo o período é pré-judicial: correção pelo IPCA-E (a variação mensal do IPCA-15), do mês do vencimento até o anterior ao cálculo, e juros pela TR acumulada, proporcional aos dias (Lei 8.177/1991, art. 39, e ADC 58 do STF).");
            if (r.DataAjuizamento is not null)
            {
                criterios.Add("Fase judicial até 29/08/2024: só a Selic, que reúne a correção e os juros, somada mês a mês e proporcional aos dias (ADC 58 do STF).");
                criterios.Add("Desde 30/08/2024: correção pelo IPCA e juros pela taxa legal, a Selic menos o IPCA-15, nunca negativa (Lei 14.905/2024; TST, E-ED-RR-713-03.2010.5.04.0029).");
            }
        }
        else
        {
            var indice = r.IndiceAnterior == IndiceCivelAnterior.Ipca ? "IPCA" : "INPC";
            criterios.Add(r.InicioJuros == InicioJurosCivel.Citacao
                ? $"Juros desde a citação, em {Formato.Data(r.DataCitacao!.Value)} (Código Civil, art. 405); para as parcelas que vencem depois dela, desde o vencimento."
                : "Juros desde o vencimento de cada parcela (Código Civil, art. 397).");
            criterios.Add($"Antes dos juros e até 29/08/2024: correção pelo {indice}, do mês do vencimento até o anterior ao início dos juros.");
            criterios.Add("Com juros, até 29/08/2024: só a Selic, que reúne a correção e os juros, somada mês a mês e proporcional aos dias (STJ, Tema 1.368).");
            criterios.Add("Desde 30/08/2024: correção pelo IPCA e juros pela taxa legal, a Selic menos o IPCA-15, nunca negativa (Código Civil, arts. 389 e 406, com a redação da Lei 14.905/2024).");
            if (multa > 0m)
                criterios.Add(honorarios > 0m
                    ? $"Multa de 10% ({Formato.Moeda(multa)}) e honorários de 10% ({Formato.Moeda(honorarios)}) sobre o débito atualizado não pago em 15 dias úteis da intimação (CPC, art. 523, § 1º); os honorários são calculados sem a multa."
                    : $"Multa de 10% ({Formato.Moeda(multa)}) sobre o débito atualizado não pago em 15 dias úteis da intimação (CPC, art. 523, § 1º).");
        }
        criterios.Add("Os juros são simples e incidem sobre o valor atualizado. Meses de deflação entram no cálculo, mas o valor atualizado nunca fica abaixo do original.");
        return criterios;
    }

    /// <summary>Séries carregadas uma vez por cálculo, com o registro dos meses que ainda não estão nas tabelas.</summary>
    private sealed class Series(IIndicesEconomicos indices, CancellationToken cancellationToken)
    {
        private readonly Dictionary<IndiceEconomico, IReadOnlyDictionary<DateOnly, decimal>> _series = [];
        private readonly Dictionary<IndiceEconomico, SortedSet<DateOnly>> _faltantes = [];

        /// <summary>O primeiro índice sem nenhum valor cadastrado até o mês pedido: sem ele, o débito não pode ser atualizado.</summary>
        public Erro? Ausente { get; private set; }

        private async Task<IReadOnlyDictionary<DateOnly, decimal>> SerieAsync(IndiceEconomico indice)
        {
            if (!_series.TryGetValue(indice, out var serie))
                _series[indice] = serie = await indices.ObterAsync(indice, cancellationToken);
            return serie;
        }

        /// <summary>Produto das variações mensais de <paramref name="inicio"/> até o mês anterior a <paramref name="fimExclusivo"/>; meses sem índice ficam de fora, com aviso.</summary>
        public async Task<decimal> FatorAsync(IndiceEconomico indice, DateOnly inicio, DateOnly fimExclusivo)
        {
            if (inicio >= fimExclusivo)
                return 1m;
            var serie = await SerieAsync(indice);
            var fator = 1m;
            for (var mes = inicio; mes < fimExclusivo; mes = mes.AddMonths(1))
            {
                if (serie.TryGetValue(mes, out var variacao))
                    fator *= 1m + variacao / 100m;
                else
                    Faltante(indice, mes);
            }
            return fator;
        }

        /// <summary>
        /// Taxa simples dos dias seguintes a <paramref name="inicio"/> até <paramref name="fim"/>: em cada mês, a taxa dividida
        /// pelos dias do mês e multiplicada pelos dias do período. O mês sem taxa repete a do último mês cadastrado, com aviso.
        /// </summary>
        public async Task<decimal> TaxaAsync(IndiceEconomico indice, DateOnly inicio, DateOnly fim)
        {
            if (fim <= inicio)
                return 0m;
            var serie = await SerieAsync(indice);
            var total = 0m;
            for (var dia = inicio.AddDays(1); dia <= fim;)
            {
                var mes = new DateOnly(dia.Year, dia.Month, 1);
                var ultimoDia = mes.AddMonths(1).AddDays(-1);
                var ate = fim < ultimoDia ? fim : ultimoDia;
                total += Math.Round(TaxaDoMes(indice, serie, mes) * (ate.DayNumber - dia.DayNumber + 1) / DateTime.DaysInMonth(mes.Year, mes.Month), 6);
                dia = ate.AddDays(1);
            }
            return total;
        }

        private decimal TaxaDoMes(IndiceEconomico indice, IReadOnlyDictionary<DateOnly, decimal> serie, DateOnly mes)
        {
            if (serie.TryGetValue(mes, out var taxa))
                return taxa;
            var anterior = serie.Keys.Where(cadastrado => cadastrado < mes).DefaultIfEmpty().Max();
            if (anterior == default)
            {
                Ausente ??= Erro.NaoEncontrado($"Não há {Nome(indice)} cadastrada até {Formato.Competencia(mes)}. Atualize a tabela pela internet.");
                return 0m;
            }
            Faltante(indice, mes);
            return serie[anterior];
        }

        private void Faltante(IndiceEconomico indice, DateOnly mes)
        {
            if (!_faltantes.TryGetValue(indice, out var meses))
                _faltantes[indice] = meses = [];
            meses.Add(mes);
        }

        public IReadOnlyList<string> Observacoes(IReadOnlyList<ParcelaDebitoDto> parcelas)
        {
            var observacoes = new List<string>();
            foreach (var (indice, meses) in _faltantes)
            {
                var lista = string.Join(", ", meses.Select(Formato.Competencia));
                observacoes.Add(indice is IndiceEconomico.Selic or IndiceEconomico.Tr or IndiceEconomico.TaxaLegal
                    ? $"A {Nome(indice)} de {lista} ainda não está na tabela: foi repetida a do último mês cadastrado. Atualize a tabela pela internet."
                    : $"O {Nome(indice)} de {lista} ainda não está na tabela e ficou de fora da correção. Atualize a tabela pela internet para incluí-lo.");
            }
            if (parcelas.Any(parcela => parcela.FatorCorrecao * (1m + parcela.PercentualSelic / 100m) < 1m))
                observacoes.Add("Em parcelas cujo período teve mais deflação que inflação, o valor atualizado ficou igual ao original.");
            return observacoes;
        }

        private static string Nome(IndiceEconomico indice) => indice switch
        {
            IndiceEconomico.Inpc => "INPC",
            IndiceEconomico.Ipca => "IPCA",
            IndiceEconomico.IpcaE => "IPCA-E",
            IndiceEconomico.Selic => "Selic",
            IndiceEconomico.Tr => "TR",
            _ => "taxa legal"
        };
    }
}
