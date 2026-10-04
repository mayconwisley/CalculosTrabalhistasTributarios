using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.Mapeamentos;

/// <summary>Converte a apuração do domínio nos DTOs que as telas e os relatórios exibem.</summary>
internal static class MapeamentoTributario
{
    public static ModalidadeIrrfDto ParaDto(this ModalidadeIrrf modalidade) => new(
        modalidade.Nome,
        modalidade.BaseCalculo,
        modalidade.Aliquota,
        modalidade.Deducao,
        modalidade.ImpostoAntesReducao,
        modalidade.ReducaoMensal,
        modalidade.Imposto,
        modalidade.AliquotaEfetiva,
        modalidade.DetalhesProgressivos.ParaDto());

    public static IReadOnlyList<DetalheFaixaDto> ParaDto(this IReadOnlyList<ResultadoFaixaTributaria> faixas) =>
        faixas.Select(faixa => new DetalheFaixaDto(faixa.Faixa, faixa.BaseCalculada, faixa.Aliquota, faixa.Imposto)).ToArray();
}
