using CalculosTrabalhistasTributarios.Mobile.Presentation.Ajuda;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Services;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Ajuda;

/// <summary>Seção do manual da calculadora aberta, como o botão "?" do desktop, com acesso ao manual completo.</summary>
public sealed class AjudaPage : ContentPage
{
    public AjudaPage(TipoCalculadoraMobile tipo, string tituloCalculadora, ManualMobileService manual)
    {
        Title = "Ajuda";
        var conteudo = new VerticalStackLayout { Padding = 20, Spacing = 12, MaximumWidthRequest = 640 };
        var blocos = ManualMobile.NumeroSecao(tipo) is { } numero ? ManualMobile.Secao(numero) : [];
        conteudo.Add(new Label
        {
            Text = "Trecho do manual do aplicativo. Botões, atalhos e janelas citados se referem ao computador; no celular, os campos ficam na mesma ordem da tela.",
            Style = Estilo("TextoSecundario")
        });
        if (blocos.Count == 0)
            conteudo.Add(new Label { Text = $"O manual não tem uma seção própria para {tituloCalculadora}. Abra o manual completo." });
        foreach (var bloco in blocos) conteudo.Add(Criar(bloco));
        var completo = new Button { Text = "Abrir o manual completo (PDF)", Margin = new Thickness(0, 8, 0, 0) };
        completo.Clicked += async (_, _) => await manual.AbrirAsync();
        SemanticProperties.SetHint(completo, "Abre o manual inteiro no leitor de PDF do aparelho");
        conteudo.Add(completo);
        Content = new ScrollView { Content = conteudo };
    }

    private static View Criar(BlocoManualMobile bloco)
    {
        var texto = new Label { FormattedText = Formatar(bloco.Texto) };
        switch (bloco.Tipo)
        {
            case TipoBlocoManualMobile.Titulo:
                texto.Style = Estilo("TituloSecao");
                texto.FontSize = 22;
                SemanticProperties.SetHeadingLevel(texto, SemanticHeadingLevel.Level1);
                return texto;
            case TipoBlocoManualMobile.Subtitulo:
                texto.Style = Estilo("TituloSecao");
                texto.Margin = new Thickness(0, 8, 0, 0);
                SemanticProperties.SetHeadingLevel(texto, SemanticHeadingLevel.Level2);
                return texto;
            case TipoBlocoManualMobile.Item:
                var marcador = new Label { Text = "•", FontSize = 16 };
                var linha = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8 };
                linha.Add(marcador, 0);
                linha.Add(texto, 1);
                return linha;
            case TipoBlocoManualMobile.Destaque:
                return new Border { Style = Estilo("CartaoBase"), Content = texto };
            default:
                return texto;
        }
    }

    // O negrito do manual ("**texto**") vira um trecho em seminegrito.
    private static FormattedString Formatar(string texto)
    {
        var formatado = new FormattedString();
        var partes = texto.Split("**");
        for (var i = 0; i < partes.Length; i++)
            if (partes[i].Length > 0)
                formatado.Spans.Add(new Span { Text = partes[i], FontFamily = i % 2 == 1 ? "OpenSansSemibold" : "OpenSansRegular" });
        return formatado;
    }

    private static Style? Estilo(string chave) =>
        Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(chave, out var estilo) == true ? estilo as Style : null;
}
