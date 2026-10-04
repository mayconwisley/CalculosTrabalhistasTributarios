using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

public sealed record TabelaInssPublicada(DateOnly Competencia, IReadOnlyList<FaixaInssPublicada> Faixas) : ITabelaPublicada<TabelaInssPublicada>
{
    public void Validar()
    {
        if (Faixas.Count != 4 ||
            Faixas[0].Limite <= 0m ||
            Faixas[0].Aliquota <= 0m ||
            Faixas[^1].Aliquota >= 100m ||
            !Faixas.Select(faixa => faixa.Limite).EstritamenteCrescente() ||
            !Faixas.Select(faixa => faixa.Aliquota).EstritamenteCrescente())
            throw new InvalidOperationException("A página retornou uma estrutura de faixas INSS inválida.");
    }

    public bool TemMesmosValores(TabelaInssPublicada outra) => Faixas.SequenceEqual(outra.Faixas);
}
