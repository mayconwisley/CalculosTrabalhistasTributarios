using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Limite da dispensa de retenção do IRRF na redação em vigor da lei.</summary>
/// <param name="Competencia">Início da vigência da redação em vigor.</param>
/// <param name="Norma">Dispositivo que fixa o valor, como "art. 67 da Lei 9.430/1996".</param>
public sealed record DescontoMinimoPublicado(DateOnly Competencia, decimal Valor, string Norma) : ITabelaPublicada<DescontoMinimoPublicado>
{
    public void Validar()
    {
        // Um valor fora dessa faixa indica que a página mudou e outro número foi lido no lugar do limite.
        if (Valor is <= 0m or > 1_000m)
            throw new InvalidOperationException($"A página retornou um desconto mínimo fora do esperado ({Valor:N2}).");
    }

    public bool TemMesmosValores(DescontoMinimoPublicado outra) => Valor == outra.Valor;
}
