using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class CreditoTrabalhadorMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularCreditoTrabalhadorRequest> _simulador;

    public override string Titulo => "Crédito do Trabalhador";
    public override string Descricao => "Simule o consignado com parcelas, juros e custos da proposta. A margem de referência é 35% da remuneração habitual disponível; valores e custo efetivo são estimados e dependem da análise do banco.";

    public CreditoTrabalhadorMobileViewModel(ISimularDemonstrativoUseCase<SimularCreditoTrabalhadorRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<CreditoTrabalhadorMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("secaoMargem", "Remuneração e margem");
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês e ano das tabelas de INSS e IRRF.");
        Adicionar("salario", "Remuneração habitual (R$)", TipoCampoMobile.Moeda, "0,00",
            "Salário e adicionais habituais com INSS do vínculo, sem verbas variáveis como horas extras. INSS e IRRF são estimados.");
        Adicionar("descontosCp", "Descontos previdenciários (R$)", TipoCampoMobile.Moeda, "0,00",
            "Faltas, DSR perdido e outros descontos que reduzem a base do INSS. Não informe o INSS: ele é calculado.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Adicionar("pensao", "Pensão alimentícia (R$)", TipoCampoMobile.Moeda, "0,00",
            "Pensão compulsória mensal, dedutível do IRRF.");
        Adicionar("compulsorios", "Outros compulsórios (R$)", TipoCampoMobile.Moeda, "0,00",
            "Outros descontos compulsórios usados na margem. Não repita INSS, IRRF, pensão nem consignados.");
        Adicionar("existentes", "Consignados atuais (R$)", TipoCampoMobile.Moeda, "0,00",
            "Total mensal das prestações já contratadas neste vínculo; ocupam a margem estimada.");
        // Campo numérico, e não monetário, para preservar o vazio: o comportamento monetário trocaria o vazio por 0,00,
        // e zero informado significa sem margem livre.
        Adicionar("margem", "Margem livre oficial (R$, opcional)", TipoCampoMobile.Numero, "",
            "Margem confirmada na plataforma ou no banco, já após os contratos existentes. Vazio estima pela remuneração; 0,00 significa sem margem livre.");
        Secao("secaoProposta", "Proposta");
        // Mesma ordem de ObjetivoCreditoTrabalhador.
        Adicionar("objetivo", "Objetivo", TipoCampoMobile.Opcao,
            dica: "Simular uma proposta ou estimar o crédito que cabe na margem. Não representa aprovação pelo banco.",
            opcoes: ["Valor desejado", "Valor pela margem"]);
        Adicionar("valor", "Crédito desejado (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor antes dos custos descontados na liberação. IOF e outros custos financiados são somados ao principal.");
        Adicionar("prazo", "Parcelas (meses)", TipoCampoMobile.Inteiro, "24",
            "De 1 a 120 meses, sem carência. Confirme o prazo disponível na proposta.");
        Adicionar("juros", "Juros (% ao mês)", TipoCampoMobile.Numero, "0",
            "Taxa mensal da proposta, de 0 a 20%, com até seis casas decimais. Não use taxa anual nem CET.");
        Secao("secaoCustos", "IOF e custos");
        Adicionar("modoIof", "Cálculo do IOF", TipoCampoMobile.Opcao,
            dica: "Automático: 0,38% + 0,0082% ao dia por amortização, limitado a 365 dias.",
            opcoes: ["Automático (estimado)", "Informado pelo banco"]);
        Adicionar("dataLiberacao", "Data de liberação (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Data em que o crédito é disponibilizado; usada só no IOF.");
        Adicionar("primeiroVencimento", "Primeiro vencimento (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.AddMonths(1).ToString("dd/MM/yyyy", Cultura),
            "Sugestão: um mês após hoje. Os seguintes vencem no mesmo dia mensal, sem ajuste por feriados; usado só no IOF.");
        Adicionar("iof", "IOF financiado (R$)", TipoCampoMobile.Moeda, "0,00", "IOF da proposta incluído no financiamento.");
        Adicionar("outros", "Custos financiados (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Seguros e demais custos incluídos no financiamento, exceto o IOF.");
        Adicionar("liberacao", "Custos na liberação (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Custos descontados do crédito recebido. Não repita valores informados como financiados.");
        Secao("secaoQuitacao", "Quitação antecipada");
        Adicionar("pagasParaQuitar", "Parcelas já pagas (opcional)", TipoCampoMobile.Inteiro, "0",
            "Para simular a quitação: o saldo usa a taxa do contrato e abate os juros futuros (Resolução CMN 5.004/2022, art. 7º). 0 não simula.");
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "objetivo" or "modoIof") Atualizar();
    }

    private void Atualizar()
    {
        Campo("valor").Visivel = Campo("objetivo").Indice == (int)ObjetivoCreditoTrabalhador.ValorDesejado;
        var automatico = Campo("modoIof").Indice == 0;
        Campo("iof").Visivel = !automatico;
        Campo("dataLiberacao").Visivel = automatico;
        Campo("primeiroVencimento").Visivel = automatico;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var automatico = Opcao("modoIof") == 0;
        var credito = new EntradaCreditoTrabalhador((ObjetivoCreditoTrabalhador)Opcao("objetivo"),
            Campo("valor").Visivel ? Numero("valor") : 0m, Inteiro("prazo"), Numero("juros"),
            automatico ? 0m : Numero("iof"), Numero("outros"), Numero("liberacao"), 0m, Numero("existentes"),
            Texto("margem").Length == 0 ? null : Numero("margem"), automatico,
            automatico ? Data("dataLiberacao") : null, automatico ? Data("primeiroVencimento") : null);
        return _simulador.ExecutarAsync(new SimularCreditoTrabalhadorRequest(Competencia("competencia"), Numero("salario"),
            Numero("descontosCp"), Inteiro("dependentes"), Numero("pensao"), Numero("compulsorios"), credito,
            Inteiro("pagasParaQuitar")), cancellationToken);
    }
}
