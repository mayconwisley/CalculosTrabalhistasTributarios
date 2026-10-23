using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class SalarioPeloLiquidoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularSalarioPeloLiquidoRequest> _simulador;

    public override string Titulo => "Salário bruto a partir do líquido";
    public override string Descricao => "Descubra o salário bruto necessário para que o trabalhador receba um líquido desejado.";

    public SalarioPeloLiquidoMobileViewModel(ISimularDemonstrativoUseCase<SimularSalarioPeloLiquidoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<SalarioPeloLiquidoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês e ano das tabelas de INSS e IRRF.");
        Adicionar("liquido", "Salário líquido desejado (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor que o trabalhador deve receber, já descontados INSS e IRRF.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularSalarioPeloLiquidoRequest(Competencia("competencia"), Numero("liquido"),
            Inteiro("dependentes")), cancellationToken);
}
