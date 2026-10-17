using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>As faixas do INSS ou do IRRF da memória em tabela, com os valores já formatados para a tela.</summary>
public sealed record QuadroFaixasViewModel(IReadOnlyList<LinhaQuadroFaixasViewModel> Linhas, string BaseCalculo, string ValorExato, string Valor)
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static QuadroFaixasViewModel De(QuadroFaixasDto quadro) => new(
        quadro.Faixas.Select(faixa => new LinhaQuadroFaixasViewModel(
            faixa.Numero.ToString(CulturaPtBr),
            Moeda(faixa.De),
            faixa.Ate is { } ate ? Moeda(ate) : "Sem limite",
            Moeda(faixa.ParteDaBase),
            faixa.Aliquota.ToString("0.##", CulturaPtBr) + "%",
            MoedaExata(faixa.ValorExato),
            MoedaExata(faixa.Valor))).ToArray(),
        Moeda(quadro.BaseCalculo),
        MoedaExata(quadro.ValorExato),
        Moeda(quadro.Valor));

    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"Soma das faixas: base {BaseCalculo}, valor exato {ValorExato}, valor considerado {Valor}";

    private static string Moeda(decimal valor) => valor.ToString("C2", CulturaPtBr);

    /// <summary>As casas que o valor tiver, até seis, como nas fórmulas: "R$ 115,3656".</summary>
    private static string MoedaExata(decimal valor) => "R$ " + valor.ToString("#,##0.00####", CulturaPtBr);
}
