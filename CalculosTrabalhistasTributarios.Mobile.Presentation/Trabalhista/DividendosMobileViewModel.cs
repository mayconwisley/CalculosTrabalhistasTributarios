using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class DividendosMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularDividendosRequest> _simulador;

    public override string Titulo => "Dividendos";
    public override string Descricao => "Retenção de 10% sobre lucros e dividendos acima de R$ 50 mil no mês, desde 2026 (Lei 15.270/2025).";

    public DividendosMobileViewModel(ISimularDemonstrativoUseCase<SimularDividendosRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<DividendosMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês dos pagamentos, desde 01/2026.");
        Adicionar("valor", "Dividendos no mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Total pago no mês pela mesma empresa à mesma pessoa, somando todos os pagamentos.");
        Adicionar("transicao", "Lucros até 2025 (R$)", TipoCampoMobile.Moeda, "0,00",
            "Parte de lucros apurados até 2025, com distribuição aprovada até 31/12/2025, que não sofre retenção.");
        Adicionar("jaRetido", "IRRF já retido no mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Retido em pagamentos anteriores do mesmo mês; é descontado do imposto refeito sobre o total.");
        Adicionar("residencia", "Beneficiário", TipoCampoMobile.Opcao,
            dica: "No exterior, a retenção de 10% vale para qualquer valor, sem o limite de R$ 50 mil.",
            opcoes: ["Residente no Brasil", "Residente no exterior"]);
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularDividendosRequest(Competencia("competencia"), Numero("valor"), Numero("transicao"),
            Numero("jaRetido"), Opcao("residencia") == 1), cancellationToken);
}
