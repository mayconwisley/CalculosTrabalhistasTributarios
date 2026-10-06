using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.IO;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

/// <summary>Cores, página padrão, seções e células comuns a todos os relatórios em PDF.</summary>
internal static class ComponentesPdf
{
    internal static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    internal const string AzulPrimario = "1D4ED8";

    internal const string AzulClaro = "EFF6FF";

    internal const string CinzaBorda = "D1D5DB";

    internal const string CinzaTexto = "4B5563";

    internal const string VerdeVantagem = "027A48";

    internal const string VerdeClaroVantagem = "ECFDF3";

    internal const string VerdeBordaVantagem = "86EFAC";

    internal static Task GerarAsync(string caminhoArquivo, CancellationToken cancellationToken, Action<IDocumentContainer> criarDocumento)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diretorio = Path.GetDirectoryName(caminhoArquivo);
        if (string.IsNullOrWhiteSpace(diretorio))
            throw new ArgumentException("Informe um caminho válido para o arquivo PDF.", nameof(caminhoArquivo));

        Directory.CreateDirectory(diretorio);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Definida aqui, e não na abertura do app, para que o QuestPDF e sua biblioteca nativa só sejam carregados ao gerar um PDF.
            QuestPDF.Settings.License = LicenseType.Community;
            Document.Create(criarDocumento).GeneratePdf(caminhoArquivo);
        }, cancellationToken);
    }

    internal static void ConfigurarPagina(PageDescriptor pagina, string titulo, DateOnly competencia)
        => ConfigurarPagina(pagina, titulo, $"Competência: {competencia:MM/yyyy}  |  Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");

    internal static void ConfigurarPagina(PageDescriptor pagina, string titulo, string subtitulo)
    {
        pagina.Size(PageSizes.A4);
        pagina.Margin(36);
        pagina.DefaultTextStyle(estilo => estilo.FontFamily(Fonts.Arial).FontSize(9).FontColor(CinzaTexto));
        pagina.Header().Column(cabecalho =>
        {
            cabecalho.Spacing(4);
            cabecalho.Item().Text(titulo).FontSize(20).SemiBold().FontColor(AzulPrimario);
            cabecalho.Item().Text(subtitulo).FontSize(9).FontColor(CinzaTexto);
            cabecalho.Item().PaddingTop(8).LineHorizontal(1).LineColor(AzulPrimario);
        });
        pagina.Footer().PaddingTop(12).Column(rodape =>
        {
            rodape.Spacing(8);
            rodape.Item().Background(AzulClaro).Border(1).BorderColor(CinzaBorda).Padding(8).Column(aviso =>
            {
                aviso.Spacing(3);
                aviso.Item().Text("CÁLCULO SIMULADO - VALORES ESTIMADOS").SemiBold().FontColor(AzulPrimario);
                aviso.Item().Text("Este documento apresenta uma simulação. Procure um profissional especializado para apurar e validar os valores aplicáveis ao seu caso antes de utilizá-los.");
            });
            rodape.Item().Row(informacoes =>
            {
                informacoes.RelativeItem().Text("Cálculos Trabalhistas e Tributários - dados processados localmente").FontSize(8).FontColor(CinzaTexto);
                informacoes.AutoItem().Text(texto =>
                {
                    texto.Span("Página ").FontSize(8).FontColor(CinzaTexto);
                    texto.CurrentPageNumber().FontSize(8).FontColor(CinzaTexto);
                    texto.Span(" de ").FontSize(8).FontColor(CinzaTexto);
                    texto.TotalPages().FontSize(8).FontColor(CinzaTexto);
                });
            });
        });
    }

    internal static void AdicionarFormulaIrrf(ColumnDescriptor coluna, string titulo, string formula)
    {
        coluna.Item().Text(titulo).FontSize(8).SemiBold().FontColor(AzulPrimario);
        coluna.Item().Text(formula).FontFamily("Courier New").FontSize(8);
    }

    internal static void CriarSecao(IContainer container, string titulo, Action<IContainer> criarConteudo, string? destaque = null) => container.Column(coluna =>
    {
        coluna.Spacing(8);
        coluna.Item().Row(cabecalho =>
        {
            cabecalho.RelativeItem().Text(titulo).FontSize(12).SemiBold().FontColor(AzulPrimario);
            if (!string.IsNullOrWhiteSpace(destaque))
                cabecalho.AutoItem().Background(AzulClaro).PaddingHorizontal(8).PaddingVertical(4).Text(destaque).FontSize(8).SemiBold().FontColor(AzulPrimario);
        });
        coluna.Item().LineHorizontal(1).LineColor(CinzaBorda);
        coluna.Item().Element(criarConteudo);
    });

    internal static void CabecalhoTabela(IContainer container, string texto) => container.Background(AzulPrimario).Padding(6).Text(texto).FontColor(Colors.White).SemiBold();

    internal static void CelulaTabela(IContainer container, string texto, bool ehMaisVantajosa = false)
    {
        var celula = container.BorderBottom(1).BorderColor(ehMaisVantajosa ? VerdeBordaVantagem : CinzaBorda).Padding(6);
        if (ehMaisVantajosa)
            celula = celula.Background(VerdeClaroVantagem);

        var textoCelula = celula.Text(texto).FontColor(ehMaisVantajosa ? VerdeVantagem : CinzaTexto);
        if (ehMaisVantajosa)
            textoCelula.SemiBold();
    }

    internal static void CelulaRotulo(IContainer container, string texto) => container.Background(AzulClaro).Padding(6).Text(texto).SemiBold();

    internal static void CelulaValor(IContainer container, string texto) => container.BorderBottom(1).BorderColor(CinzaBorda).Padding(6).Text(texto);

    internal static string Moeda(decimal valor) => valor.ToString("C2", CulturaPtBr);

    internal static string Percentual(decimal valor) => valor.ToString("N2", CulturaPtBr) + "%";
}
