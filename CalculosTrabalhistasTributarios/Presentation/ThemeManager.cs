using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CalculosTrabalhistasTributarios.Presentation;

[SupportedOSPlatform("windows")]
public static class ThemeManager
{
    // O logo é exibido com 52 px; decodificar com 156 px cobre até 300% de escala sem carregar o PNG original inteiro.
    private const int LogoDecodePixelWidth = 156;
    private static bool? _appliedDarkTheme;

    public static ThemeMode CurrentMode { get; private set; } = ThemeMode.Automatico;
    public static bool UseHardwareAcceleration => ConfiguracoesUsuario.Atuais.HardwareAcceleration;

    public static void Initialize()
    {
        CurrentMode = Enum.TryParse<ThemeMode>(ConfiguracoesUsuario.Atuais.Theme, true, out var mode) ? mode : ThemeMode.Automatico;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Apply(CurrentMode, false);
    }

    public static void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    public static void Apply(ThemeMode mode, bool persist = true)
    {
        CurrentMode = mode;
        if (persist) ConfiguracoesUsuario.Alterar(configuracoes => configuracoes.Theme = mode.ToString());

        var isDarkTheme = mode == ThemeMode.Escuro || mode == ThemeMode.Automatico && IsWindowsUsingDarkTheme();
        if (_appliedDarkTheme == isDarkTheme) return;

        _appliedDarkTheme = isDarkTheme;
        var resources = System.Windows.Application.Current.Resources;
        foreach (var (key, value) in isDarkTheme ? DarkResources.Value : LightResources.Value)
            resources[key] = value;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (CurrentMode == ThemeMode.Automatico && e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => Apply(CurrentMode, false));
    }

    private static (string Key, object Value)[] CreateResources((string Key, string Color)[] palette, string logoFileName) => palette
        .Select(item => (item.Key, (object)CreateBrush(item.Color)))
        .Append(("BrandLogoImage", CreateLogo(logoFileName)))
        .ToArray();

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static BitmapImage CreateLogo(string fileName)
    {
        var logo = new BitmapImage();
        logo.BeginInit();
        logo.UriSource = new Uri($"pack://application:,,,/Assets/{fileName}", UriKind.Absolute);
        logo.DecodePixelWidth = LogoDecodePixelWidth;
        logo.CacheOption = BitmapCacheOption.OnLoad;
        logo.EndInit();
        logo.Freeze();
        return logo;
    }

    private static bool IsWindowsUsingDarkTheme()
    {
        const string Key = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        return Registry.GetValue(Key, "AppsUseLightTheme", 1) is int lightTheme && lightTheme == 0;
    }



    private static readonly (string Key, string Color)[] Light =
    [
        ("ApplicationBackgroundBrush", "#F8FAFC"), ("SurfaceBrush", "#FFFFFF"), ("InputBackgroundBrush", "#FFFFFF"), ("ResultBackgroundBrush", "#F8FAFC"), ("BorderBrush", "#E2E8F0"), ("TextBrush", "#101828"), ("MutedTextBrush", "#667085"), ("AccentBrush", "#1570EF"), ("AccentHoverBrush", "#175CD3"), ("AccentForegroundBrush", "#FFFFFF"), ("AdvantageBackgroundBrush", "#ECFDF3"), ("AdvantageBorderBrush", "#86EFAC"), ("AdvantageTextBrush", "#027A48"), ("ButtonBackgroundBrush", "#F9FAFB"), ("ButtonHoverBrush", "#F2F4F7"), ("DisabledBrush", "#EAECF0"), ("DataGridAlternateBrush", "#F9FAFB"), ("ScrollThumbBrush", "#CBD5E1"), ("ScrollThumbHoverBrush", "#94A3B8"), ("SelectionBrush", "#DBEAFE")
    ];

    private static readonly (string Key, string Color)[] Dark =
    [
        ("ApplicationBackgroundBrush", "#101828"), ("SurfaceBrush", "#182230"), ("InputBackgroundBrush", "#101828"), ("ResultBackgroundBrush", "#101828"), ("BorderBrush", "#344054"), ("TextBrush", "#F9FAFB"), ("MutedTextBrush", "#98A2B3"), ("AccentBrush", "#2E90FA"), ("AccentHoverBrush", "#53B1FD"), ("AccentForegroundBrush", "#FFFFFF"), ("AdvantageBackgroundBrush", "#123B2A"), ("AdvantageBorderBrush", "#32D583"), ("AdvantageTextBrush", "#6CE9A6"), ("ButtonBackgroundBrush", "#25354D"), ("ButtonHoverBrush", "#344054"), ("DisabledBrush", "#25354D"), ("DataGridAlternateBrush", "#1D2939"), ("ScrollThumbBrush", "#475467"), ("ScrollThumbHoverBrush", "#667085"), ("SelectionBrush", "#1E3A5F")
    ];

    // Depois das paletas: os campos estáticos são inicializados na ordem em que aparecem.
    private static readonly Lazy<(string Key, object Value)[]> LightResources = new(() => CreateResources(Light, "logo-light.png"));
    private static readonly Lazy<(string Key, object Value)[]> DarkResources = new(() => CreateResources(Dark, "logo-dark.png"));
}
