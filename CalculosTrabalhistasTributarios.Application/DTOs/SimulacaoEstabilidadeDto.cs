namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Meses">Meses cheios do período de estabilidade.</param>
/// <param name="DiasAlemDosMeses">Dias que sobram depois dos meses cheios.</param>
public sealed record SimulacaoEstabilidadeDto(
    int DiasEstabilidade,
    int Meses,
    int DiasAlemDosMeses,
    int AvosDecimoTerceiro,
    int AvosFerias,
    decimal Indenizacao,
    decimal DecimoTerceiro,
    decimal Ferias,
    decimal TercoFerias,
    decimal FgtsOitoPorCento,
    decimal MultaFgtsQuarentaPorCento,
    decimal Complementos,
    decimal Total);
