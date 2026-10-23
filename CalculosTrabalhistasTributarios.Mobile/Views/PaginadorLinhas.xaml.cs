namespace CalculosTrabalhistasTributarios.Mobile.Views;

/// <summary>Navega entre as páginas de linhas de uma grade.</summary>
public partial class PaginadorLinhas : ContentView
{
    /// <summary>Elemento levado ao topo da tela depois de trocar a página, para o usuário começar pela primeira linha exibida.</summary>
    public static readonly BindableProperty RolarAteProperty = BindableProperty.Create(nameof(RolarAte), typeof(VisualElement), typeof(PaginadorLinhas));

    public PaginadorLinhas() => InitializeComponent();

    public VisualElement? RolarAte { get => (VisualElement?)GetValue(RolarAteProperty); set => SetValue(RolarAteProperty, value); }

    private void AoNavegar(object? sender, EventArgs e)
    {
        if (RolarAte is not { } alvo) return;
        Element? ancestral = Parent;
        while (ancestral is not null and not ScrollView) ancestral = ancestral.Parent;
        if (ancestral is ScrollView rolagem)
            // Aguarda o leiaute das linhas novas antes de rolar.
            Dispatcher.Dispatch(() => _ = rolagem.ScrollToAsync(alvo, ScrollToPosition.Start, false));
    }
}
