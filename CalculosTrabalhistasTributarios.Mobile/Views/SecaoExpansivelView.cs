namespace CalculosTrabalhistasTributarios.Mobile.Views;

/// <summary>Cria controles apenas enquanto a seção está aberta; recolher libera a árvore visual.</summary>
public sealed class SecaoExpansivelView : ContentView
{
    public static readonly BindableProperty TituloProperty = BindableProperty.Create(nameof(Titulo), typeof(string), typeof(SecaoExpansivelView), "", propertyChanged: (b, _, _) => ((SecaoExpansivelView)b).Atualizar());
    public static readonly BindableProperty ModeloProperty = BindableProperty.Create(nameof(Modelo), typeof(DataTemplate), typeof(SecaoExpansivelView), null, propertyChanged: (b, _, _) => ((SecaoExpansivelView)b).Atualizar());
    public static readonly BindableProperty ExpandidaProperty = BindableProperty.Create(nameof(Expandida), typeof(bool), typeof(SecaoExpansivelView), false, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((SecaoExpansivelView)b).Atualizar());
    public static readonly BindableProperty DadosProperty = BindableProperty.Create(nameof(Dados), typeof(object), typeof(SecaoExpansivelView), null, propertyChanged: (b, _, _) => ((SecaoExpansivelView)b).Reiniciar());
    private readonly Button _cabecalho = new();
    private readonly ContentView _conteudo = new();
    public bool AbertaInicialmente { get; set; }

    public SecaoExpansivelView()
    {
        _cabecalho.Clicked += (_, _) => Expandida = !Expandida;
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("BotaoSecundario", out var estilo) == true)
            _cabecalho.Style = (Style)estilo;
        Content = new VerticalStackLayout { Spacing = 10, Children = { _cabecalho, _conteudo } };
    }
    public string Titulo { get => (string)GetValue(TituloProperty); set => SetValue(TituloProperty, value); }
    public DataTemplate? Modelo { get => (DataTemplate?)GetValue(ModeloProperty); set => SetValue(ModeloProperty, value); }
    public bool Expandida { get => (bool)GetValue(ExpandidaProperty); set => SetValue(ExpandidaProperty, value); }
    public object? Dados { get => GetValue(DadosProperty); set => SetValue(DadosProperty, value); }
    private void Reiniciar()
    {
        _conteudo.Content = null;
        Expandida = AbertaInicialmente;
        Atualizar();
    }
    private void Atualizar()
    {
        _cabecalho.Text = (Expandida ? "▾  " : "▸  ") + Titulo;
        SemanticProperties.SetDescription(_cabecalho, (Expandida ? "Recolher " : "Expandir ") + Titulo);
        if (!Expandida) _conteudo.Content = null;
        else if (_conteudo.Content is null && Modelo?.CreateContent() is View view) _conteudo.Content = view;
    }
}
