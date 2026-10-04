using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using System.Diagnostics;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>
/// Roda o instalador do Inno Setup em modo silencioso: só a barra de progresso aparece, as tabelas cadastradas são
/// preservadas, e o aplicativo é aberto de novo no fim. O aplicativo fecha logo em seguida para liberar os arquivos.
/// </summary>
public sealed class WpfExecutorInstalador : IExecutorInstalador
{
    private const string Parametros = "/SILENT /SP- /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /NORESTART";

    public void InstalarEFechar(string instalador)
    {
        Process.Start(new ProcessStartInfo(instalador, Parametros) { UseShellExecute = true });
        System.Windows.Application.Current.Shutdown();
    }
}
