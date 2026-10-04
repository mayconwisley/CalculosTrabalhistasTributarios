using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Seguro-desemprego do trabalhador dispensado sem justa causa: o valor de cada parcela pela média dos últimos salários e a
/// tabela vigente na dispensa, e a quantidade de parcelas pelos meses trabalhados e pelas solicitações anteriores.
/// </summary>
public sealed class SimularSeguroDesempregoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularSeguroDesempregoRequest>
{
    /// <summary>A quantidade de parcelas considera os meses trabalhados nos 36 meses anteriores à dispensa.</summary>
    private const int PeriodoDeReferencia = 36;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularSeguroDesempregoRequest r, CancellationToken cancellationToken)
    {
        if (r.Salarios.Any(salario => salario < 0m) || r.MesesTrabalhados < 0)
            return Erro.Validacao("Os salários e os meses trabalhados não podem ser negativos.");
        var salarios = r.Salarios.Where(salario => salario > 0m).ToArray();
        if (salarios.Length == 0 && !r.Domestico)
            return Erro.Validacao("Informe pelo menos o salário do último mês antes da dispensa.");

        var competencia = new DateOnly(r.Dispensa.Year, r.Dispensa.Month, 1);
        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var consultaSalarioMinimo = tabelas.ObterSalarioMinimo();
        if (consultaSalarioMinimo.Falhou)
            return consultaSalarioMinimo.Erro;
        var salarioMinimo = consultaSalarioMinimo.Valor;
        if (r.Domestico)
            return Domestico(r, salarioMinimo);
        if (tabelas.FaixasSeguroDesemprego.Count == 0)
            return Erro.NaoEncontrado($"Não há tabela do seguro-desemprego cadastrada para {Formato.Competencia(competencia)}. Cadastre as faixas na tabela Seguro-desemprego.");

        var media = CalculadoraTributacao.Arredondar(salarios.Sum() / salarios.Length);
        var calculoParcela = RegrasSeguroDesemprego.ValorDaParcela(media, tabelas.FaixasSeguroDesemprego, salarioMinimo);
        if (calculoParcela.Falhou)
            return calculoParcela.Erro;
        var parcela = calculoParcela.Valor;
        var meses = Math.Min(r.MesesTrabalhados, PeriodoDeReferencia);
        var quantidade = RegrasSeguroDesemprego.Parcelas(r.Solicitacao, meses);
        var total = parcela.Valor * quantidade;
        var nomeSolicitacao = NomeSolicitacao(r.Solicitacao);

        var formulas = new List<FormulaDto>
        {
            new("Média salarial", salarios.Length == 1
                ? $"{Formato.Moeda(media)} (só o último mês tem salário informado)"
                : $"({string.Join(" + ", salarios.Select(Formato.Moeda))}) ÷ {salarios.Length} = {Formato.Moeda(media)}"),
            new($"Faixa {parcela.Faixa} da tabela", FormulaDaFaixa(media, parcela)),
        };
        if (parcela.Valor > parcela.ValorPelaTabela)
            formulas.Add(new("Piso", $"{Formato.Moeda(parcela.ValorPelaTabela)} é menor que o salário mínimo de {Formato.Moeda(salarioMinimo)}: cada parcela é de {Formato.Moeda(parcela.Valor)} (Lei 7.998/1990, art. 5º, § 2º)."));
        formulas.Add(new("Quantidade de parcelas", quantidade > 0
            ? $"{meses} meses trabalhados nos últimos {PeriodoDeReferencia}, na {nomeSolicitacao}: {quantidade} parcelas (3 de 6 a 11 meses, 4 de 12 a 23 e 5 a partir de 24; Lei 7.998/1990, art. 4º)"
            : $"Na {nomeSolicitacao}, é preciso ter trabalhado pelo menos {RegrasSeguroDesemprego.MesesMinimos(r.Solicitacao)} meses; com {meses}, não há direito ao benefício."));
        if (quantidade > 0)
            formulas.Add(new("Total do benefício", $"{quantidade} x {Formato.Moeda(parcela.Valor)} = {Formato.Moeda(total)}"));

        var observacoes = new List<string>
        {
            "Tem direito quem foi dispensado sem justa causa, inclusive na rescisão indireta e na rescisão antecipada do contrato a prazo pelo empregador. Não cabe no pedido de demissão, na justa causa, no acordo (CLT, art. 484-A, § 4º) nem no fim normal do contrato a prazo.",
            "Também é preciso não ter renda própria suficiente para a família e não receber benefício de prestação continuada da Previdência, exceto pensão por morte e auxílio-acidente.",
            $"Os meses trabalhados são considerados os imediatamente anteriores à dispensa: a carência exige {Carencia(r.Solicitacao)}.",
            "Peça de 7 a 120 dias depois da dispensa, pela Carteira de Trabalho Digital ou pelo gov.br. Entre um benefício e outro é preciso esperar 16 meses.",
            "O seguro-desemprego é pago pelo governo, com recursos do FAT, e não pela empresa; não tem desconto de INSS nem de IRRF.",
            $"Tabela de {Formato.Competencia(tabelas.Competencia)}, a vigente na data da dispensa; ela é reajustada todo ano em janeiro pelo INPC."
        };

        return new DemonstrativoDto(
            "Seguro-desemprego",
            $"Dispensa em {Formato.Data(r.Dispensa)} • {char.ToUpper(nomeSolicitacao[0])}{nomeSolicitacao[1..]}",
            [
                new("Valor da parcela", Formato.Moeda(parcela.Valor), parcela.Valor > parcela.ValorPelaTabela ? "O salário mínimo, que é o piso" : $"Faixa {parcela.Faixa} da tabela"),
                new("Parcelas", quantidade > 0 ? quantidade.ToString(Formato.Cultura) : "Sem direito", $"{meses} meses trabalhados"),
                new("Total do benefício", Formato.Moeda(total), quantidade > 0 ? $"{quantidade} x {Formato.Moeda(parcela.Valor)}" : "Carência não cumprida"),
                new("Média salarial", Formato.Moeda(media), salarios.Length == 1 ? "Do último salário" : $"Dos últimos {salarios.Length} salários")
            ],
            quantidade > 0 ? [new("Seguro-desemprego", $"{quantidade} parcelas", total)] : [],
            [],
            [],
            [new GrupoMemoriaDto("Seguro-desemprego", quantidade > 0 ? $"Total: {Formato.Moeda(total)}" : "Sem direito", formulas)],
            observacoes,
            RotuloResultado: "Total do benefício");
    }

    /// <summary>O doméstico recebe um salário mínimo por parcela, sem a média dos salários (LC 150/2015, arts. 26 a 28).</summary>
    private static DemonstrativoDto Domestico(SimularSeguroDesempregoRequest r, decimal salarioMinimo)
    {
        var meses = Math.Min(r.MesesTrabalhados, 24);
        var quantidade = RegrasSeguroDesemprego.ParcelasDomestico(meses);
        var total = salarioMinimo * quantidade;
        var formulas = new List<FormulaDto>
        {
            new("Valor da parcela", $"Um salário mínimo: {Formato.Moeda(salarioMinimo)} (LC 150/2015, art. 26)"),
            new("Quantidade de parcelas", quantidade > 0
                ? $"{meses} meses trabalhados como doméstico nos últimos 24: até 3 parcelas"
                : $"São exigidos {RegrasSeguroDesemprego.MesesMinimosDomestico} meses de trabalho nos últimos 24; com {meses}, não há direito (art. 28)")
        };
        if (quantidade > 0)
            formulas.Add(new("Total do benefício", $"{quantidade} x {Formato.Moeda(salarioMinimo)} = {Formato.Moeda(total)}"));
        var observacoes = new List<string>
        {
            "O empregado doméstico dispensado sem justa causa tem direito a até 3 parcelas de um salário mínimo, se trabalhou como doméstico pelo menos 15 meses nos últimos 24 (LC 150/2015, arts. 26 e 28). Não cabe no pedido de demissão nem na justa causa.",
            "Peça de 7 a 90 dias depois da dispensa, pela Carteira de Trabalho Digital ou pelo gov.br (LC 150/2015, art. 29).",
            "O seguro-desemprego é pago pelo governo, com recursos do FAT, e não pelo empregador; não tem desconto de INSS nem de IRRF."
        };
        return new DemonstrativoDto(
            "Seguro-desemprego do doméstico",
            $"Dispensa em {Formato.Data(r.Dispensa)}",
            [
                new("Valor da parcela", Formato.Moeda(salarioMinimo), "Um salário mínimo"),
                new("Parcelas", quantidade > 0 ? quantidade.ToString(Formato.Cultura) : "Sem direito", $"{meses} meses nos últimos 24"),
                new("Total do benefício", Formato.Moeda(total), quantidade > 0 ? $"{quantidade} x {Formato.Moeda(salarioMinimo)}" : "Carência não cumprida")
            ],
            quantidade > 0 ? [new("Seguro-desemprego", $"{quantidade} parcelas", total)] : [],
            [],
            [],
            [new GrupoMemoriaDto("Seguro-desemprego do doméstico", quantidade > 0 ? $"Total: {Formato.Moeda(total)}" : "Sem direito", formulas)],
            observacoes,
            RotuloResultado: "Total do benefício");
    }

    private static string FormulaDaFaixa(decimal media, ParcelaSeguroDesemprego parcela)
    {
        if (parcela.Percentual == 0m)
            return $"Média acima de {Formato.Moeda(parcela.LimiteAnterior)}: valor máximo de {Formato.Moeda(parcela.ValorFixo)}";
        if (parcela.LimiteAnterior == 0m && parcela.ValorFixo == 0m)
            return $"{Formato.Moeda(media)} x {Formato.PercentualCurto(parcela.Percentual)} = {Formato.Moeda(parcela.ValorPelaTabela)}";
        return $"{Formato.Moeda(parcela.ValorFixo)} + ({Formato.Moeda(media)} - {Formato.Moeda(parcela.LimiteAnterior)}) x {Formato.PercentualCurto(parcela.Percentual)} = {Formato.Moeda(parcela.ValorPelaTabela)}";
    }

    private static string NomeSolicitacao(SolicitacaoSeguroDesemprego solicitacao) => solicitacao switch
    {
        SolicitacaoSeguroDesemprego.Primeira => "1ª solicitação",
        SolicitacaoSeguroDesemprego.Segunda => "2ª solicitação",
        _ => "3ª solicitação ou seguinte"
    };

    private static string Carencia(SolicitacaoSeguroDesemprego solicitacao) => solicitacao switch
    {
        SolicitacaoSeguroDesemprego.Primeira => "salário em pelo menos 12 dos últimos 18 meses, na 1ª solicitação",
        SolicitacaoSeguroDesemprego.Segunda => "salário em pelo menos 9 dos últimos 12 meses, na 2ª solicitação",
        _ => "salário em cada um dos 6 últimos meses, da 3ª solicitação em diante"
    };
}
