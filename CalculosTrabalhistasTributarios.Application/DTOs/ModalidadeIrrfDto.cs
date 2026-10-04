namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record ModalidadeIrrfDto(
    string Nome,
    decimal BaseCalculo,
    decimal Aliquota,
    decimal Deducao,
    decimal ImpostoAntesReducao,
    decimal ReducaoMensal,
    decimal Imposto,
    decimal AliquotaEfetiva,
    IReadOnlyList<DetalheFaixaDto> DetalhesProgressivos);
