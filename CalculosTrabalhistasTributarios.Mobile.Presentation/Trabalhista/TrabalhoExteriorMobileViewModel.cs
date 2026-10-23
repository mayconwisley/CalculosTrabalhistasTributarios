using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Trabalho no exterior do residente fiscal no Brasil. A consulta online do dólar fiscal e da PTAX do desktop não existe no
/// aplicativo móvel, que não acessa a internet: as cotações são digitadas a partir das fontes oficiais.
/// </summary>
public sealed class TrabalhoExteriorMobileViewModel : CalculadoraTrabalhistaViewModel
{
    // Mesma lista e ordem do desktop; a moeda vazia é "Outro país", que pede o nome e a moeda.
    private static readonly (string Pais, string Moeda)[] Paises =
    [
        ("Estados Unidos", "USD"), ("Portugal", "EUR"), ("Alemanha", "EUR"), ("Espanha", "EUR"),
        ("França", "EUR"), ("Itália", "EUR"), ("Países Baixos", "EUR"), ("Irlanda", "EUR"),
        ("Bélgica", "EUR"), ("Áustria", "EUR"), ("Finlândia", "EUR"), ("Grécia", "EUR"),
        ("Reino Unido", "GBP"), ("Canadá", "CAD"), ("Austrália", "AUD"), ("Suíça", "CHF"),
        ("Japão", "JPY"), ("Argentina", "ARS"), ("Chile", "CLP"), ("México", "MXN"),
        ("Colômbia", "COP"), ("Peru", "PEN"), ("Uruguai", "UYU"), ("Paraguai", "PYG"),
        ("China", "CNY"), ("Índia", "INR"), ("Coreia do Sul", "KRW"), ("Singapura", "SGD"),
        ("Nova Zelândia", "NZD"), ("África do Sul", "ZAR"), ("Outro país", "")
    ];

    private readonly ISimularDemonstrativoUseCase<SimularTrabalhoExteriorRequest> _simulador;

    public override string Titulo => "Trabalho no exterior — residente no Brasil";
    public override string Descricao => "Simule salário ou serviços recebidos de fonte estrangeira como pessoa física residente fiscal no Brasil. " +
        "Informe o dólar fiscal, a cotação da moeda e o câmbio efetivo e os encargos do comprovante bancário.";

    public TrabalhoExteriorMobileViewModel(ISimularDemonstrativoUseCase<SimularTrabalhoExteriorRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<TrabalhoExteriorMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("secaoRendimento", "Rendimento");
        Adicionar("recebimento", "Recebimento", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Data em que o rendimento foi recebido; define o mês do carnê-leão.");
        Adicionar("pais", "País de origem", TipoCampoMobile.Opcao,
            dica: "País da fonte pagadora. A moeda é sugerida; confira a moeda real do contrato e a elegibilidade para compensar o imposto.",
            opcoes: Paises.Select(pais => pais.Pais).ToArray());
        Adicionar("outroPais", "Outro país (nome)", TipoCampoMobile.Texto, "", "Preencha quando o país não estiver na lista.");
        Adicionar("moeda", "Moeda (ISO)", TipoCampoMobile.Texto, "USD", "Código de três letras, como USD, EUR ou GBP.");
        Adicionar("vinculo", "Vínculo", TipoCampoMobile.Opcao,
            dica: "Emprego com fonte estrangeira ou serviço prestado em nome próprio como pessoa física. Pessoa jurídica brasileira não é abrangida.",
            opcoes: ["Emprego assalariado", "Serviço como PF"]);
        Adicionar("remuneracao", "Remuneração (moeda)", TipoCampoMobile.Moeda, "0,00",
            "Valor bruto na moeda estrangeira, antes de imposto, taxas e juros bancários.");
        Adicionar("jurosRecebidos", "Juros recebidos (moeda)", TipoCampoMobile.Moeda, "0,00",
            "Juros de mora efetivamente recebidos pelo atraso do pagamento. Zero quando não houver.");
        Adicionar("impostoExterior", "IR exterior retido (moeda)", TipoCampoMobile.Moeda, "0,00",
            "Imposto de renda sobre a remuneração tributável no Brasil, retido no mesmo recebimento. Não inclua contribuições sociais.");
        Secao("secaoCotacoes", "Cotações (informe manualmente)");
        Adicionar("usdPorUnidade", "USD por unidade", TipoCampoMobile.Numero, "1",
            "Cotação oficial da moeda de origem em dólares dos EUA na data do recebimento (PTAX do Banco Central). Para USD, use 1. Até seis casas decimais.");
        Adicionar("dolarFiscal", "Dólar compra fiscal (R$/USD)", TipoCampoMobile.Numero, "0",
            "Cotação de compra do Banco Central do último dia útil da primeira quinzena do mês anterior ao recebimento, divulgada pela Receita Federal.");
        Adicionar("cambioEfetivo", "Câmbio efetivo (R$ por unidade)", TipoCampoMobile.Numero, "0",
            "Reais recebidos por unidade da moeda estrangeira, do comprovante bancário, antes das taxas informadas separadamente.");
        Secao("secaoEncargos", "Encargos bancários");
        Adicionar("taxaMoeda", "Taxa bancária (moeda)", TipoCampoMobile.Moeda, "0,00",
            "Taxa de transferência cobrada na moeda estrangeira, separada do spread já embutido no câmbio efetivo.");
        Adicionar("jurosBanco", "Juros bancários (moeda)", TipoCampoMobile.Moeda, "0,00",
            "Juros efetivamente cobrados pela instituição sobre a operação. Não estime sem o contrato.");
        Adicionar("taxaReais", "Taxa bancária (R$)", TipoCampoMobile.Moeda, "0,00", "Taxa de recebimento ou transferência debitada em reais.");
        Secao("secaoDeducoes", "Imposto e deduções");
        Adicionar("creditoExterior", "Compensar IR exterior", TipoCampoMobile.Opcao,
            dica: "Confirme apenas se houver tratado ou reciprocidade aplicável e o imposto não for restituído nem compensado no exterior.",
            opcoes: ["Não confirmado", "Elegibilidade confirmada"]);
        Adicionar("previdencia", "Previdência Brasil (R$)", TipoCampoMobile.Moeda, "0,00",
            "Contribuição à previdência oficial brasileira paga no mês. Não informe previdência estrangeira.");
        Adicionar("dependentes", "Dependentes", TipoCampoMobile.Inteiro, "0", "Número de dependentes para o carnê-leão, quando adotadas deduções legais.");
        Adicionar("pensao", "Pensão paga (R$)", TipoCampoMobile.Moeda, "0,00", "Pensão alimentícia dedutível paga no mês.");
        Adicionar("livroCaixa", "Livro-caixa (R$)", TipoCampoMobile.Moeda, "0,00",
            "Despesas necessárias, comprovadas e escrituradas, limitadas ao rendimento do mês.");
        AtualizarPais();
        AtualizarVinculo();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "pais") AtualizarPais();
        else if (campo.Chave == "vinculo") AtualizarVinculo();
    }

    /// <summary>Como no desktop: sugere a moeda do país e zera as cotações, que passam a ser de outra operação.</summary>
    private void AtualizarPais()
    {
        var moeda = Paises[Opcao("pais")].Moeda;
        Campo("outroPais").Visivel = moeda.Length == 0;
        if (moeda.Length > 0) Campo("moeda").Valor = moeda;
        Campo("usdPorUnidade").Valor = moeda == "USD" ? "1" : "0";
        Campo("cambioEfetivo").Valor = "0";
    }

    private void AtualizarVinculo() =>
        Campo("livroCaixa").Visivel = (VinculoTrabalhoExterior)Opcao("vinculo") == VinculoTrabalhoExterior.ServicoPessoaFisica;

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var outroPais = Campo("outroPais").Visivel;
        return _simulador.ExecutarAsync(new SimularTrabalhoExteriorRequest(
            Data("recebimento"), outroPais ? Campo("outroPais").Valor : Paises[Opcao("pais")].Pais, Campo("moeda").Valor,
            new EntradaTrabalhoExterior((VinculoTrabalhoExterior)Opcao("vinculo"), Numero("remuneracao"),
                Numero("jurosRecebidos"), Numero("impostoExterior"), Numero("taxaMoeda"),
                Numero("jurosBanco"), Numero("taxaReais"), Numero("usdPorUnidade"),
                Numero("dolarFiscal"), Numero("cambioEfetivo")),
            Opcao("creditoExterior") == 1, Numero("previdencia"), Inteiro("dependentes"),
            Numero("pensao"), Campo("livroCaixa").Visivel ? Numero("livroCaixa") : 0m), cancellationToken);
    }
}
