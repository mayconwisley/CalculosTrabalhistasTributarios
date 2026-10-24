using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class RevisaoPensaoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    // Mesma ordem de BasePensao; a revisão compara sempre duas pensões, sem a opção "Não há".
    private static readonly string[] Bases = ["% do líquido", "% do bruto", "% do salário mínimo", "Valor fixo"];
    private const string DicaForma = "Como a decisão ou o acordo define a pensão. Líquido: rendimentos menos INSS e IRRF.";
    private const string DicaPercentual = "De 0 a 100; sobre o salário mínimo, até 1.000 (150 para 1,5 salário mínimo).";

    private readonly ISimularDemonstrativoUseCase<SimularRevisaoPensaoRequest> _simulador;

    public override string Titulo => "Revisão de pensão";
    public override string Descricao => "Compare a pensão atual com a proposta, com o efeito de cada uma no IRRF e no líquido de quem paga.";

    public RevisaoPensaoMobileViewModel(ISimularDemonstrativoUseCase<SimularRevisaoPensaoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<RevisaoPensaoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("pagadorSecao", "Quem paga a pensão");
        Adicionar("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês e ano das tabelas de INSS, IRRF e salário mínimo.");
        Adicionar("valorBruto", "Valor bruto (R$)", TipoCampoMobile.Moeda, "0,00",
            "Rendimentos de quem paga a pensão, que definem o INSS e o IRRF.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0", "Dependentes de quem paga.");

        Secao("atualSecao", "Pensão atual");
        Adicionar("atualBase", "Pensão atual", TipoCampoMobile.Opcao, dica: DicaForma, opcoes: Bases);
        Adicionar("atualPercentual", "Percentual atual (%)", TipoCampoMobile.Numero, "0", DicaPercentual);
        Adicionar("atualValor", "Valor atual (R$)", TipoCampoMobile.Moeda, "0,00", "Valor mensal da pensão atual.");

        Secao("propostaSecao", "Pensão proposta");
        Adicionar("propostaBase", "Pensão proposta", TipoCampoMobile.Opcao, dica: DicaForma, opcoes: Bases);
        Adicionar("propostaPercentual", "Percentual proposto (%)", TipoCampoMobile.Numero, "0", DicaPercentual);
        Adicionar("propostaValor", "Valor proposto (R$)", TipoCampoMobile.Moeda, "0,00", "Valor mensal da pensão proposta.");
        Atualizar("atual");
        Atualizar("proposta");
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "atualBase") Atualizar("atual");
        else if (campo.Chave == "propostaBase") Atualizar("proposta");
    }

    private void Atualizar(string cenario)
    {
        var fixo = (BasePensao)Campo(cenario + "Base").Indice == BasePensao.ValorFixo;
        Campo(cenario + "Percentual").Visivel = !fixo;
        Campo(cenario + "Valor").Visivel = fixo;
    }

    private RegraPensao Regra(string cenario)
    {
        var forma = (BasePensao)Opcao(cenario + "Base");
        return forma == BasePensao.ValorFixo
            ? new RegraPensao(forma, 0m, Numero(cenario + "Valor"))
            : new RegraPensao(forma, Numero(cenario + "Percentual"), 0m);
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularRevisaoPensaoRequest(Competencia("competencia"), Numero("valorBruto"),
            Inteiro("dependentes"), Regra("atual"), Regra("proposta")), cancellationToken);
}
