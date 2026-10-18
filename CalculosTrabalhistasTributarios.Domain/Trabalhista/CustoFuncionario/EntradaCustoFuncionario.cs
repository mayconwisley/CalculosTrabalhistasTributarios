using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;

public sealed record EntradaCustoFuncionario(decimal Salario, RegimeTributario Regime, decimal Rat, decimal Fap,
    decimal Terceiros, decimal Beneficios, bool IncluirProvisoes, bool Aprendiz = false,
    decimal HorasMensais = 220m, IReadOnlyList<BeneficioFuncionario>? BeneficiosDetalhados = null);
