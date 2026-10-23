using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Financeiro;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class EmprestimoPessoalMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularEmprestimoPessoalRequest> _simulador;

    public override string Titulo => "Empréstimo pessoal";
    public override string Descricao => "Simule empréstimo pessoal PF com parcelas mensais Price, sem carência, e primeira parcela um mês após a liberação. IOF, seguro e demais custos aparecem separados; contratos com juros diários ou outra data de primeira parcela exigem cálculo específico.";

    public EmprestimoPessoalMobileViewModel(ISimularDemonstrativoUseCase<SimularEmprestimoPessoalRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<EmprestimoPessoalMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("proposta", "Proposta");
        Adicionar("valor", "Valor solicitado (R$)", TipoCampoMobile.Moeda, "0,00",
            "Crédito antes dos descontos na liberação. Seguro, outros financiados e IOF financiado são somados automaticamente.");
        Adicionar("parcelas", "Parcelas (meses)", TipoCampoMobile.Inteiro, "24",
            "De 1 a 120 meses; primeira parcela um mês após a liberação.");
        Adicionar("juros", "Juros (% ao mês)", TipoCampoMobile.Numero, "0",
            "Taxa mensal da proposta, de 0 a 20%, até seis casas decimais. Não use taxa anual nem CET.");
        Adicionar("data", "Data de liberação (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "De 01/01/2025 a 31/12/2100. Define o calendário das parcelas e os dias do IOF.");

        Secao("iofSecao", "IOF");
        Adicionar("modoIof", "Cálculo do IOF", TipoCampoMobile.Opcao,
            opcoes: ["Automático (estimado)", "Informado pelo banco"]);
        Adicionar("cobrancaIof", "Cobrança do IOF", TipoCampoMobile.Opcao,
            dica: "Financiado aumenta o saldo devedor; descontado reduz o valor recebido.",
            opcoes: ["Financiado", "Descontado do crédito"]);
        Adicionar("iof", "IOF informado (R$)", TipoCampoMobile.Moeda, "0,00",
            "IOF da proposta; informe zero só se o banco confirmou que não há cobrança.");

        Secao("custos", "Custos (opcionais)");
        Adicionar("seguro", "Seguro financiado (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Preço do seguro em reais, não o percentual de cobertura.");
        Adicionar("outrosCustos", "Outros financiados (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Demais custos somados ao financiamento. Não repita seguro nem IOF.");
        Adicionar("custosLiberacao", "Custos na liberação (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Custos retidos do valor solicitado, exceto IOF. Não repita valores financiados.");
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "modoIof") Atualizar();
    }

    private void Atualizar() => Campo("iof").Visivel = Campo("modoIof").Indice != 0;

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var iofAutomatico = Opcao("modoIof") == 0;
        var entrada = new EntradaEmprestimoPessoal(Numero("valor"), Inteiro("parcelas"), Numero("juros"), Data("data"),
            iofAutomatico, iofAutomatico ? 0m : Numero("iof"), Opcao("cobrancaIof") == 0,
            Numero("seguro"), Numero("outrosCustos"), Numero("custosLiberacao"));
        return _simulador.ExecutarAsync(new SimularEmprestimoPessoalRequest(entrada), cancellationToken);
    }
}
