using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Regras do seguro-desemprego do trabalhador dispensado sem justa causa (Lei 7.998/1990, com a redação da Lei 13.134/2015).</summary>
public static class RegrasSeguroDesemprego
{
    /// <summary>
    /// Meses com salário exigidos antes da dispensa: 12 nos últimos 18 meses na 1ª solicitação, 9 nos últimos 12 na 2ª e cada
    /// um dos 6 últimos nas seguintes (art. 3º, I).
    /// </summary>
    public static int MesesMinimos(SolicitacaoSeguroDesemprego solicitacao) => solicitacao switch
    {
        SolicitacaoSeguroDesemprego.Primeira => 12,
        SolicitacaoSeguroDesemprego.Segunda => 9,
        _ => 6
    };

    /// <summary>
    /// Parcelas pelos meses trabalhados nos 36 meses anteriores à dispensa (art. 4º, § 2º): 3 de 6 a 11 meses (a partir da 2ª
    /// solicitação), 4 de 12 a 23 meses e 5 com 24 meses ou mais. Sem a carência, nenhuma.
    /// </summary>
    public static int Parcelas(SolicitacaoSeguroDesemprego solicitacao, int mesesTrabalhados)
    {
        if (mesesTrabalhados < MesesMinimos(solicitacao))
            return 0;
        return mesesTrabalhados >= 24 ? 5 : mesesTrabalhados >= 12 ? 4 : 3;
    }

    /// <summary>
    /// Valor de cada parcela pela média dos últimos salários: na faixa, o valor fixo mais o percentual sobre o que passa do
    /// limite da faixa anterior; na última faixa, o valor máximo. Nunca abaixo do salário mínimo (art. 5º, § 2º).
    /// </summary>
    /// <param name="faixas">Limite da média, percentual sobre o excedente (Aliquota) e valor fixo (Deducao) de cada faixa.</param>
    public static Result<ParcelaSeguroDesemprego> ValorDaParcela(decimal media, IReadOnlyList<FaixaTributaria> faixas, decimal salarioMinimo)
    {
        if (faixas.Count == 0)
            return Erro.NaoEncontrado("Não há tabela do seguro-desemprego cadastrada para a data da dispensa.");

        var ordenadas = faixas.OrderBy(item => item.Numero).ToArray();
        var anterior = 0m;
        foreach (var faixa in ordenadas)
        {
            if (media <= faixa.Limite || faixa == ordenadas[^1])
            {
                var pelaTabela = CalculadoraTributacao.Arredondar(faixa.Deducao + (media - anterior) * faixa.Aliquota / 100m);
                return new ParcelaSeguroDesemprego(faixa.Numero, anterior, faixa.Aliquota, faixa.Deducao, pelaTabela, Math.Max(pelaTabela, salarioMinimo));
            }
            anterior = faixa.Limite;
        }
        // Inalcançável: a última faixa sempre devolve a parcela.
        throw new InvalidOperationException("A tabela do seguro-desemprego não tem faixas válidas.");
    }
}
