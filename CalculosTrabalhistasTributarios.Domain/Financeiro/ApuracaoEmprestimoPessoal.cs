namespace CalculosTrabalhistasTributarios.Domain.Financeiro;

public sealed record ApuracaoEmprestimoPessoal(
    decimal PrincipalFinanciado, decimal CreditoLiquido, decimal Iof, decimal BaseDiasIof,
    decimal Parcela, decimal UltimaParcela, decimal TotalPago, decimal TotalJuros,
    decimal CustoTotal, decimal JurosAnuais, decimal? CustoEfetivoMensal,
    decimal? CustoEfetivoAnual, DateOnly PrimeiroVencimento, DateOnly UltimoVencimento);
