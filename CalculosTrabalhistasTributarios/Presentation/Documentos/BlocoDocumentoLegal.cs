namespace CalculosTrabalhistasTributarios.Presentation.Documentos;

/// <summary>Trecho do documento pronto para exibir; o texto mantém o negrito em **asteriscos** e já vem sem links nem código.</summary>
public sealed record BlocoDocumentoLegal(TipoBlocoDocumentoLegal Tipo, string Texto)
{
    public IReadOnlyList<TrechoDocumentoLegal> Trechos { get; } = Separar(Texto);

    public bool EhItem => Tipo == TipoBlocoDocumentoLegal.Item;

    private static TrechoDocumentoLegal[] Separar(string texto) =>
    [
        .. texto.Split("**").Select((parte, indice) => new TrechoDocumentoLegal(parte, indice % 2 == 1)).Where(trecho => trecho.Texto.Length > 0)
    ];
}
