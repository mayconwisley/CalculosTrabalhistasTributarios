using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>
/// Demonstrativo da pensão em atraso a partir da simulação já apurada: os mesmos totais, ritos, critérios, observações e
/// parcelas da janela e do PDF do desktop, sem recalcular correção nem juros.
/// </summary>
public static class DemonstrativoPensaoAtraso
{
    public static DemonstrativoDto Montar(SimulacaoPensaoAtrasoDto simulacao)
    {
        var acrescimos = Acrescimos(simulacao);
        var nomeCorrecao = SimulacaoPensaoAtrasoDto.NomeCorrecao(simulacao.Correcao);

        // Devido - pago + correção + juros = total atualizado, porque cada parcela vale o saldo corrigido mais os juros; com a
        // multa e os honorários do rito da penhora, o resultado é o total com acréscimos do desktop.
        var proventos = new List<VerbaDto>
        {
            new("Valor devido", TextoParcelas(simulacao.Parcelas.Count), simulacao.TotalDevido),
            new($"Correção ({nomeCorrecao})", simulacao.CorrecaoAte is { } ate ? $"até {Formato.Competencia(ate)}" : "", simulacao.TotalCorrecao),
            new("Juros de mora", "", simulacao.TotalJuros)
        };
        if (simulacao.Multa > 0m)
            proventos.Add(new("Multa de 10% (CPC, art. 523)", "", simulacao.Multa));
        if (simulacao.Honorarios > 0m)
            proventos.Add(new("Honorários de 10% (CPC, art. 523)", "", simulacao.Honorarios));
        var descontos = simulacao.TotalPago > 0m
            ? new List<VerbaDto> { new("Pagamentos parciais", "", simulacao.TotalPago) }
            : [];

        return new DemonstrativoDto(
            "Pensão em atraso",
            simulacao.DataAjuizamento is { } ajuizamento
                ? $"Cálculo em {Formato.Data(simulacao.DataCalculo)} • ajuizamento em {Formato.Data(ajuizamento)}"
                : $"Cálculo em {Formato.Data(simulacao.DataCalculo)}",
            [
                new("Total atualizado", Formato.Moeda(simulacao.TotalComAcrescimos), simulacao.TemAcrescimos
                    ? $"Em {Formato.Data(simulacao.DataCalculo)}, com {acrescimos}" : $"Em {Formato.Data(simulacao.DataCalculo)}"),
                new("Rito da prisão", Formato.Moeda(simulacao.TotalPrisao), TextoParcelas(simulacao.QuantidadePrisao)),
                new("Rito da penhora", Formato.Moeda(simulacao.TotalPenhoraComAcrescimos), simulacao.TemAcrescimos
                    ? $"{TextoParcelas(simulacao.QuantidadePenhora)} e {Formato.Moeda(simulacao.Multa + simulacao.Honorarios)} de {acrescimos}"
                    : TextoParcelas(simulacao.QuantidadePenhora)),
                new("Saldo original", Formato.Moeda(simulacao.TotalSaldo), simulacao.TotalPago > 0m ? $"Devido menos {Formato.Moeda(simulacao.TotalPago)} pagos" : "Sem pagamentos parciais"),
                new("Correção", Formato.Moeda(simulacao.TotalCorrecao), simulacao.CorrecaoAte is { } correcaoAte ? $"{nomeCorrecao} até {Formato.Competencia(correcaoAte)}" : "Sem correção"),
                new("Juros de mora", Formato.Moeda(simulacao.TotalJuros), NomeJuros(simulacao.Juros))
            ],
            proventos,
            descontos,
            [],
            [Parcelas(simulacao)],
            [.. simulacao.Criterios, .. simulacao.Observacoes],
            RotuloProventos: "Débito e acréscimos",
            RotuloResultado: simulacao.TemAcrescimos ? "Total com acréscimos" : "Total atualizado",
            Comparativo: Ritos(simulacao));
    }

    private static TabelaComparativaDto Ritos(SimulacaoPensaoAtrasoDto simulacao)
    {
        var linhas = new List<LinhaComparativaDto>
        {
            new("Parcelas", [TextoParcelas(simulacao.QuantidadePrisao), TextoParcelas(simulacao.QuantidadePenhora)]),
            new("Débito atualizado", [Formato.Moeda(simulacao.TotalPrisao), Formato.Moeda(simulacao.TotalPenhora)])
        };
        // A multa e os honorários do art. 523 do CPC só incidem no rito da penhora.
        if (simulacao.TemAcrescimos)
        {
            linhas.Add(new("Multa de 10%", ["Não se aplica", Formato.Moeda(simulacao.Multa)]));
            linhas.Add(new("Honorários de 10%", ["Não se aplicam", simulacao.Honorarios > 0m ? Formato.Moeda(simulacao.Honorarios) : "Não incluídos"]));
        }
        linhas.Add(new("Total", [Formato.Moeda(simulacao.TotalPrisao), Formato.Moeda(simulacao.TotalPenhoraComAcrescimos)], true));
        return new TabelaComparativaDto("Ritos de cobrança", ["Rito da prisão", "Rito da penhora"], linhas);
    }

    private static GrupoMemoriaDto Parcelas(SimulacaoPensaoAtrasoDto simulacao) => new(
        "Parcelas",
        $"Total atualizado: {Formato.Moeda(simulacao.TotalAtualizado)}",
        simulacao.Parcelas.Select(parcela => new FormulaDto(
            $"{Formato.Competencia(parcela.Competencia)} • vence em {Formato.Data(parcela.Vencimento)} • {(parcela.RitoPrisao ? "prisão" : "penhora")}",
            $"{Saldo(parcela)} × fator {parcela.FatorCorrecao.ToString("N6", Formato.Cultura)} = {Formato.Moeda(parcela.Corrigido)}; " +
            $"juros de {parcela.PercentualJuros.ToString("N4", Formato.Cultura)}% = {Formato.Moeda(parcela.Juros)}; total {Formato.Moeda(parcela.Total)}")).ToArray());

    private static string Saldo(ParcelaAtrasoDto parcela) => parcela.Pago > 0m
        ? $"({Formato.Moeda(parcela.Devido)} - {Formato.Moeda(parcela.Pago)} pagos) = {Formato.Moeda(parcela.Saldo)}"
        : Formato.Moeda(parcela.Saldo);

    /// <summary>O mesmo texto das opções de juros do formulário.</summary>
    public static string NomeJuros(JurosDeMora juros) => juros switch
    {
        JurosDeMora.UmPorCentoAteTaxaLegal => "1% ao mês até 29/08/2024; depois, taxa legal",
        JurosDeMora.UmPorCento => "1% ao mês",
        JurosDeMora.TaxaLegal => "Taxa legal (desde 30/08/2024)",
        _ => "Sem juros"
    };

    private static string TextoParcelas(int quantidade) => quantidade == 1 ? "1 parcela" : $"{quantidade} parcelas";
    private static string Acrescimos(SimulacaoPensaoAtrasoDto simulacao) => simulacao.Honorarios > 0m ? "multa e honorários" : "multa";
}
