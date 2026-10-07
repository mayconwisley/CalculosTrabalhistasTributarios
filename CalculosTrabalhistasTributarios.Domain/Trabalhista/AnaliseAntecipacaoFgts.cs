namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record AnaliseAntecipacaoFgts(string Situacao, DateOnly ProximoAniversario, decimal SaldoForaGarantia,
    int LimiteSaquesAnuais, IReadOnlyList<string> Motivos);
