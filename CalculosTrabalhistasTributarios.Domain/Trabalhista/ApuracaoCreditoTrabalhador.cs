namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record ApuracaoCreditoTrabalhador(decimal MargemTotal, decimal MargemLivre, decimal ValorCredito,
    decimal PrincipalFinanciado, decimal CreditoLiquido, decimal Parcela, decimal UltimaParcela,
    decimal TotalPago, decimal TotalJuros, decimal CustoTotal, decimal JurosAnuais,
    decimal? CustoEfetivoMensalEstimado, bool CabeNaMargem,
    decimal IofFinanciado, decimal BaseDiasIof);
