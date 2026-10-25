namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Referencia">Quantidade que originou o valor, como "15 dias" ou "7/12"; vazio quando não se aplica.</param>
public sealed record VerbaDto(string Descricao, string Referencia, decimal Valor)
{
    /// <summary>
    /// Identifica a linha ao comparar dois cálculos quando a descrição traz um nome digitado, como "1º vínculo" para
    /// "1º vínculo: Empresa A". Sem ela, a comparação junta as linhas pela descrição.
    /// </summary>
    public string? ChaveComparacao { get; init; }
}
