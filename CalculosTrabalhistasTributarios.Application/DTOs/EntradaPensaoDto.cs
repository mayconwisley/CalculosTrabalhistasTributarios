using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Sucessiva">Cada pensão foi calculada depois de descontar as anteriores da base.</param>
public sealed record EntradaPensaoDto(DateOnly Competencia, decimal ValorBruto, decimal BaseInss, int Dependentes, IReadOnlyList<BeneficiarioPensao> Beneficiarios, decimal OutrosDescontos, bool Sucessiva = false)
{
    /// <summary>Um só beneficiário.</summary>
    public EntradaPensaoDto(DateOnly competencia, decimal valorBruto, decimal baseInss, int dependentes, RegraPensao pensao, decimal outrosDescontos)
        : this(competencia, valorBruto, baseInss, dependentes, [new BeneficiarioPensao("Beneficiário", pensao)], outrosDescontos)
    {
    }
}
