using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ValorBruto">Rendimento tributável do IRRF.</param>
/// <param name="BaseInss">Remuneração sobre a qual incidem o INSS, limitado ao teto, e o FGTS, sem teto.</param>
/// <param name="Vinculo">Define a alíquota do FGTS: 8%, 2% no aprendiz e 8% mais 3,2% no doméstico.</param>
public sealed record SimularImpostoRequest(
    DateOnly Competencia,
    decimal ValorBruto,
    decimal BaseInss,
    int QuantidadeDependentes,
    CategoriaFgts Vinculo = CategoriaFgts.Empregado);
