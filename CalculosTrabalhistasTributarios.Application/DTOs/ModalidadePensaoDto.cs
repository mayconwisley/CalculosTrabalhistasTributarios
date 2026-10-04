namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="DeducoesBase">Deduções da base do IRRF além da pensão: o INSS e os dependentes, ou o desconto simplificado.</param>
/// <param name="RotuloDeducoes">Como as deduções aparecem na memória de cálculo, por exemplo "INSS e dependentes".</param>
/// <param name="DeduzPensao">Falso no desconto simplificado, que substitui todas as deduções legais, inclusive a pensão.</param>
public sealed record ModalidadePensaoDto(string Nome, decimal ImpostoAntesReducao, decimal ReducaoMensal, decimal Imposto, decimal Pensao, decimal Total, int Iteracoes, IReadOnlyList<IteracaoPensaoDto> Detalhes, decimal DeducoesBase, string RotuloDeducoes, bool DeduzPensao);
