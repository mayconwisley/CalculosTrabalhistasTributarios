using CalculosTrabalhistasTributarios.Presentation.Documentos;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

/// <summary>O Android compila o leitor do desktop e precisa embutir os mesmos arquivos de docs/legal.</summary>
public sealed class DocumentosLegaisMobileTests
{
    [Fact]
    public void Todos_os_documentos_estao_embutidos_na_apresentacao_android()
    {
        Assert.Equal("CalculosTrabalhistasTributarios.Mobile.Presentation", typeof(DocumentosLegais).Assembly.GetName().Name);
        foreach (var tipo in Enum.GetValues<TipoDocumentoLegal>())
            Assert.Contains(DocumentosLegais.Blocos(tipo), bloco => bloco.Tipo == TipoBlocoDocumentoLegal.Secao);
        // A licença inclui LICENSE e THIRD-PARTY-NOTICES.md, também embutidos.
        var licenca = DocumentosLegais.Blocos(TipoDocumentoLegal.LicencaDeUso);
        Assert.Contains(licenca, bloco => bloco.Texto == "Copyright (c) 2026 Maycon Wisley");
        Assert.Contains(licenca, bloco => bloco.Texto == "Avisos de terceiros");
    }
}
