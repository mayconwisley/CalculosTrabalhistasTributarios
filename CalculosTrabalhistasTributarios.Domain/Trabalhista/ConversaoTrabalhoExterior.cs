using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Separa conversão fiscal obrigatória do câmbio efetivamente praticado pelo banco.</summary>
public static class ConversaoTrabalhoExterior
{
    public static Result<ApuracaoTrabalhoExterior> Calcular(EntradaTrabalhoExterior r)
    {
        if (!Enum.IsDefined(r.Vinculo))
            return Erro.Validacao("Selecione o vínculo de trabalho no exterior.");
        if (r.RemuneracaoMoeda <= 0m || r.RemuneracaoMoeda > 100_000_000m || decimal.Round(r.RemuneracaoMoeda, 2) != r.RemuneracaoMoeda)
            return Erro.Validacao("Informe a remuneração recebida maior que zero e até 100 milhões, com no máximo duas casas decimais.");
        if (!ValorValido(r.JurosAtrasoMoeda) || !ValorValido(r.ImpostoRetidoMoeda) || !ValorValido(r.TaxaTransferenciaMoeda) ||
            !ValorValido(r.JurosCobradosMoeda) || !ValorValido(r.TaxaTransferenciaReais))
            return Erro.Validacao("Juros recebidos, imposto retido, taxas e juros cobrados devem ser valores não negativos, até 100 milhões e com no máximo duas casas decimais.");
        if (!CotacaoValida(r.DolaresPorUnidade))
            return Erro.Validacao("USD por unidade: informe cotação positiva, até seis casas decimais e valor máximo de 100.000. Use 1 para USD.");
        if (!CotacaoValida(r.DolarCompraFiscal))
            return Erro.Validacao("Dólar compra fiscal: atualize a cotação online ou informe valor positivo, até seis casas decimais e máximo de 100.000.");
        if (!CotacaoValida(r.CambioEfetivoReais))
            return Erro.Validacao("Câmbio efetivo: informe os reais por unidade pagos pelo banco, valor positivo, até seis casas decimais e máximo de 100.000.");
        if (r.ImpostoRetidoMoeda + r.TaxaTransferenciaMoeda + r.JurosCobradosMoeda > r.RemuneracaoMoeda + r.JurosAtrasoMoeda)
            return Erro.Validacao("O imposto retido e os custos na moeda de origem superam a remuneração e os juros recebidos. Confira os valores do comprovante.");

        // Receita Federal: a base e o imposto estrangeiro passam por USD e pelo dólar-compra fiscal.
        // https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/carne-leao/rendimentos
        var fiscal = r.DolaresPorUnidade * r.DolarCompraFiscal;
        decimal F(decimal valor) => CalculadoraTributacao.Arredondar(valor * fiscal);
        decimal E(decimal valor) => CalculadoraTributacao.Arredondar(valor * r.CambioEfetivoReais);
        var remuneracao = E(r.RemuneracaoMoeda);
        var juros = E(r.JurosAtrasoMoeda);
        var imposto = E(r.ImpostoRetidoMoeda);
        var taxa = E(r.TaxaTransferenciaMoeda);
        var jurosBanco = E(r.JurosCobradosMoeda);
        var creditoBanco = remuneracao + juros - imposto - taxa - jurosBanco - r.TaxaTransferenciaReais;
        if (creditoBanco < 0m)
            return Erro.Validacao("As taxas e os juros cobrados em reais superam o valor convertido. Confira os encargos da operação.");
        var remuneracaoFiscal = F(r.RemuneracaoMoeda);
        var jurosFiscal = F(r.JurosAtrasoMoeda);
        // Juros de mora pelo atraso de salário são isentos; juros em serviços sem vínculo integram o rendimento.
        // Receita: manual MIR, Rendimentos do Trabalho, campo 'Juros por atraso'.
        return new ApuracaoTrabalhoExterior(fiscal, remuneracaoFiscal, jurosFiscal,
            remuneracaoFiscal + (r.Vinculo == VinculoTrabalhoExterior.ServicoPessoaFisica ? jurosFiscal : 0m),
            F(r.ImpostoRetidoMoeda), remuneracao, juros, imposto, taxa, jurosBanco,
            r.TaxaTransferenciaReais, creditoBanco);
    }

    private static bool ValorValido(decimal valor) => valor >= 0m && valor <= 100_000_000m && decimal.Round(valor, 2) == valor;
    private static bool CotacaoValida(decimal valor) => valor > 0m && valor <= 100_000m && decimal.Round(valor, 6) == valor;
}
