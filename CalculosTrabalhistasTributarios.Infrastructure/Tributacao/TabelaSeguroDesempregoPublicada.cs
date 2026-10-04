using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Tabela anual do seguro-desemprego: 80% da média, o valor fixo mais 50% do excedente e, acima disso, o valor máximo.</summary>
public sealed record TabelaSeguroDesempregoPublicada(DateOnly Competencia, IReadOnlyList<FaixaSeguroDesempregoPublicada> Faixas) : ITabelaPublicada<TabelaSeguroDesempregoPublicada>
{
    // Os valores da tabela são arredondados em centavos, e cada um é calculado a partir dos anteriores.
    private const decimal Tolerancia = 0.02m;

    public void Validar()
    {
        if (Faixas.Count != 3 ||
            Faixas[0].LimiteMedia <= 0m || Faixas[0].Percentual is <= 0m or > 100m || Faixas[0].ValorFixo != 0m ||
            Faixas[1].Percentual is <= 0m or > 100m || Faixas[1].ValorFixo <= 0m ||
            Faixas[2].LimiteMedia != TabelaIrrfPublicada.LimiteUltimaFaixa || Faixas[2].Percentual != 0m || Faixas[2].ValorFixo <= Faixas[1].ValorFixo ||
            !Faixas.Select(faixa => faixa.LimiteMedia).EstritamenteCrescente())
            throw new InvalidOperationException("A página retornou uma estrutura de seguro-desemprego inválida.");

        // O valor fixo da 2ª faixa é a parcela no limite da 1ª, e o valor máximo, a parcela no limite da 2ª: um número
        // trocado na página, como o valor máximo de outro ano, não fecha a conta.
        var parcelaNoLimiteDaPrimeira = Faixas[0].LimiteMedia * Faixas[0].Percentual / 100m;
        var parcelaNoLimiteDaSegunda = Faixas[1].ValorFixo + (Faixas[1].LimiteMedia - Faixas[0].LimiteMedia) * Faixas[1].Percentual / 100m;
        if (Math.Abs(Faixas[1].ValorFixo - parcelaNoLimiteDaPrimeira) > Tolerancia || Math.Abs(Faixas[2].ValorFixo - parcelaNoLimiteDaSegunda) > Tolerancia)
            throw new InvalidOperationException("Os valores da tabela de seguro-desemprego da página não fecham entre si.");
    }

    public bool TemMesmosValores(TabelaSeguroDesempregoPublicada outra) => Faixas.SequenceEqual(outra.Faixas);
}
