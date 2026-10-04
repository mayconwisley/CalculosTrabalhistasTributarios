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

    internal static Celula Percentual(decimal percentual) => new(percentual / 100m, "0.00%");

    internal static DateOnly Data(DateOnly data) => data;
}
