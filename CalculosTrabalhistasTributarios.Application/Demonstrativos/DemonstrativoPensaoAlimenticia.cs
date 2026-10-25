using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>
/// Demonstrativo da pensão alimentícia a partir da simulação já apurada, com os mesmos cartões, comparativo, textos e
/// memória da janela e do PDF do desktop. Os valores seguem a modalidade de menor IRRF, a que a fonte pagadora aplica.
/// </summary>
public static class DemonstrativoPensaoAlimenticia
{
    public static DemonstrativoDto Montar(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada)
    {
        var aplicada = simulacao.Aplicada;
        var ultima = aplicada.Detalhes[^1];
        var pensoes = ultima.Beneficiarios;
        var varias = pensoes.Count > 1;
        var temRendimentos = entrada.ValorBruto > 0m;
        var liquido = entrada.ValorBruto - entrada.OutrosDescontos - simulacao.ValorInss - aplicada.Imposto - aplicada.Pensao;

        DestaqueDto[] destaques =
        [
            new(varias ? "Pensões" : "Pensão", Moeda(aplicada.Pensao), varias ? $"Soma das {pensoes.Count} pensões" : pensoes[0].Descrever(Moeda, Percentual)),
            new("Líquido de quem paga", temRendimentos ? Moeda(liquido) : "—", temRendimentos
                ? $"Bruto menos {(entrada.OutrosDescontos > 0m ? "abatimentos, " : "")}INSS, IRRF e {(varias ? "pensões" : "pensão")}"
                : "Sem valor bruto informado"),
            new(varias ? "IRRF com pensões" : "IRRF com pensão", Moeda(aplicada.Imposto),
                $"{(aplicada.DeduzPensao ? "Deduções legais, com a pensão" : "Desconto simplificado")}; {MemoriaCalculoPensao.FaixaIrrf(ultima.Aliquota, aplicada.Imposto)}"),
            new(varias ? "IRRF sem pensões" : "IRRF sem pensão", Moeda(simulacao.ImpostoSemPensao),
                $"Se não houvesse a pensão; {MemoriaCalculoPensao.FaixaIrrf(simulacao.AliquotaIrrfSemPensao, simulacao.ImpostoSemPensao)}"),
            new("Economia de IRRF", Moeda(simulacao.EconomiaIrrf), simulacao.EconomiaIrrf > 0m ? "Pela dedução da pensão" : "A pensão não reduziu o IRRF"),
            new("INSS", Moeda(simulacao.ValorInss), DescreverBaseInss(simulacao, entrada.BaseInss))
        ];

        // Com rendimentos, o demonstrativo é o salário de quem paga: o resultado é o mesmo líquido do cartão. Sem eles, a
        // pensão não sai de um salário informado e fica como informativo, com o comparativo das modalidades.
        // Na comparação, os beneficiários se correspondem pela posição na lista, mesmo com nomes diferentes.
        var linhasPensao = pensoes.Select((pensao, indice) => new VerbaDto(varias ? $"Pensão - {pensao.Nome}" : "Pensão alimentícia", Referencia(pensao.Regra), pensao.Pensao)
            { ChaveComparacao = $"Pensão do {indice + 1}º beneficiário" }).ToArray();
        var proventos = new List<VerbaDto>();
        var descontos = new List<VerbaDto>();
        var informativos = new List<VerbaDto>();
        if (temRendimentos)
        {
            proventos.Add(new("Rendimentos brutos", "", entrada.ValorBruto));
            if (entrada.OutrosDescontos > 0m)
                descontos.Add(new("Abatimentos dos rendimentos", "", entrada.OutrosDescontos));
            descontos.Add(new("INSS", MemoriaCalculoPensao.FaixaInss(simulacao), simulacao.ValorInss));
            descontos.Add(new($"IRRF ({aplicada.Nome.ToLowerInvariant()})", PrimeiraMaiuscula(MemoriaCalculoPensao.FaixaIrrf(ultima.Aliquota, aplicada.Imposto)), aplicada.Imposto));
            descontos.AddRange(linhasPensao);
        }
        else
        {
            informativos.AddRange(linhasPensao);
        }

        var observacoes = new List<string>
        {
            MemoriaCalculoPensao.Explicacao(simulacao, entrada, Moeda, Percentual),
            MemoriaCalculoPensao.EfeitoNoIrrf(simulacao, Moeda),
            MemoriaCalculoPensao.Faixas(simulacao, Moeda),
            simulacao.MensagemVantagem
        };
        // A soma das linhas das pensões é a mesma pensão da modalidade aplicada; se não for, o motivo precisa aparecer.
        var somaPensoes = pensoes.Sum(pensao => pensao.Pensao);
        if (somaPensoes != aplicada.Pensao)
            observacoes.Add($"A soma das pensões por beneficiário, {Moeda(somaPensoes)}, difere da pensão da modalidade aplicada, {Moeda(aplicada.Pensao)}.");

        return new DemonstrativoDto(
            "Pensão alimentícia",
            $"Competência {Formato.Competencia(entrada.Competencia)} • modalidade aplicada: {aplicada.Nome}",
            destaques,
            proventos,
            descontos,
            informativos,
            Memoria(simulacao, entrada),
            observacoes,
            RotuloProventos: "Rendimentos de quem paga",
            RotuloResultado: "Líquido de quem paga",
            Comparativo: Comparativo(simulacao));
    }

    private static TabelaComparativaDto Comparativo(SimulacaoPensaoDto simulacao)
    {
        var modalidades = simulacao.Modalidades;
        return new TabelaComparativaDto(
            "Comparativo dos modelos",
            modalidades.Select(modalidade => modalidade.Nome + (ReferenceEquals(modalidade, simulacao.Aplicada) ? " (aplicada)" : "")).ToArray(),
            [
                new("Faixa do IRRF", modalidades.Select(modalidade => PrimeiraMaiuscula(MemoriaCalculoPensao.FaixaIrrf(modalidade.Detalhes[^1].Aliquota, modalidade.Imposto))).ToArray()),
                new("IRRF final", modalidades.Select(modalidade => Moeda(modalidade.Imposto)).ToArray()),
                new("Pensão", modalidades.Select(modalidade => Moeda(modalidade.Pensao)).ToArray()),
                new("IRRF + pensão", modalidades.Select(modalidade => Moeda(modalidade.Total)).ToArray(), true)
            ]);
    }

    private static List<GrupoMemoriaDto> Memoria(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada)
    {
        var grupos = new List<GrupoMemoriaDto>
        {
            new GrupoMemoriaDto("Dados considerados", $"Rendimentos: {Moeda(entrada.ValorBruto)}",
            [
                new("Rendimentos", Moeda(entrada.ValorBruto)),
                new("Base de INSS", Moeda(entrada.BaseInss)),
                new("INSS calculado", Moeda(simulacao.ValorInss)),
                new("Pensão", DescreverPensoes(entrada)),
                new("Abatimentos dos rendimentos", Moeda(entrada.OutrosDescontos)),
                new("Dependentes", entrada.Dependentes.ToString(Formato.Cultura))
            ])
        };
        // O INSS é o mesmo nas duas modalidades: uma tabela só, antes das iterações.
        if (simulacao.QuadroInss is { } quadroInss)
            grupos.Add(new("INSS por faixas", $"INSS: {Moeda(simulacao.ValorInss)}",
                [new("Base do INSS", DescreverBaseInss(simulacao, entrada.BaseInss))], quadroInss));

        foreach (var modalidade in simulacao.Modalidades)
        {
            foreach (var detalhe in modalidade.Detalhes)
            {
                var ehUltima = ReferenceEquals(detalhe, modalidade.Detalhes[^1]);
                var formulas = MemoriaCalculoPensao.Iteracao(modalidade, detalhe, entrada, simulacao.ValorInss, Moeda, Percentual).ToList();
                if (ehUltima && MemoriaCalculoPensao.ConferenciaDasFaixas(modalidade, Moeda) is { Length: > 0 } conferencia)
                    formulas.Add(new("IRRF por faixas na última iteração", conferencia));
                grupos.Add(new($"{modalidade.Nome} - iteração {detalhe.Sequencia}",
                    ehUltima ? $"Iterações: {modalidade.Iteracoes} | Total: {Moeda(modalidade.Total)}" : $"Pensão: {Moeda(detalhe.Pensao)}",
                    formulas, ehUltima ? modalidade.QuadroIrrf : null));
            }
        }
        return grupos;
    }

    private static string DescreverPensoes(EntradaPensaoDto entrada) => entrada.Beneficiarios.Count == 1
        ? entrada.Beneficiarios[0].Regra.Descrever(Moeda, Percentual)
        : $"{entrada.Beneficiarios.Count} beneficiários{(entrada.Sucessiva ? ", cada um após descontar os anteriores" : ", sobre a mesma base")}";

    private static string DescreverBaseInss(SimulacaoPensaoDto simulacao, decimal baseInformada)
    {
        // As parcelas da apuração já refletem o teto da competência, inclusive no regime anterior a 03/2020.
        var baseUtilizada = simulacao.FaixasInss.Sum(faixa => faixa.BaseCalculada);
        var descricao = $"Base utilizada: {Moeda(baseUtilizada)}";
        if (baseInformada > baseUtilizada)
            descricao += $" (limitada ao teto); base informada: {Moeda(baseInformada)}";
        if (simulacao.FaixasInss.Count > 0)
            descricao += $"; {MemoriaCalculoPensao.FaixaInss(simulacao)}";
        return descricao;
    }

    private static string Referencia(RegraPensao regra) => regra.EhPercentual ? Percentual(regra.Percentual) : "Valor fixo";
    private static string PrimeiraMaiuscula(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], Formato.Cultura) + texto[1..];
    private static string Moeda(decimal valor) => Formato.Moeda(valor);

    // Mesmo formato da janela e do PDF do desktop: "30,00%".
    private static string Percentual(decimal valor) => valor.ToString("N2", Formato.Cultura) + "%";
}
