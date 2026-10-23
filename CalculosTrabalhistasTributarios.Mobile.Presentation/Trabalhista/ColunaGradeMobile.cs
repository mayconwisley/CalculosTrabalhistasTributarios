namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Coluna de uma grade: cada linha nova recebe um campo com este rótulo, tipo e valor inicial.</summary>
public sealed record ColunaGradeMobile(string Chave, string Rotulo, TipoCampoMobile Tipo, string Valor = "", string Dica = "",
    IReadOnlyList<string>? Opcoes = null);
