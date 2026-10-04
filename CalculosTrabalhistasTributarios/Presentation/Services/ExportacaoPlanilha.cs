using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using System.IO;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>Fluxo comum de todas as janelas para salvar o resultado no Excel.</summary>
public static class ExportacaoPlanilha
{
    /// <summary>Pede o destino, gera a planilha e avisa quando o arquivo está aberto em outro programa.</summary>
    public static async Task SalvarAsync(IArquivoDialogService dialogo, IUserNotifier notificador, string nomeSugerido, Func<string, Task> gerar)
    {
        var caminho = dialogo.SolicitarDestinoPlanilha(nomeSugerido);
        if (caminho is null)
            return;

        try
        {
            await gerar(caminho);
        }
        catch (IOException)
        {
            notificador.MostrarAviso($"Não foi possível salvar {Path.GetFileName(caminho)}. Se a planilha estiver aberta no Excel, feche-a e tente de novo.");
        }
        catch (Exception exception)
        {
            notificador.MostrarErro("Não foi possível gerar a planilha do Excel.", exception);
        }
    }
}
