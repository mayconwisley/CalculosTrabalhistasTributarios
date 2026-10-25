using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Historico;

public partial class ComparacaoPage : ContentPage
{
    private readonly Func<Task>? _compartilharPdf;

    /// <param name="compartilharPdf">Gera o PDF da comparação; nulo quando não há linhas comparadas.</param>
    public ComparacaoPage(string primeiro, string segundo, IReadOnlyList<LinhaResultadoComparadoViewModel> linhas, string? aviso,
        Func<Task>? compartilharPdf = null)
    {
        InitializeComponent();
        _compartilharPdf = compartilharPdf;
        CompartilharPdf.IsVisible = compartilharPdf is not null;
        Cabecalho.Text = $"{primeiro}  →  {segundo}";
        Aviso.Text = aviso;
        Aviso.IsVisible = !string.IsNullOrWhiteSpace(aviso);
        foreach (var linha in linhas)
        {
            var coluna = new VerticalStackLayout { Spacing = 4 };
            coluna.Add(new Label { Text = $"{linha.Grupo} · {linha.Item}", FontAttributes = FontAttributes.Bold });
            coluna.Add(new Label { Text = $"{primeiro}: {linha.Primeiro}" });
            coluna.Add(new Label { Text = $"{segundo}: {linha.Segundo}" });
            if (!string.IsNullOrEmpty(linha.Diferenca)) coluna.Add(new Label { Text = $"Diferença: {linha.Diferenca}" });
            Linhas.Add(new Border { Content = coluna, Padding = 12 });
        }
    }

    private async void AoCompartilharPdf(object? sender, EventArgs e)
    {
        if (_compartilharPdf is null) return;
        CompartilharPdf.IsEnabled = false;
        try { await _compartilharPdf(); }
        finally { CompartilharPdf.IsEnabled = true; }
    }
}
