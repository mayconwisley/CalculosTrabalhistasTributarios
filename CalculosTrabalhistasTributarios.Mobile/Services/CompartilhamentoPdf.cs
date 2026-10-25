using Microsoft.Extensions.Logging;
using CalculosTrabalhistasTributarios.Infrastructure.Arquivos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Mobile.Platforms.Android.Reporting;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Resultado;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class CompartilhamentoPdf(IShare compartilhamento, IFileSystem arquivos, ILogger<CompartilhamentoPdf> logger) : ICompartilhamentoPdf
{
    public async Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken)
    {
        var pasta = Path.Combine(arquivos.CacheDirectory, "sharing-root");
        var caminho = Path.Combine(pasta, $"simulacao-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.pdf");
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(pasta);
            // O destinatário pode continuar lendo depois que o menu é fechado.
            var falhas = LimpezaCacheCompartilhamento.Executar(pasta, DateTime.UtcNow, cancellationToken);
            if (falhas > 0) logger.LogWarning("Limpeza do cache de compartilhamento teve {Falhas} falhas de acesso.", falhas);
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

    public Task CompartilharResumoAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken)
    {
        var texto = ResumoTextoDemonstrativo.Montar(demonstrativo);
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await compartilhamento.RequestAsync(new ShareTextRequest
            {
                Title = "Compartilhar resumo da simulação",
                Subject = demonstrativo.Titulo,
                Text = texto
            });
        });
    }
}
