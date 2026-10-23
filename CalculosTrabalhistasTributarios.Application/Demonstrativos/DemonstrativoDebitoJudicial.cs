using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>
/// Demonstrativo da atualização de débitos judiciais para quem só exibe <see cref="DemonstrativoDto"/>, como o aplicativo
/// móvel. Reorganiza o resultado já apurado com os mesmos valores, critérios e observações da janela e do PDF do desktop;
/// não recalcula a correção, os juros nem os acréscimos.
/// </summary>
public static class DemonstrativoDebitoJudicial
{
    public static DemonstrativoDto Montar(SimulacaoDebitoJudicialDto simulacao)
    {
        var trabalhista = simulacao.Natureza == NaturezaDebito.Trabalhista;
        var destaques = new List<DestaqueDto>
        {
            new("Total atualizado", Formato.Moeda(simulacao.TotalComAcrescimos), simulacao.TemAcrescimos ? "Com multa e honorários" : $"Em {Formato.Data(simulacao.DataCalculo)}"),
            new("Valor original", Formato.Moeda(simulacao.TotalValor), simulacao.Parcelas.Count == 1 ? "1 parcela" : $"{simulacao.Parcelas.Count} parcelas"),
            new("Correção", Formato.Moeda(simulacao.TotalCorrecao), trabalhista ? "IPCA-E, Selic e IPCA" : "Índice, Selic e IPCA"),
            new("Juros", Formato.Moeda(simulacao.TotalJuros), trabalhista ? "TR e taxa legal" : "Taxa legal"),
            new("Multa e honorários", Formato.Moeda(simulacao.Multa + simulacao.Honorarios), simulacao.TemAcrescimos ? "CPC, art. 523, § 1º" : "Não incluídos")
        };

        // O resultado é a soma dos totais das parcelas e dos acréscimos: o mesmo "Total atualizado" da janela e do PDF.
        var proventos = simulacao.Parcelas
            .Select(parcela => new VerbaDto(parcela.Descricao, $"Venc. {Formato.Data(parcela.Vencimento)}", parcela.Total))
            .ToList();
        if (simulacao.Multa > 0m)
            proventos.Add(new VerbaDto("Multa de 10% (CPC, art. 523)", "10%", simulacao.Multa));
        if (simulacao.Honorarios > 0m)
            proventos.Add(new VerbaDto("Honorários de 10% (CPC, art. 523)", "10%", simulacao.Honorarios));

        var informativos = new List<VerbaDto>
        {
            new("Valor original", "Soma das parcelas", simulacao.TotalValor),
            new("Correção", "Atualizado - original", simulacao.TotalCorrecao),
            new("Valor atualizado", "Soma das parcelas", simulacao.TotalAtualizado),
            new("Juros", "Soma das parcelas", simulacao.TotalJuros)
        };

        var memoria = simulacao.Parcelas.Select(Memoria).ToList();
        if (simulacao.TemAcrescimos)
            memoria.Add(MemoriaAcrescimos(simulacao));

        return new DemonstrativoDto(
            $"Atualização de {simulacao.NomeNatureza.ToLowerInvariant()}",
            $"Cálculo em {Formato.Data(simulacao.DataCalculo)}",
            destaques,
            proventos,
            [],
            informativos,
            memoria,
            [.. simulacao.Criterios, .. simulacao.Observacoes],
            RotuloProventos: simulacao.TemAcrescimos ? "Parcelas atualizadas e acréscimos" : "Parcelas atualizadas",
            RotuloResultado: simulacao.TemAcrescimos ? "Total atualizado com acréscimos" : "Total atualizado do débito");
    }

    /// <summary>As colunas da grade de parcelas da janela e do PDF, com as operações que ligam uma à outra.</summary>
    private static GrupoMemoriaDto Memoria(ParcelaDebitoDto parcela)
    {
        var selic = parcela.PercentualSelic > 0m;
        var formulas = new List<FormulaDto>
        {
            new("Valor original", $"{Formato.Moeda(parcela.Valor)}, vencido em {Formato.Data(parcela.Vencimento)}"),
            new("Fator de correção", $"{parcela.FatorCorrecao.ToString("N6", Formato.Cultura)} (produto dos índices mensais de correção, sem a Selic)")
        };
        if (selic)
            formulas.Add(new("Selic", $"{parcela.PercentualSelic.ToString("N4", Formato.Cultura)}% (reúne a correção e os juros no período em que se aplica)"));
        formulas.Add(new("Valor atualizado", selic
            ? $"{Formato.Moeda(parcela.Valor)} x fator de correção x (1 + Selic) = {Formato.Moeda(parcela.Atualizado)}; nunca abaixo do valor original"
            : $"{Formato.Moeda(parcela.Valor)} x fator de correção = {Formato.Moeda(parcela.Atualizado)}; nunca abaixo do valor original"));
        formulas.Add(new("Juros", $"{Formato.Moeda(parcela.Atualizado)} x {parcela.PercentualJuros.ToString("N4", Formato.Cultura)}% = {Formato.Moeda(parcela.Juros)} (juros simples sobre o valor atualizado)"));
        formulas.Add(new("Total da parcela", $"{Formato.Moeda(parcela.Atualizado)} + {Formato.Moeda(parcela.Juros)} = {Formato.Moeda(parcela.Total)}"));
        return new GrupoMemoriaDto($"{parcela.Descricao} (venc. {Formato.Data(parcela.Vencimento)})", $"Total: {Formato.Moeda(parcela.Total)}", formulas);
    }

    private static GrupoMemoriaDto MemoriaAcrescimos(SimulacaoDebitoJudicialDto simulacao)
    {
        var formulas = new List<FormulaDto>
        {
            new("Débito atualizado", Formato.Moeda(simulacao.Total)),
            new("Multa de 10%", $"{Formato.Moeda(simulacao.Total)} x 10% = {Formato.Moeda(simulacao.Multa)}"),
            new("Honorários de 10%", simulacao.Honorarios > 0m
                ? $"{Formato.Moeda(simulacao.Total)} x 10% = {Formato.Moeda(simulacao.Honorarios)} (calculados sem a multa)"
                : "Não incluídos")
        };
        return new GrupoMemoriaDto("Multa e honorários (CPC, art. 523, § 1º)", $"Total com acréscimos: {Formato.Moeda(simulacao.TotalComAcrescimos)}", formulas);
    }
}
