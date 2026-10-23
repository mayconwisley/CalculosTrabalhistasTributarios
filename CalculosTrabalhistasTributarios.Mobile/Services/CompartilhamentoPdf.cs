using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Mobile.Platforms.Android.Reporting;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class CompartilhamentoPdf(IShare compartilhamento, IFileSystem arquivos) : ICompartilhamentoPdf
{
    public async Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken)
    {
        var pasta = Path.Combine(arquivos.CacheDirectory, "sharing-root");
        var caminho = Path.Combine(pasta, $"simulacao-fgts-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.pdf");
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(pasta);
            // Não remova ao fechar o menu: o aplicativo destinatário ainda pode estar lendo o arquivo.
            foreach (var arquivo in Directory.EnumerateFiles(pasta, "simulacao-fgts-*.pdf"))
            {
                if (File.GetLastWriteTimeUtc(arquivo) < DateTime.UtcNow.AddDays(-7))
                    File.Delete(arquivo);
            }
            try
            {
                GeradorDemonstrativoPdf.Gerar(demonstrativo, caminho, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                File.Delete(caminho);
                throw;
            }
        }, cancellationToken);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await compartilhamento.RequestAsync(new ShareFileRequest
            {
                Title = "Compartilhar simulação em PDF",
                File = new ShareFile(caminho, "application/pdf")
            });
        });
    }
}
