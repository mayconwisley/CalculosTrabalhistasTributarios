namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="BasePensao">Base da pensão do primeiro beneficiário; a de cada um está em <paramref name="Beneficiarios"/>.</param>
/// <param name="Pensao">Soma das pensões de todos os beneficiários.</param>
/// <param name="PensaoDeduzida">Pensão da iteração anterior, deduzida da base do IRRF; zero no desconto simplificado.</param>
public sealed record IteracaoPensaoDto(int Sequencia, decimal BaseIrrf, decimal Aliquota, decimal Deducao, decimal ImpostoAntesReducao, decimal ReducaoMensal, decimal Imposto, decimal BasePensao, decimal Pensao, decimal PensaoDeduzida, IReadOnlyList<PensaoBeneficiarioDto> Beneficiarios);
