namespace CalculosTrabalhistasTributarios.Presentation.Documentos;

/// <param name="Arquivo">Arquivo de docs/legal, embutido no assembly.</param>
public sealed record DocumentoLegal(TipoDocumentoLegal Tipo, string Titulo, string Descricao, string Arquivo)
{
    /// <summary>Lidos na primeira abertura do documento e guardados para as seguintes.</summary>
    public IReadOnlyList<BlocoDocumentoLegal> Blocos => DocumentosLegais.Blocos(Tipo);
}
