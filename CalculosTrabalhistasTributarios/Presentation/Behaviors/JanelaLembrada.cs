using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>
/// Lembra o tamanho e a posição de cada janela, pela chave da calculadora, e a mantém dentro da área útil do monitor. Com
/// escala de 125% ou 150% do Windows, uma tela de 1366 x 768 tem só cerca de 1093 x 580 unidades livres: os tamanhos
/// mínimos dos formulários são reduzidos ao que cabe, e a página rola.
/// </summary>
public static class JanelaLembrada
{
    public static readonly DependencyProperty ChaveProperty = DependencyProperty.RegisterAttached(
        "Chave", typeof(string), typeof(JanelaLembrada), new PropertyMetadata(null, AoDefinirChave));

    public static string? GetChave(DependencyObject objeto) => (string?)objeto.GetValue(ChaveProperty);

    public static void SetChave(DependencyObject objeto, string? valor) => objeto.SetValue(ChaveProperty, valor);

    private static void AoDefinirChave(DependencyObject objeto, DependencyPropertyChangedEventArgs argumentos)
    {
        if (objeto is not Window janela || argumentos.OldValue is not null || argumentos.NewValue is not string chave || chave.Length == 0)
            return;
        // Em escalas fracionárias, como 125%, arredonda as medidas aos pixels do monitor: bordas de 1 pixel e linhas das
        // grades ficam nítidas em vez de borradas entre dois pixels.
        janela.UseLayoutRounding = true;
        Restaurar(janela, chave);
        janela.SourceInitialized += (_, _) => CaberNoMonitor(janela);
        janela.Closing += (_, _) => Guardar(janela, chave);
    }

    /// <summary>Aplica o último tamanho e posição, se ainda estiverem numa tela; um monitor desconectado não esconde a janela.</summary>
    private static void Restaurar(Window janela, string chave)
    {
        if (!ConfiguracoesUsuario.Atuais.Janelas.TryGetValue(chave, out var posicao) || posicao.Width <= 0 || posicao.Height <= 0)
            return;
        var tela = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var titulo = new Rect(posicao.Left, posicao.Top, posicao.Width, 40);
        janela.Width = Math.Max(janela.MinWidth, posicao.Width);
        janela.Height = Math.Max(janela.MinHeight, posicao.Height);
        if (tela.IntersectsWith(titulo))
        {
            janela.WindowStartupLocation = WindowStartupLocation.Manual;
            janela.Left = posicao.Left;
            janela.Top = posicao.Top;
        }
        if (posicao.Maximizada)
            janela.WindowState = WindowState.Maximized;
    }

    private static void Guardar(Window janela, string chave)
    {
        if (janela.WindowState == WindowState.Minimized)
            return;
        var limites = janela.WindowState == WindowState.Maximized ? janela.RestoreBounds : new Rect(janela.Left, janela.Top, janela.ActualWidth, janela.ActualHeight);
        if (limites.IsEmpty || double.IsNaN(limites.Left) || limites.Width <= 0 || limites.Height <= 0)
            return;
        ConfiguracoesUsuario.Alterar(configuracoes => configuracoes.Janelas[chave] =
            new PosicaoJanela(Math.Round(limites.Left), Math.Round(limites.Top), Math.Round(limites.Width), Math.Round(limites.Height), janela.WindowState == WindowState.Maximized));
    }

    /// <summary>Reduz os mínimos e o tamanho ao que cabe na área útil do monitor da janela e traz a janela para dentro dela.</summary>
    private static void CaberNoMonitor(Window janela)
    {
        if (AreaUtil(janela) is not { } area)
            return;
        var normal = janela.WindowState == WindowState.Normal;
        var encaixe = Encaixar(area, new Size(janela.MinWidth, janela.MinHeight),
            new Rect(janela.Left, janela.Top, double.IsNaN(janela.Width) ? janela.ActualWidth : janela.Width, double.IsNaN(janela.Height) ? janela.ActualHeight : janela.Height));
        janela.MinWidth = encaixe.Minimo.Width;
        janela.MinHeight = encaixe.Minimo.Height;
        janela.Width = encaixe.Janela.Width;
        janela.Height = encaixe.Janela.Height;
        if (normal && !double.IsNaN(janela.Left) && !double.IsNaN(janela.Top))
        {
            janela.Left = encaixe.Janela.Left;
            janela.Top = encaixe.Janela.Top;
        }
    }

    /// <summary>
    /// Mínimos e limites da janela dentro da <paramref name="area"/>: nada maior que a área, e a janela inteira nela. Uma
    /// posição ainda não definida (NaN) continua indefinida, para a janela ser centralizada sobre a que a abriu.
    /// </summary>
    public static (Size Minimo, Rect Janela) Encaixar(Rect area, Size minimo, Rect janela)
    {
        var minimoAjustado = new Size(Math.Min(minimo.Width, area.Width), Math.Min(minimo.Height, area.Height));
        var largura = Math.Min(Math.Max(janela.Width, minimoAjustado.Width), area.Width);
        var altura = Math.Min(Math.Max(janela.Height, minimoAjustado.Height), area.Height);
        var esquerda = double.IsNaN(janela.X) ? double.NaN : Math.Max(area.Left, Math.Min(janela.X, area.Right - largura));
        var topo = double.IsNaN(janela.Y) ? double.NaN : Math.Max(area.Top, Math.Min(janela.Y, area.Bottom - altura));
        return (minimoAjustado, new Rect { X = esquerda, Y = topo, Width = largura, Height = altura });
    }

    /// <summary>Área de trabalho do monitor da janela, sem a barra de tarefas, em unidades WPF (DIPs).</summary>
    private static Rect? AreaUtil(Window janela)
    {
        var alca = new WindowInteropHelper(janela).Handle;
        var origem = PresentationSource.FromVisual(janela);
        if (alca == IntPtr.Zero || origem?.CompositionTarget is null)
            return null;
        var monitor = MonitorFromWindow(alca, MonitorDefaultToNearest);
        var informacoes = new MonitorInfo { Tamanho = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref informacoes))
            return null;
        var paraDips = origem.CompositionTarget.TransformFromDevice;
        var trabalho = informacoes.Trabalho;
        return new Rect(paraDips.Transform(new Point(trabalho.Left, trabalho.Top)), paraDips.Transform(new Point(trabalho.Right, trabalho.Bottom)));
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Retangulo { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Tamanho;
        public Retangulo Monitor;
        public Retangulo Trabalho;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr janela, uint opcoes);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo informacoes);
}
