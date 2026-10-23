using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class TributoAtrasoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularTributoAtrasoRequest> _simulador;

    public override string Titulo => "Tributo em atraso";
    public override string Descricao => "Calcule a multa e os juros pela Selic de um DARF, DAS, DAE ou GPS pago depois do vencimento.";

    public TributoAtrasoMobileViewModel(ISimularDemonstrativoUseCase<SimularTributoAtrasoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<TributoAtrasoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        // Mesma ordem de GuiaDeRecolhimento, para converter o índice no enum.
        Adicionar("guia", "Guia", TipoCampoMobile.Opcao, dica: "Todas seguem a mesma regra de multa e juros.",
            opcoes: ["DARF (tributos federais)", "DAS (Simples Nacional)", "DAE (empregador doméstico)", "GPS (INSS)"]);
        Adicionar("principal", "Valor principal (R$)", TipoCampoMobile.Moeda, "0,00", "Valor da guia no vencimento, sem acréscimos.");
        Adicionar("vencimento", "Vencimento (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.AddMonths(-1).ToString("dd/MM/yyyy", Cultura),
            "Já prorrogado para o dia útil seguinte quando cai em fim de semana ou feriado.");
        Adicionar("pagamento", "Pagamento (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Data em que a guia será paga. A Selic do mês anterior precisa estar na tabela local.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularTributoAtrasoRequest((GuiaDeRecolhimento)Opcao("guia"), Numero("principal"),
            Data("vencimento"), Data("pagamento")), cancellationToken);
}
