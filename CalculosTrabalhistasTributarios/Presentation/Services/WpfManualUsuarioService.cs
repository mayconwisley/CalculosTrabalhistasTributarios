using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>
/// Abre o manual em PDF distribuído com a aplicação; sem o arquivo ou sem leitor de PDF, abre a versão publicada no GitHub.
/// Na janela de uma calculadora, abre na página da seção dela quando o leitor padrão aceita a página: navegadores (Edge,
/// Chrome, Firefox), Adobe Reader/Acrobat e SumatraPDF. Com outro leitor, o manual abre no início.
/// </summary>
public sealed class WpfManualUsuarioService(IUserNotifier notificador) : IManualUsuarioService
{
    private const string ManualOnline = "https://github.com/mayconwisley/CalculosTrabalhistasTributarios/blob/master/docs/MANUAL.md";
    private static readonly string ManualLocal = Path.Combine(AppContext.BaseDirectory, "Manual", "ManualDoUsuario.pdf");
    private readonly Lazy<IReadOnlyDictionary<string, int>> _indice = new(LerIndice);

    public void Abrir(string? chaveJanela = null)
    {
        if (File.Exists(ManualLocal))
        {
            var pagina = SecoesDoManual.Numero(chaveJanela) is { } numero ? IndiceManualPdf.Pagina(_indice.Value, numero) : null;
            if ((pagina is { } escolhida && TentarAbrirNaPagina(escolhida)) || TentarAbrir(new ProcessStartInfo(ManualLocal) { UseShellExecute = true }, out _))
                return;
        }

        if (!TentarAbrir(new ProcessStartInfo(ManualOnline) { UseShellExecute = true }, out var erro))
            notificador.MostrarErro("Não foi possível abrir o manual do usuário.", erro!);
    }

    private static IReadOnlyDictionary<string, int> LerIndice()
    {
        try
        {
            return IndiceManualPdf.Ler(File.ReadAllBytes(ManualLocal));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Dictionary<string, int>();
        }
    }

    /// <summary>Cada leitor recebe a página do seu jeito; um leitor desconhecido não é chamado.</summary>
    private static bool TentarAbrirNaPagina(int pagina)
    {
        if (LeitorPadraoDePdf() is not { } leitor)
            return false;
        var argumentos = Path.GetFileNameWithoutExtension(leitor).ToLowerInvariant() switch
        {
            "msedge" or "chrome" or "brave" or "firefox" or "vivaldi" or "opera" => $"\"{new Uri(ManualLocal).AbsoluteUri}#page={pagina}\"",
            "acrord32" or "acrobat" => $"/A \"page={pagina}\" \"{ManualLocal}\"",
            "sumatrapdf" => $"-page {pagina} \"{ManualLocal}\"",
            _ => null
        };
        return argumentos is not null && TentarAbrir(new ProcessStartInfo(leitor, argumentos) { UseShellExecute = false }, out _);
    }

    private static string? LeitorPadraoDePdf()
    {
        uint tamanho = 1024;
        var caminho = new StringBuilder((int)tamanho);
        return AssocQueryString(0, AssocStrExecutable, ".pdf", "open", caminho, ref tamanho) == 0 && File.Exists(caminho.ToString()) ? caminho.ToString() : null;
    }

    private static bool TentarAbrir(ProcessStartInfo inicio, out Exception? erro)
    {
        try
        {
            Process.Start(inicio);
            erro = null;
            return true;
        }
        catch (Exception exception)
        {
            erro = exception;
            return false;
        }
    }

    private const int AssocStrExecutable = 2;

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(int opcoes, int dado, string associacao, string? verbo, StringBuilder resultado, ref uint tamanho);
}
