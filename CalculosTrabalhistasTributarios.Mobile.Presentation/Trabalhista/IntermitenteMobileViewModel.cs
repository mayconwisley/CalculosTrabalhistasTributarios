using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class IntermitenteMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularIntermitenteRequest> _simulador;

    public override string Titulo => "Trabalho intermitente";
    public override string Descricao => "Calcule o pagamento ao fim de cada convocação: horas, DSR, férias com 1/3, 13º proporcional e FGTS.";

    public IntermitenteMobileViewModel(ISimularDemonstrativoUseCase<SimularIntermitenteRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<IntermitenteMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês e ano das tabelas de INSS e IRRF.");
        Adicionar("valorHora", "Valor da hora (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor da hora no contrato, não menor que o do salário mínimo.");
        Adicionar("horas", "Horas trabalhadas (h)", TipoCampoMobile.Horas, "0:00",
            "Horas no período da convocação, como 40:00 ou 40,5.");
        Adicionar("dias", "Dias trabalhados", TipoCampoMobile.Inteiro, "5", "Dias de trabalho no período.");
        Adicionar("descansos", "Domingos e feriados", TipoCampoMobile.Inteiro, "1",
            "Domingos e feriados dentro do período, que geram o DSR.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularIntermitenteRequest(Competencia("competencia"), Numero("valorHora"), Horas("horas"),
            Inteiro("dias"), Inteiro("descansos")), cancellationToken);
}
