using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class EstagioMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularEstagioRequest> _simulador;

    public override string Titulo => "Estágio";
    public override string Descricao => "Calcule o líquido da bolsa de estágio, com o IRRF, e o recesso remunerado proporcional.";

    public EstagioMobileViewModel(ISimularDemonstrativoUseCase<SimularEstagioRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<EstagioMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("bolsaSecao", "Bolsa do mês");
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês e ano das tabelas de IRRF.");
        Adicionar("bolsa", "Bolsa mensal (R$)", TipoCampoMobile.Moeda, "0,00", "Valor mensal da bolsa de estágio.");
        Adicionar("auxilio", "Auxílio-transporte (R$)", TipoCampoMobile.Moeda, "0,00", "Auxílio-transporte pago com a bolsa.");
        Adicionar("diasNoMes", "Dias no mês (opcional)", TipoCampoMobile.Inteiro, "0",
            "No mês de início ou de fim: dias de estágio na competência, para a bolsa proporcional com 30 dias por mês. Deixe 0 para o mês inteiro.");

        Secao("recessoSecao", "Recesso e IRRF");
        Adicionar("inicio", "Início do estágio (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.AddYears(-1).ToString("dd/MM/yyyy", Cultura),
            "Data de início no termo de compromisso.");
        Adicionar("fim", "Fim do estágio (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Fim do estágio, ou a data até a qual o recesso é contado.");
        Adicionar("gozados", "Recesso já usufruído (dias)", TipoCampoMobile.Numero, "0", "Dias de recesso que o estagiário já tirou.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularEstagioRequest(Competencia("competencia"), Numero("bolsa"), Numero("auxilio"),
            Data("inicio"), Data("fim"), Numero("gozados"), Inteiro("dependentes"), Inteiro("diasNoMes")), cancellationToken);
}
