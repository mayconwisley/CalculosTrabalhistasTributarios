using CalculosTrabalhistasTributarios.Application.DTOs;
using ClosedXML.Excel;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Formatos de célula, nome das abas, gravação do arquivo e seções comuns a todas as planilhas.</summary>
internal static class ComponentesPlanilha
{
    internal static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    internal static void Observacoes(Aba aba, IReadOnlyList<string> observacoes)
    {
        if (observacoes.Count == 0)
            return;
        aba.Secao("Observações");
        foreach (var observacao in observacoes)
            aba.Texto(observacao);
    }

    internal static Task SalvarAsync(string caminhoArquivo, CancellationToken cancellationToken, Action<XLWorkbook> montar) =>
        Task.Run(() =>
        {
            using var pasta = new XLWorkbook();
            pasta.Properties.Author = "Cálculos Trabalhistas e Tributários";
            pasta.Properties.Created = DateTime.Now;
            montar(pasta);
            pasta.SaveAs(caminhoArquivo);
        }, cancellationToken);

    internal static Celula Moeda(decimal valor) => new(valor, "\"R$\" #,##0.00;-\"R$\" #,##0.00");

    internal static readonly string[] ColunasDoQuadro = ["Faixa", "De", "Até", "Parte da base", "Alíquota", "Valor exato", "Valor considerado"];

    /// <summary>As faixas do INSS ou do IRRF em células numéricas, com o valor exato antes de truncar ou arredondar.</summary>
    internal static void QuadroFaixas(Aba aba, QuadroFaixasDto quadro)
    {
        aba.Cabecalho(ColunasDoQuadro);
        foreach (var faixa in quadro.Faixas)
            aba.Linha(faixa.Numero, Moeda(faixa.De), faixa.Ate is { } ate ? Moeda(ate) : "Sem limite", Moeda(faixa.ParteDaBase),
                new Celula(faixa.Aliquota / 100m, "0.0#%"), MoedaExata(faixa.ValorExato), MoedaExata(faixa.Valor));
        aba.Linha(true, "Soma das faixas", "", "", Moeda(quadro.BaseCalculo), "", MoedaExata(quadro.ValorExato), Moeda(quadro.Valor));
    }

    /// <summary>Moeda com até seis casas, para o produto exato de cada faixa.</summary>
    internal static Celula MoedaExata(decimal valor) => new(valor, "\"R$\" #,##0.00####;-\"R$\" #,##0.00####");

    internal static Celula Percentual(decimal percentual) => new(percentual / 100m, "0.00%");

    internal static DateOnly Data(DateOnly data) => data;
}
