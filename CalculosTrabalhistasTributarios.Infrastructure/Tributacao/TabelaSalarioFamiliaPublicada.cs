using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Até 10/2019 eram duas faixas de remuneração, com cotas diferentes; depois, uma só.</summary>
public sealed record TabelaSalarioFamiliaPublicada(DateOnly Competencia, IReadOnlyList<FaixaSalarioFamiliaPublicada> Faixas) : ITabelaPublicada<TabelaSalarioFamiliaPublicada>
{
    public void Validar()
    {
        if (Faixas.Count is < 1 or > 2 ||
            Faixas.Any(faixa => faixa.LimiteRemuneracao <= 0m || faixa.Cota <= 0m) ||
            !Faixas.Select(faixa => faixa.LimiteRemuneracao).EstritamenteCrescente())
            throw new InvalidOperationException("A página retornou uma estrutura de salário-família inválida.");
    }

    public bool TemMesmosValores(TabelaSalarioFamiliaPublicada outra) => Faixas.SequenceEqual(outra.Faixas);
}
