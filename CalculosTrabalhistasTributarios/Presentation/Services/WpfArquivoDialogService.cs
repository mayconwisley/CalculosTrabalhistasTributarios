using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

public sealed class WpfArquivoDialogService : IArquivoDialogService
{
    public string? SolicitarDestinoPdf(string nomeArquivoSugerido) =>
        Solicitar("Salvar relatório em PDF", "Arquivo PDF (*.pdf)|*.pdf", ".pdf", nomeArquivoSugerido);

    public string? SolicitarDestinoPlanilha(string nomeArquivoSugerido) =>
        Solicitar("Salvar planilha do Excel", "Planilha do Excel (*.xlsx)|*.xlsx", ".xlsx", Path.ChangeExtension(nomeArquivoSugerido, ".xlsx"));

    private static string? Solicitar(string titulo, string filtro, string extensao, string nomeArquivoSugerido)
    {
        var dialogo = new SaveFileDialog
        {
            Title = titulo,
            Filter = filtro,
            DefaultExt = extensao,
            AddExtension = true,
            FileName = nomeArquivoSugerido
        };
        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }
}
