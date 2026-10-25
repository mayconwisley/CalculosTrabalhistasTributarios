using System.Globalization;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Views;

/// <summary>Digitação direta com separadores assistidos e seleção nativa, preservando texto vazio ou inválido.</summary>
public sealed class EntradaPeriodoView : ContentView
{
    public static readonly BindableProperty TextoProperty = BindableProperty.Create(nameof(Texto), typeof(string), typeof(EntradaPeriodoView), "", BindingMode.TwoWay);
    public static readonly BindableProperty TipoProperty = BindableProperty.Create(nameof(Tipo), typeof(TipoCampoMobile), typeof(EntradaPeriodoView), TipoCampoMobile.Data, propertyChanged: (b, _, _) => ((EntradaPeriodoView)b).AtualizarTipo());
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(nameof(Rotulo), typeof(string), typeof(EntradaPeriodoView), "Data", propertyChanged: (b, _, _) => ((EntradaPeriodoView)b).AtualizarRotulo());
    public static readonly BindableProperty IdentificadorProperty = BindableProperty.Create(nameof(Identificador), typeof(string), typeof(EntradaPeriodoView), "", propertyChanged: (b, _, v) => ((EntradaPeriodoView)b)._entrada.AutomationId = (string)v);
    private readonly Entry _entrada = new() { Keyboard = Keyboard.Text, HorizontalTextAlignment = TextAlignment.Start, FontSize = 18, ReturnType = ReturnType.Next };
    private readonly Button _selecionar = new() { Text = "📅", FontSize = 22, Padding = new Thickness(8), MinimumWidthRequest = 48 };
    private readonly DatePicker _calendario = new() { Opacity = 0, WidthRequest = 1, HeightRequest = 1, InputTransparent = true, MinimumDate = new DateTime(1900, 1, 1), MaximumDate = new DateTime(9999, 12, 31) };
    private bool _sincronizando;

    public EntradaPeriodoView()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("BotaoSecundario", out var estilo) == true)
            _selecionar.Style = (Style)estilo;
        AutomationProperties.SetIsInAccessibleTree(_calendario, false);
        _entrada.SetBinding(Entry.TextProperty, new Binding(nameof(Texto), source: this, mode: BindingMode.TwoWay));
        _entrada.Unfocused += (_, _) => Texto = DigitacaoPeriodoMobile.Formatar(Texto ?? "", Tipo);
        var grade = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        grade.Add(_entrada); grade.Add(_selecionar, 1); grade.Add(_calendario);
        _selecionar.Clicked += Selecionar;
        _calendario.DateSelected += (_, args) =>
        {
            if (!_sincronizando && args.NewDate is { } data) Texto = data.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        };
        Content = grade;
        AtualizarTipo();
        AtualizarRotulo();
    }

    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public TipoCampoMobile Tipo { get => (TipoCampoMobile)GetValue(TipoProperty); set => SetValue(TipoProperty, value); }
    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public string Identificador { get => (string)GetValue(IdentificadorProperty); set => SetValue(IdentificadorProperty, value); }
    public bool FocarEntrada() => _entrada.Focus();

    private void AtualizarTipo()
    {
        _entrada.Placeholder = Tipo == TipoCampoMobile.Competencia ? "MM/yyyy" : "dd/MM/yyyy";
        _entrada.MaxLength = Tipo == TipoCampoMobile.Competencia ? 7 : 10;
    }
    private void AtualizarRotulo()
    {
        SemanticProperties.SetDescription(_entrada, Rotulo);
        SemanticProperties.SetDescription(_selecionar, "Escolher " + Rotulo);
    }

    private async void Selecionar(object? sender, EventArgs args)
    {
        if (Tipo == TipoCampoMobile.Data)
        {
            _sincronizando = true;
            // Sem data válida, null permite que confirmar o dia inicial também dispare DateSelected.
            _calendario.Date = DateTime.TryParseExact(Texto, "dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var data)
                && data >= _calendario.MinimumDate && data <= _calendario.MaximumDate ? data : null;
            _sincronizando = false;
            _calendario.IsOpen = true;
            return;
        }
        var pagina = this.FindParentPage();
        if (pagina is null) return;
        var referencia = DateTime.TryParseExact("01/" + Texto, "dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var competencia) ? competencia : DateTime.Today;
        var mes = new Picker { Title = "Mês", ItemsSource = CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.MonthNames.Take(12).ToArray(), SelectedIndex = referencia.Month - 1, AutomationId = "MesCompetencia" };
        var ano = new Entry { Text = referencia.Year.ToString(CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, MaxLength = 4, AutomationId = "AnoCompetencia" };
        SemanticProperties.SetDescription(mes, "Mês da competência"); SemanticProperties.SetDescription(ano, "Ano da competência");
        var erro = new Label { Text = "Informe um ano entre 1 e 9999.", IsVisible = false };
        var aplicar = new Button { Text = "Aplicar competência", AutomationId = "AplicarCompetencia" };
        var cancelar = new Button { Text = "Cancelar" };
        var modal = new ContentPage { Title = "Escolher competência", Content = new ScrollView { Content = new VerticalStackLayout
        { Padding = 24, Spacing = 16, Children = { new Label { Text = "Escolher competência", FontSize = 22 }, mes, ano, erro, aplicar, cancelar } } } };
        aplicar.Clicked += async (_, _) =>
        {
            if (!int.TryParse(ano.Text, out var valor) || valor is < 1 or > 9999 || mes.SelectedIndex < 0) { erro.IsVisible = true; return; }
            Texto = $"{mes.SelectedIndex + 1:00}/{valor:0000}";
            await pagina.Navigation.PopModalAsync();
        };
        cancelar.Clicked += async (_, _) => await pagina.Navigation.PopModalAsync();
        await pagina.Navigation.PushModalAsync(modal);
    }
}

internal static class AncestraisMobile
{
    internal static Page? FindParentPage(this Element elemento)
    {
        for (Element? atual = elemento; atual is not null; atual = atual.Parent)
            if (atual is Page pagina) return pagina;
        return null;
    }
}
