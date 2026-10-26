namespace CalculosTrabalhistasTributarios.Presentation.Documentos;

/// <summary>Parte do texto de um bloco, em negrito quando estava entre "**" no Markdown.</summary>
public sealed record TrechoDocumentoLegal(string Texto, bool Negrito);
