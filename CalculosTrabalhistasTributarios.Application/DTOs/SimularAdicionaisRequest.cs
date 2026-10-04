using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ValorBaseInformado">Piso da categoria ou outra base prevista em convenção; usado com <see cref="BaseInsalubridade.ValorInformado"/>.</param>
public sealed record SimularAdicionaisRequest(
    DateOnly Competencia,
    decimal Salario,
    GrauInsalubridade Grau,
    BaseInsalubridade Base,
    decimal ValorBaseInformado,
    bool Periculosidade,
    int Dependentes);
