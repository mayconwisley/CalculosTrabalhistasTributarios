using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Presentation.Documentos;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Termos, licença, responsabilidade e privacidade lidos de docs/legal, embutidos no aplicativo.</summary>
public class DocumentosLegaisTests
{
    public static TheoryData<TipoDocumentoLegal> Tipos => [.. Enum.GetValues<TipoDocumentoLegal>()];

    [Theory]
    [MemberData(nameof(Tipos))]
    public void Documento_e_lido_sem_marcacao_do_markdown(TipoDocumentoLegal tipo)
    {
        var documento = DocumentosLegais.Documento(tipo);
        var blocos = documento.Blocos;

        Assert.NotEmpty(blocos);
        // O título do arquivo fica no cabeçalho da tela, não no texto.
        Assert.DoesNotContain(blocos, bloco => bloco.Texto == documento.Titulo);
        Assert.Contains(blocos, bloco => bloco.Tipo == TipoBlocoDocumentoLegal.Secao);
        foreach (var bloco in blocos)
        {
            Assert.False(string.IsNullOrWhiteSpace(bloco.Texto));
            Assert.DoesNotContain("<!--", bloco.Texto);
            Assert.DoesNotContain("```", bloco.Texto);
            Assert.DoesNotContain("](", bloco.Texto);
            Assert.DoesNotContain("`", bloco.Texto);
            Assert.False(bloco.Texto.StartsWith('#') || bloco.Texto.StartsWith('|') || bloco.Texto.StartsWith("- ", StringComparison.Ordinal), bloco.Texto);
            Assert.All(bloco.Trechos, trecho => Assert.DoesNotContain("**", trecho.Texto));
        }
        Assert.Same(blocos, documento.Blocos);
    }

    [Fact]
    public void Licenca_inclui_o_texto_mit_e_os_avisos_de_terceiros()
    {
        var blocos = DocumentosLegais.Blocos(TipoDocumentoLegal.LicencaDeUso);
        var licencas = blocos.Where(bloco => bloco.Tipo == TipoBlocoDocumentoLegal.TextoLicenca).Select(bloco => bloco.Texto).ToArray();

        // LICENSE: o aviso de copyright e a permissão, com as linhas do arquivo unidas num parágrafo.
        Assert.Contains("Copyright (c) 2026 Maycon Wisley", licencas);
        Assert.Contains(licencas, texto => texto.StartsWith("Permission is hereby granted, free of charge, to any person obtaining a copy of this software", StringComparison.Ordinal));
        Assert.Contains(licencas, texto => texto.StartsWith("THE SOFTWARE IS PROVIDED \"AS IS\"", StringComparison.Ordinal));
        // THIRD-PARTY-NOTICES.md: o "#" do arquivo incluído vira uma seção, e a tabela, uma linha por componente.
        Assert.Contains(blocos, bloco => bloco.Tipo == TipoBlocoDocumentoLegal.Secao && bloco.Texto == "Avisos de terceiros");
        Assert.Contains(blocos, bloco => bloco.Tipo == TipoBlocoDocumentoLegal.Subsecao && bloco.Texto == "Apache License 2.0");
        var questPdf = Assert.Single(blocos, bloco => bloco.EhItem && bloco.Texto.StartsWith("**QuestPDF**:", StringComparison.Ordinal));
        Assert.Contains("QuestPDF Community MIT License", questPdf.Texto);
        Assert.Contains("https://github.com/QuestPDF/QuestPDF", questPdf.Texto);
        Assert.Equal(new TrechoDocumentoLegal("QuestPDF", true), questPdf.Trechos[0]);
        Assert.DoesNotContain(blocos, bloco => bloco.Texto.StartsWith("**Componente**", StringComparison.Ordinal));
    }

    [Fact]
    public void Responsabilidade_repete_o_aviso_dos_pdfs()
    {
        var texto = string.Join('\n', DocumentosLegais.Blocos(TipoDocumentoLegal.ResponsabilidadeDeUso).Select(bloco => bloco.Texto.Replace("**", "")));

        Assert.Contains(AvisoSimulacao.Titulo, texto, StringComparison.CurrentCultureIgnoreCase);
        Assert.Contains("Procure um profissional especializado", texto);
    }

    [Fact]
    public void Politica_de_privacidade_cita_os_dados_locais_e_as_consultas()
    {
        var texto = string.Join('\n', DocumentosLegais.Blocos(TipoDocumentoLegal.PoliticaDePrivacidade).Select(bloco => bloco.Texto));

        Assert.Contains(@"%LOCALAPPDATA%\CalculoIRRF\settings.json", texto);
        Assert.Contains(@"%LOCALAPPDATA%\CalculoIRRF\logs", texto);
        Assert.Contains(@"BancoDados\calculoIrrf.db", texto);
        Assert.Contains("GitHub", texto);
        Assert.Contains("não coleta dados pessoais", texto);
    }
}
