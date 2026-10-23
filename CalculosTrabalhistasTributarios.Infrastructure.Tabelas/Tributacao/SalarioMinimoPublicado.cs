using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

public sealed record SalarioMinimoPublicado(DateOnly Competencia, decimal Valor) : ITabelaPublicada<SalarioMinimoPublicado>
{
    public void Validar()
    {
        // Um valor fora dessa faixa indica que a página mudou e outro número foi lido no lugar do salário mínimo.
        if (Valor is < 500m or > 100_000m)
            throw new InvalidOperationException($"A página retornou um salário mínimo fora do esperado ({Valor:N2}).");
    }

    public bool TemMesmosValores(SalarioMinimoPublicado outra) => Valor == outra.Valor;
}
