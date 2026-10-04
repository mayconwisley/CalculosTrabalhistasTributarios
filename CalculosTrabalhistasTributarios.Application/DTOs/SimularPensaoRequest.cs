using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Beneficiarios">Uma pensão por beneficiário, na ordem da decisão; todas são deduzidas da base do IRRF.</param>
/// <param name="OutrosDescontos">Valores retirados dos rendimentos antes do cálculo.</param>
/// <param name="Sucessiva">Cada pensão é calculada depois de descontar as anteriores da base; senão, todas incidem sobre a mesma base.</param>
public sealed record SimularPensaoRequest(DateOnly Competencia, decimal ValorBruto, decimal BaseInss, int Dependentes, IReadOnlyList<BeneficiarioPensao> Beneficiarios, decimal OutrosDescontos, bool Sucessiva = false)
{
    /// <summary>Um só beneficiário.</summary>
    public SimularPensaoRequest(DateOnly competencia, decimal valorBruto, decimal baseInss, int dependentes, RegraPensao pensao, decimal outrosDescontos)
        : this(competencia, valorBruto, baseInss, dependentes, [new BeneficiarioPensao("Beneficiário", pensao)], outrosDescontos)
    {
    }
}
