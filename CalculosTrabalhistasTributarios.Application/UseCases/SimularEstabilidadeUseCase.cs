using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Estabilidade;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularEstabilidadeUseCase : ISimularEstabilidadeUseCase
{
    public Result<SimulacaoEstabilidadeDto> Executar(SimularEstabilidadeRequest request) =>
        CalculadoraEstabilidade.Calcular(
            request.MediaRemuneratoria,
            request.DiasBase,
            request.Demissao,
            request.FimEstabilidade,
            request.Complementos)
        .Map(resultado => new SimulacaoEstabilidadeDto(
            resultado.DiasEstabilidade,
            resultado.Meses,
            resultado.DiasAlemDosMeses,
            resultado.AvosDecimoTerceiro,
            resultado.AvosFerias,
            resultado.Indenizacao,
            resultado.DecimoTerceiro,
            resultado.Ferias,
            resultado.TercoFerias,
            resultado.FgtsOitoPorCento,
            resultado.MultaFgtsQuarentaPorCento,
            resultado.Complementos,
            resultado.Total));
}
