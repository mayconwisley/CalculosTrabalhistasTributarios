using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;

namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>
/// Desenha a barra de título das janelas no tema do aplicativo. Sem isso, o Windows a pinta conforme o próprio tema,
/// e uma janela escura fica com a barra clara (ou o contrário) quando o tema do aplicativo difere do sistema.
/// </summary>
[SupportedOSPlatform("windows")]
public static class BarraDeTitulo
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE: disponível a partir do Windows 10 20H1; em versões anteriores a chamada falha e a barra fica como está.
    private const int UsarModoEscuro = 20;
    private static bool _escura;
    private static bool _registrada;

    public static void Aplicar(bool escura)
    {
        _escura = escura;
        if (!_registrada)
        {
            _registrada = true;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((janela, _) => Pintar((Window)janela)));
        }

        foreach (Window janela in System.Windows.Application.Current.Windows)
            Pintar(janela);
    }

    private static void Pintar(Window janela)
    {
        var handle = new WindowInteropHelper(janela).Handle;
        if (handle == IntPtr.Zero) return;

        var valor = _escura ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, UsarModoEscuro, ref valor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);
}
