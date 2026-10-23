using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class DomesticoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularDomesticoRequest> _simulador;

    public override string Titulo => "Empregado doméstico (DAE)";
    public override string Descricao => "Calcule o salário líquido do doméstico e o DAE do mês, com INSS, IRRF, FGTS e os encargos do empregador.";

    public DomesticoMobileViewModel(ISimularDemonstrativoUseCase<SimularDomesticoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<DomesticoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês e ano das tabelas de INSS e IRRF.");
        Adicionar("salario", "Salário mensal (R$)", TipoCampoMobile.Moeda, "0,00", "Salário registrado na carteira.");
        Adicionar("adicionais", "Horas extras e adicionais (R$)", TipoCampoMobile.Moeda, "0,00",
            "Horas extras, adicional noturno e outros valores do mês, com INSS, IRRF e FGTS.");
        Adicionar("faltas", "Faltas (dias)", TipoCampoMobile.Inteiro, "0",
            "Faltas injustificadas no mês, descontadas a 1/30 do salário cada.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Adicionar("valeTransporte", "Custo do vale-transporte (R$)", TipoCampoMobile.Moeda, "0,00",
            "Custo mensal das passagens; o desconto é de até 6% do salário. Use 0,00 se não há.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularDomesticoRequest(Competencia("competencia"), Numero("salario"), Numero("adicionais"),
            Inteiro("faltas"), Inteiro("dependentes"), Numero("valeTransporte")), cancellationToken);
}
