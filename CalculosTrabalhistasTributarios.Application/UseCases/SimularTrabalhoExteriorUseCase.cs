using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Câmbio do recebimento e carnê-leão mensal de trabalho de fonte estrangeira.</summary>
public sealed class SimularTrabalhoExteriorUseCase(ITributacaoConsulta consulta) : ISimularDemonstrativoUseCase<SimularTrabalhoExteriorRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularTrabalhoExteriorRequest r, CancellationToken cancellationToken)
    {
        if (r.Recebimento == default || r.Recebimento.Year < 2020 || r.Recebimento.Year > 2100)
            return Erro.Validacao("Informe a data do recebimento entre 2020 e 2100 no formato dd/mm/aaaa.");
        if (string.IsNullOrWhiteSpace(r.PaisOrigem) || r.PaisOrigem.Length > 80)
            return Erro.Validacao("Informe o país de origem do rendimento, com até 80 caracteres.");
        var moeda = r.Moeda.Trim().ToUpperInvariant();
        if (moeda.Length != 3 || !moeda.All(char.IsAsciiLetterUpper))
            return Erro.Validacao("Informe o código da moeda com três letras, por exemplo USD, EUR ou GBP.");
        if (r.PrevidenciaBrasil < 0m || r.PrevidenciaBrasil > 100_000_000m ||
            r.PensaoPaga < 0m || r.PensaoPaga > 100_000_000m ||
            r.LivroCaixa < 0m || r.LivroCaixa > 100_000_000m || r.Dependentes is < 0 or > 100 ||
            decimal.Round(r.PrevidenciaBrasil, 2) != r.PrevidenciaBrasil ||
            decimal.Round(r.PensaoPaga, 2) != r.PensaoPaga || decimal.Round(r.LivroCaixa, 2) != r.LivroCaixa)
            return Erro.Validacao("Previdência, pensão e livro-caixa devem ser não negativos, até 100 milhões e com dois centavos; dependentes, de 0 a 100.");
        if (r.Valores.Vinculo == VinculoTrabalhoExterior.EmpregoAssalariado && r.LivroCaixa > 0m)
            return Erro.Validacao("Livro-caixa não é dedutível de salário. Zere o campo ou selecione prestação de serviços como pessoa física.");

        var apuracao = ConversaoTrabalhoExterior.Calcular(r.Valores);
        if (apuracao.Falhou) return apuracao.Erro;
        var a = apuracao.Valor;
        if (r.LivroCaixa > a.RendimentosTributaveis)
            return Erro.Validacao("Livro-caixa não pode superar os rendimentos de prestação de serviços deste mês. Informe apenas a parcela usada agora.");

        var competencia = new DateOnly(r.Recebimento.Year, r.Recebimento.Month, 1);
        var tabelas = await consulta.ObterTabelasAsync(competencia, cancellationToken);
        if (tabelas.Falhou) return tabelas.Erro;
        // O limite de compensação é a diferença do imposto com e sem os rendimentos do exterior.
        // Aqui toda a base tributável do mês é estrangeira; outras fontes exigem apuração mensal conjunta no Carnê-Leão Web.
        var ir = tabelas.Valor.CalcularIrrf(a.RendimentosTributaveis, r.PrevidenciaBrasil, r.Dependentes, r.PensaoPaga,
            tributacaoExclusiva: true, livroCaixa: r.LivroCaixa);
        var creditoExterior = r.CompensacaoExteriorConfirmada ? Math.Min(a.ImpostoExteriorFiscal, ir.Imposto) : 0m;
        var impostoBrasil = ir.Imposto - creditoExterior;
        var acumula = impostoBrasil is > 0m and < 10m;
        var vencimento = Prazos.UltimoDiaUtil(competencia.AddMonths(1));

        var proventos = new List<VerbaDto> { new("Remuneração convertida", $"{moeda} × câmbio efetivo", a.RemuneracaoEfetiva) };
        if (a.JurosRecebidosEfetivos > 0m) proventos.Add(new("Juros recebidos por atraso", $"{moeda} × câmbio efetivo", a.JurosRecebidosEfetivos));
        var descontos = new List<VerbaDto>();
        if (a.ImpostoExteriorEfetivo > 0m) descontos.Add(new("Imposto retido no exterior", "Saída de caixa", a.ImpostoExteriorEfetivo));
        if (a.TaxaMoedaEfetiva > 0m) descontos.Add(new("Taxa de transferência em moeda", "Câmbio efetivo", a.TaxaMoedaEfetiva));
        if (a.JurosBancoEfetivos > 0m) descontos.Add(new("Juros cobrados pela instituição", "Câmbio efetivo", a.JurosBancoEfetivos));
        if (a.TaxaReais > 0m) descontos.Add(new("Taxa de transferência em reais", "Valor informado", a.TaxaReais));
        descontos.Add(new("Carnê-leão a recolher no Brasil", acumula ? "Acumula para mês seguinte" : $"DARF 0190 até {Formato.Data(vencimento)}", impostoBrasil));

        var observacoes = new List<string>
        {
            "Para residente fiscal no Brasil, trabalho recebido de fonte estrangeira entra no carnê-leão no mês do recebimento, mesmo quando o valor permanece fora do País. Esta simulação trata um recebimento mensal e não substitui o ajuste anual.",
            "A cotação fiscal usa a moeda de origem para dólar dos EUA na data do recebimento e depois o dólar-compra do Banco Central do último dia útil da primeira quinzena do mês anterior. Informe os valores oficiais; o câmbio efetivo do banco serve apenas ao fluxo de caixa.",
            "Taxas de transferência, spread cambial e juros cobrados pela instituição reduzem o caixa, mas não foram deduzidos da base do carnê-leão. Previdência oficial no Brasil, pensão e livro-caixa dependem das condições legais.",
            "O imposto no exterior só é compensável se houver tratado ou reciprocidade aplicável e se não for restituído nem compensado lá. A opção de compensação deve ser confirmada pelo contribuinte; o limite é o imposto brasileiro sobre a renda estrangeira do mês.",
            "Informe como imposto exterior compensável apenas a parcela atribuível aos rendimentos tributáveis também no Brasil. Eventual imposto estrangeiro sobre juros de mora de salário isentos aqui exige conferência separada.",
            "A simulação não calcula imposto devido ao país de origem, previdência estrangeira, FGTS, direitos trabalhistas estrangeiros, remessas em datas distintas, outros rendimentos no mês ou regras de pessoa jurídica."
        };
        if (r.Valores.Vinculo == VinculoTrabalhoExterior.EmpregoAssalariado)
            observacoes.Add("Juros de mora por atraso no pagamento de remuneração de emprego foram apresentados como isentos no IR brasileiro, sem abatê-los do caixa. O cenário exclui 13º e situações especiais de servidores brasileiros no exterior.");
        if (acumula)
            observacoes.Add($"O DARF de {Formato.Moeda(impostoBrasil)} é inferior a R$ 10,00 e deve ser acumulado com o mês seguinte (Lei 9.430/1996, art. 68).");

        return new DemonstrativoDto(
            "Trabalho no exterior — residente no Brasil",
            $"Recebido em {Formato.Data(r.Recebimento)} • {r.PaisOrigem.Trim()} • {moeda}",
            [
                new("Após câmbio e tributos", Formato.Moeda(a.CreditoBanco - impostoBrasil), "Disponível estimado"),
                new("Crédito do banco", Formato.Moeda(a.CreditoBanco), "Antes do carnê-leão brasileiro"),
                new("IR brasileiro", acumula ? "Acumula" : Formato.Moeda(impostoBrasil), $"Antes da compensação: {Formato.Moeda(ir.Imposto)}"),
                new("Crédito do imposto exterior", Formato.Moeda(creditoExterior), r.CompensacaoExteriorConfirmada ? "Limitado ao IR brasileiro" : "Não aplicado"),
                new("Câmbio fiscal / efetivo", $"{a.CotacaoFiscalReais.ToString("N6", Formato.Cultura)} / {r.Valores.CambioEfetivoReais.ToString("N6", Formato.Cultura)}", $"Reais por 1 {moeda}")
            ],
            proventos, descontos,
            [new("Rendimentos tributáveis no Brasil", "Conversão fiscal", a.RendimentosTributaveis),
             new("Imposto exterior em reais fiscais", "Base do crédito possível", a.ImpostoExteriorFiscal)],
            [
                new GrupoMemoriaDto("Câmbio e fluxo de caixa", $"Crédito bancário: {Formato.Moeda(a.CreditoBanco)}", [
                    new("Cotação fiscal", $"{r.Valores.DolaresPorUnidade.ToString("N6", Formato.Cultura)} USD/{moeda} × {r.Valores.DolarCompraFiscal.ToString("N6", Formato.Cultura)} R$/USD = {a.CotacaoFiscalReais.ToString("N12", Formato.Cultura)} R$/{moeda}"),
                    new("Remuneração fiscal", $"{r.Valores.RemuneracaoMoeda.ToString("N2", Formato.Cultura)} {moeda} × {a.CotacaoFiscalReais.ToString("N12", Formato.Cultura)} = {Formato.Moeda(a.RemuneracaoFiscal)}"),
                    new("Juros recebidos", $"{r.Valores.JurosAtrasoMoeda.ToString("N2", Formato.Cultura)} {moeda} × {a.CotacaoFiscalReais.ToString("N12", Formato.Cultura)} = {Formato.Moeda(a.JurosRecebidosFiscal)}"),
                    new("Câmbio efetivo", $"{r.Valores.CambioEfetivoReais.ToString("N6", Formato.Cultura)} reais por {moeda}; remuneração {Formato.Moeda(a.RemuneracaoEfetiva)} + juros {Formato.Moeda(a.JurosRecebidosEfetivos)}"),
                    new("Crédito bancário", $"{Formato.Moeda(a.RemuneracaoEfetiva)} + {Formato.Moeda(a.JurosRecebidosEfetivos)} - imposto exterior {Formato.Moeda(a.ImpostoExteriorEfetivo)} - taxa em moeda {Formato.Moeda(a.TaxaMoedaEfetiva)} - juros bancários {Formato.Moeda(a.JurosBancoEfetivos)} - taxa em reais {Formato.Moeda(a.TaxaReais)} = {Formato.Moeda(a.CreditoBanco)}")
                ]),
                MemoriaTributaria.Irrf("Carnê-leão antes do crédito exterior", ir, "remuneração e juros tributáveis do exterior"),
                new GrupoMemoriaDto("Impostos e disponível", $"Disponível estimado: {Formato.Moeda(a.CreditoBanco - impostoBrasil)}", [
                    new("Imposto estrangeiro convertido", $"{r.Valores.ImpostoRetidoMoeda.ToString("N2", Formato.Cultura)} {moeda} × {a.CotacaoFiscalReais.ToString("N12", Formato.Cultura)} = {Formato.Moeda(a.ImpostoExteriorFiscal)}"),
                    new("Crédito admitido", $"Menor entre {Formato.Moeda(a.ImpostoExteriorFiscal)} e {Formato.Moeda(ir.Imposto)}: {Formato.Moeda(creditoExterior)}"),
                    new("Carnê-leão brasileiro", $"{Formato.Moeda(ir.Imposto)} - {Formato.Moeda(creditoExterior)} = {Formato.Moeda(impostoBrasil)}"),
                    new("Disponível", $"{Formato.Moeda(a.CreditoBanco)} - {Formato.Moeda(impostoBrasil)} = {Formato.Moeda(a.CreditoBanco - impostoBrasil)}")
                ])
            ],
            observacoes,
            RotuloProventos: "Recebimento ao câmbio efetivo",
            RotuloResultado: "Após câmbio e tributos");
    }
}
