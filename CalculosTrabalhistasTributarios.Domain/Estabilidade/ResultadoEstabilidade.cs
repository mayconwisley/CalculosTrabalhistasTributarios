namespace CalculosTrabalhistasTributarios.Domain.Estabilidade;

public sealed record ResultadoEstabilidade(
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
