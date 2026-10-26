using CalculosTrabalhistasTributarios.Presentation.Documentos;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Configuracoes;

/// <summary>
/// Termos, licença, responsabilidade ou privacidade, com o mesmo texto do desktop (docs/legal). A lista é virtualizada: a licença,
/// com os avisos de terceiros e o texto da Apache 2.0, passa de cem blocos.
/// </summary>
public sealed class DocumentoLegalPage : ContentPage
{
    public DocumentoLegalPage(TipoDocumentoLegal tipo)
    {
        var documento = DocumentosLegais.Documento(tipo);
        Title = documento.Titulo;
        var titulo = new Label { Text = documento.Titulo, Style = Estilo("TituloPagina") };
        SemanticProperties.SetHeadingLevel(titulo, SemanticHeadingLevel.Level1);
        Content = new CollectionView
        {
            AutomationId = "DocumentoLegal",
            SelectionMode = SelectionMode.None,
            Margin = new Thickness(20, 0),
            Header = new VerticalStackLayout
            {
                Padding = new Thickness(0, 18, 0, 8),
                Spacing = 6,
                Children = { titulo, new Label { Text = documento.Descricao, Style = Estilo("TextoSecundario") } }
            },
            Footer = new BoxView { HeightRequest = 28, Color = Colors.Transparent },
            ItemTemplate = new DataTemplate(() => new LinhaDocumento()),
            ItemsSource = documento.Blocos
        };
    }

    private static Style? Estilo(string chave) =>
        Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(chave, out var estilo) == true ? estilo as Style : null;

    /// <summary>Um bloco do documento; o texto é montado na troca do contexto, sem bindings por reflexão.</summary>
    private sealed class LinhaDocumento : Grid
    {
        private readonly Label _marcador = new() { Text = "•", FontSize = 16 };
        private readonly Label _texto = new();

        public LinhaDocumento()
        {
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)];
            ColumnSpacing = 8;
            this.Add(_marcador, 0);
            this.Add(_texto, 1);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not BlocoDocumentoLegal bloco) return;
            _marcador.IsVisible = bloco.EhItem;
            Grid.SetColumn(_texto, bloco.EhItem ? 1 : 0);
            Grid.SetColumnSpan(_texto, bloco.EhItem ? 1 : 2);
            _texto.Style = Estilo(bloco.Tipo switch
            {
                TipoBlocoDocumentoLegal.Secao or TipoBlocoDocumentoLegal.Subsecao => "TituloSecao",
                TipoBlocoDocumentoLegal.TextoLicenca => "TextoSecundario",
                _ => "RotuloBase"
            });
            _texto.FontSize = bloco.Tipo switch
            {
                TipoBlocoDocumentoLegal.Secao => 18,
                TipoBlocoDocumentoLegal.Subsecao => 16,
                TipoBlocoDocumentoLegal.TextoLicenca => 12,
                _ => 14
            };
            Padding = bloco.Tipo switch
            {
                TipoBlocoDocumentoLegal.Secao => new Thickness(0, 16, 0, 6),
                TipoBlocoDocumentoLegal.Subsecao => new Thickness(0, 10, 0, 4),
                _ => new Thickness(0, 0, 0, 10)
            };
            SemanticProperties.SetHeadingLevel(_texto, bloco.Tipo switch
            {
                TipoBlocoDocumentoLegal.Secao => SemanticHeadingLevel.Level2,
                TipoBlocoDocumentoLegal.Subsecao => SemanticHeadingLevel.Level3,
                _ => SemanticHeadingLevel.None
            });
            var formatado = new FormattedString();
            var negrito = bloco.Tipo is TipoBlocoDocumentoLegal.Secao or TipoBlocoDocumentoLegal.Subsecao;
            foreach (var trecho in bloco.Trechos)
                formatado.Spans.Add(new Span { Text = trecho.Texto, FontFamily = negrito || trecho.Negrito ? "OpenSansSemibold" : "OpenSansRegular" });
            _texto.FormattedText = formatado;
        }
    }
}
