using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Rat">Alíquota do risco ambiental do trabalho (RAT), de 1% a 3%, antes do FAP.</param>
/// <param name="Fap">Fator acidentário de prevenção, de 0,5 a 2.</param>
/// <param name="Terceiros">Contribuições a terceiros (Sistema S, salário-educação, Incra), normalmente 5,8%.</param>
/// <param name="Beneficios">Custo mensal da empresa com benefícios, já descontada a parte do empregado.</param>
/// <param name="Aprendiz">Jovem aprendiz, com FGTS de 2% (Lei 8.036/1990, art. 15, § 7º).</param>
public sealed record SimularCustoFuncionarioRequest(
    decimal Salario,
    RegimeTributario Regime,
    decimal Rat,
    decimal Fap,
    decimal Terceiros,
    decimal Beneficios,
    bool IncluirProvisoes,
    bool Aprendiz = false);
