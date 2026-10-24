using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class CarneLeaoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularCarneLeaoRequest> _simulador;

    public override string Titulo => "Carnê-leão";
    public override string Descricao => "Calcule o IR mensal sobre rendimentos recebidos de pessoas físicas e do exterior, como honorários e aluguéis.";

    public CarneLeaoMobileViewModel(ISimularDemonstrativoUseCase<SimularCarneLeaoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<CarneLeaoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês do recebimento: o carnê-leão segue o regime de caixa.");
        Secao("secaoRendimentos", "Rendimentos do mês");
        Adicionar("rendimentos", "Rendimentos de pessoas físicas (R$)", TipoCampoMobile.Moeda, "0,00",
            "Honorários, consultas, aulas e outros rendimentos de trabalho recebidos de pessoas físicas ou do exterior.");
        Adicionar("alugueis", "Aluguéis recebidos (R$)", TipoCampoMobile.Moeda, "0,00",
            "Só os recebidos de pessoas físicas; os pagos por empresas têm IRRF retido por elas.");
        Secao("secaoDeducoes", "Deduções");
        Adicionar("despesasAluguel", "Despesas do aluguel (R$)", TipoCampoMobile.Moeda, "0,00",
            "IPTU, condomínio e taxa de administração pagos por você, como locador.");
        Adicionar("livroCaixa", "Livro-caixa (R$)", TipoCampoMobile.Moeda, "0,00",
            "Despesas do consultório ou escritório escrituradas, de quem trabalha por conta própria.");
        Adicionar("inss", "INSS pago no mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Contribuição paga como contribuinte individual, em GPS ou no carnê do INSS.");
        Adicionar("dependentes", "Dependentes", TipoCampoMobile.Inteiro, "0",
            "Dedução por dependente, na modalidade de deduções legais.");
        Adicionar("pensao", "Pensão alimentícia paga (R$)", TipoCampoMobile.Moeda, "0,00",
            "Pensão judicial ou por escritura pública paga no mês.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularCarneLeaoRequest(Competencia("competencia"), Numero("rendimentos"), Numero("alugueis"),
            Numero("despesasAluguel"), Numero("livroCaixa"), Numero("inss"), Inteiro("dependentes"), Numero("pensao")), cancellationToken);
}
