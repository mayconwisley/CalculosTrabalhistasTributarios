namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Referencia">Quantidade que originou o valor, como "15 dias" ou "7/12"; vazio quando não se aplica.</param>
public sealed record VerbaDto(string Descricao, string Referencia, decimal Valor);
