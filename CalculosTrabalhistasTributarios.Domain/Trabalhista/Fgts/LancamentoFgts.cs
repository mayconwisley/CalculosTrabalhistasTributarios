namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

/// <summary>Remuneração da competência, base do FGTS, e o depósito que consta no extrato ou na guia.</summary>
public sealed record LancamentoFgts(DateOnly Competencia, TipoCompetenciaFgts Tipo, decimal Remuneracao, decimal DepositoInformado);
