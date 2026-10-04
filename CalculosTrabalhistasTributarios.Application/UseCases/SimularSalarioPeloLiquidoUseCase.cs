using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Encontra o salário bruto que, descontados INSS e IRRF, resulta no líquido desejado.</summary>
public sealed class SimularSalarioPeloLiquidoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularSalarioPeloLiquidoRequest>
{
    // O arredondamento em centavos do INSS e do IRRF pode fazer o líquido oscilar um centavo entre brutos vizinhos;
    // por isso a busca procura o valor exato ao redor do resultado aproximado.
    private const long VizinhancaEmCentavos = 300;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularSalarioPeloLiquidoRequest request, CancellationToken cancellationToken)
    {
        if (request.LiquidoDesejado < 0m || request.Dependentes < 0)
            return Erro.Validacao("Informe um líquido e uma quantidade de dependentes maiores ou iguais a zero.");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        decimal Liquido(long centavos) => Apurar(tabelas, centavos / 100m, request.Dependentes).Liquido;

        // O líquido nunca passa do bruto: a busca começa no próprio líquido desejado.
        var desejado = (long)Math.Round(request.LiquidoDesejado * 100m, MidpointRounding.AwayFromZero);
        var maximo = Math.Max(desejado * 3, 100_000);
        while (Liquido(maximo) < request.LiquidoDesejado)
            maximo *= 2;

        // Dentro de cada trecho o líquido cresce com o bruto, e a busca binária encontra o menor bruto que alcança o
        // desejado; entre um trecho e outro ele pode cair, e o escolhido é o menor bruto, entre todos, com o líquido exato.
        var candidatos = new List<(long Centavos, decimal Liquido)>();
        foreach (var (inicio, fim) in TrechosCrescentes(tabelas, request.Dependentes, desejado, maximo))
        {
            var minimo = inicio;
            var limite = fim;
            while (minimo < limite)
            {
                var meio = minimo + (limite - minimo) / 2;
                if (Liquido(meio) >= request.LiquidoDesejado) limite = meio; else minimo = meio + 1;
            }
            for (var centavos = Math.Max(inicio, minimo - VizinhancaEmCentavos); centavos <= Math.Min(fim, minimo + VizinhancaEmCentavos); centavos++)
                candidatos.Add((centavos, Liquido(centavos)));
        }
        var escolhido = candidatos.Where(item => item.Liquido == request.LiquidoDesejado).Select(item => (long?)item.Centavos).Min()
            ?? candidatos.Where(item => item.Liquido >= request.LiquidoDesejado).OrderBy(item => item.Liquido).ThenBy(item => item.Centavos).First().Centavos;

        var apuracao = Apurar(tabelas, escolhido / 100m, request.Dependentes);
        return Montar(request, tabelas, apuracao);
    }

    /// <summary>
    /// Trechos de bruto, em centavos, em que o líquido só cresce. Ele cai de uma vez em dois pontos: no limite de cada faixa
    /// do INSS antes de 03/2020, quando a alíquota da faixa seguinte passa a valer sobre todo o salário, e no primeiro bruto
    /// cujo IRRF passa do limite da dispensa de retenção (R$ 10,00), que deixa de ser zero.
    /// </summary>
    private static IEnumerable<(long Inicio, long Fim)> TrechosCrescentes(TabelasDaCompetencia tabelas, int dependentes, long inicio, long fim)
    {
        var cortes = tabelas.LimitesInss.Select(limite => (long)Math.Round(limite * 100m) + 1).Where(corte => corte > inicio && corte <= fim).Distinct().Order().ToList();
        var inicios = new List<long> { inicio };
        inicios.AddRange(cortes);
        for (var indice = 0; indice < inicios.Count; indice++)
        {
            var de = inicios[indice];
            var ate = indice + 1 < inicios.Count ? inicios[indice + 1] - 1 : fim;
            // Entre os limites do INSS, o IRRF calculado cresce com o bruto: o corte da dispensa é o primeiro acima do limite.
            var queda = PrimeiroComRetencao(tabelas, dependentes, de, ate);
            if (queda is { } corte && corte > de)
            {
                yield return (de, corte - 1);
                yield return (corte, ate);
            }
            else
                yield return (de, ate);
        }
    }

    private static long? PrimeiroComRetencao(TabelasDaCompetencia tabelas, int dependentes, long de, long ate)
    {
        bool Retem(long centavos) => Apurar(tabelas, centavos / 100m, dependentes).Irrf.ImpostoCalculado > tabelas.DescontoMinimo;
        if (tabelas.DescontoMinimo <= 0m || !Retem(ate))
            return null;
        while (de < ate)
        {
            var meio = de + (ate - de) / 2;
            if (Retem(meio)) ate = meio; else de = meio + 1;
        }
        return de;
    }

    private static Apuracao Apurar(TabelasDaCompetencia tabelas, decimal bruto, int dependentes)
    {
        var inss = tabelas.CalcularInss(bruto);
        var irrf = tabelas.CalcularIrrf(bruto, inss.Valor, dependentes);
        return new Apuracao(bruto, inss, irrf, bruto - inss.Valor - irrf.Imposto);
    }

    private static DemonstrativoDto Montar(SimularSalarioPeloLiquidoRequest request, TabelasDaCompetencia tabelas, Apuracao apuracao)
    {
        var diferenca = apuracao.Liquido - request.LiquidoDesejado;
        var fgts = CalculadoraTributacao.Arredondar(apuracao.Bruto * .08m);
        var observacoes = new List<string>
        {
            "Considera que todo o salário é base de INSS e de IRRF, sem outros descontos. Para incluir descontos fixos, como vale-transporte ou plano de saúde, some-os ao líquido desejado.",
            "O IRRF usa a modalidade mais vantajosa para o trabalhador: deduções legais ou desconto simplificado."
        };
        if (diferenca != 0m)
            observacoes.Insert(0, $"Por causa do arredondamento em centavos do INSS e do IRRF, nenhum salário resulta exatamente em {Formato.Moeda(request.LiquidoDesejado)}; o mais próximo resulta em {Formato.Moeda(apuracao.Liquido)}.");

        return new DemonstrativoDto(
            "Salário bruto a partir do líquido",
            $"Competência {Formato.Competencia(tabelas.Competencia)}",
            [
                new("Salário bruto necessário", Formato.Moeda(apuracao.Bruto), $"Para receber {Formato.Moeda(request.LiquidoDesejado)}"),
                new("Líquido obtido", Formato.Moeda(apuracao.Liquido), diferenca == 0m ? "Igual ao desejado" : $"Diferença de {Formato.Moeda(diferenca)}"),
                new("Descontos", Formato.Moeda(apuracao.Inss.Valor + apuracao.Irrf.Imposto), $"INSS {Formato.Moeda(apuracao.Inss.Valor)} + IRRF {Formato.Moeda(apuracao.Irrf.Imposto)}"),
                new("FGTS (8%)", Formato.Moeda(fgts), "Depositado pelo empregador")
            ],
            [new("Salário bruto", "", apuracao.Bruto)],
            [
                new("INSS", "", apuracao.Inss.Valor),
                new(MemoriaTributaria.DescricaoIrrf("IRRF", apuracao.Irrf), MemoriaTributaria.ReferenciaIrrf(apuracao.Irrf), apuracao.Irrf.Imposto)
            ],
            [new("FGTS", "8%", fgts)],
            [
                new GrupoMemoriaDto("Busca do salário bruto", $"Bruto: {Formato.Moeda(apuracao.Bruto)}",
                [
                    new("Método", "O salário bruto foi encontrado por aproximações sucessivas: a cada tentativa, INSS e IRRF são recalculados até o líquido chegar ao valor desejado. Quando mais de um salário resulta no mesmo líquido, vale o menor."),
                    new("Conferência", $"{Formato.Moeda(apuracao.Bruto)} (bruto) - {Formato.Moeda(apuracao.Inss.Valor)} (INSS) - {Formato.Moeda(apuracao.Irrf.Imposto)} (IRRF) = {Formato.Moeda(apuracao.Liquido)}")
                ]),
                MemoriaTributaria.Inss("INSS", apuracao.Inss, "salário bruto"),
                MemoriaTributaria.Irrf("IRRF", apuracao.Irrf, "salário bruto")
            ],
            observacoes);
    }

    private sealed record Apuracao(decimal Bruto, ApuracaoInss Inss, ApuracaoIrrf Irrf, decimal Liquido);
}
