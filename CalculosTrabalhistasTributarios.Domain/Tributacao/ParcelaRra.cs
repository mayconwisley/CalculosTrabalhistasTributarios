namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Valor tributável recebido referente a uma competência; no 13º, vale o ano da competência.</summary>
public sealed record ParcelaRra(DateOnly Competencia, TipoParcelaRra Tipo, decimal Valor);
