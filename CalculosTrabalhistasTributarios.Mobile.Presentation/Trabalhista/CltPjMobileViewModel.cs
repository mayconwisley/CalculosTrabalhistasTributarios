using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class CltPjMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularCltPjRequest> _simulador;

    public override string Titulo => "CLT x PJ";
    public override string Descricao => "Compare o valor que sobra ao profissional e o custo da empresa nos dois regimes.";

    public CltPjMobileViewModel(ISimularDemonstrativoUseCase<SimularCltPjRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<CltPjMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura));
        Adicionar("salario", "Salário CLT (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0", "Aplica-se aos dois regimes.");
        Adicionar("beneficios", "Benefícios do CLT (R$)", TipoCampoMobile.Moeda, "0,00",
            "Vale-refeição, alimentação, plano de saúde e outros benefícios mensais.");
        Adicionar("regime", "Regime da empresa contratante", TipoCampoMobile.Opcao,
            dica: "Define os encargos sobre o salário CLT.",
            opcoes: ["Lucro Real ou Presumido", "Simples (anexos I a III e V)", "Simples (anexo IV)"]);
        Adicionar("valorPj", "Valor mensal como PJ (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor da nota fiscal por mês. Use 0,00 para calcular o valor equivalente ao total CLT.");
        Adicionar("tributacao", "Simples Nacional do PJ", TipoCampoMobile.Opcao,
            dica: "Fator R usa pró-labore de 28% do faturamento; Anexo III ou V usam pró-labore de um salário mínimo.",
            opcoes: ["Anexo III (fator R)", "Anexo III", "Anexo V"]);
        Adicionar("custosPj", "Custos mensais do PJ (R$)", TipoCampoMobile.Moeda, "0,00",
            "Contabilidade, taxas e outros custos fixos.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularCltPjRequest(Competencia("competencia"), Numero("salario"),
            Inteiro("dependentes"), Numero("beneficios"), (RegimeTributario)Opcao("regime"),
            Numero("valorPj"), (TributacaoPj)Opcao("tributacao"), Numero("custosPj")), cancellationToken);
}
