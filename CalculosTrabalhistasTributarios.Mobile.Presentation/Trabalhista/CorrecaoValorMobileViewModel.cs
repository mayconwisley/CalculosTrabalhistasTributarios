using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class CorrecaoValorMobileViewModel : CalculadoraTrabalhistaViewModel
{
    // A lista segue a ordem da versão desktop, diferente da ordem de IndiceEconomico; por isso o índice é convertido por esta tabela.
    private static readonly IndiceEconomico[] Indices =
        [IndiceEconomico.Ipca, IndiceEconomico.Inpc, IndiceEconomico.IpcaE, IndiceEconomico.Selic, IndiceEconomico.Tr, IndiceEconomico.TaxaLegal];
    private readonly ISimularDemonstrativoUseCase<SimularCorrecaoValorRequest> _simulador;

    public override string Titulo => "Correção de valores";
    public override string Descricao => "Atualize um valor pelo IPCA, INPC, IPCA-E, Selic, TR ou taxa legal, com juros e multa opcionais.";

    public CorrecaoValorMobileViewModel(ISimularDemonstrativoUseCase<SimularCorrecaoValorRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<CorrecaoValorMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("valor", "Valor original (R$)", TipoCampoMobile.Moeda, "0,00", "Valor na data em que era devido.");
        Adicionar("inicio", "Mês em que era devido (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddYears(-1).ToString("MM/yyyy", Cultura),
            "A correção começa nele.");
        Adicionar("fim", "Mês da atualização (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "A correção vai até o mês anterior.");
        Adicionar("indice", "Índice", TipoCampoMobile.Opcao,
            dica: "Índice do contrato ou da decisão; sem previsão, o IPCA é o índice legal desde a Lei 14.905/2024.",
            opcoes: ["IPCA (inflação oficial)", "INPC", "IPCA-E", "Selic", "TR", "Taxa legal"]);
        Adicionar("juros", "Juros ao mês (%, opcional)", TipoCampoMobile.Numero, "0",
            "Juros simples sobre o valor corrigido, como 1% ao mês de um contrato; 0 para só corrigir.");
        Adicionar("multa", "Multa (%, opcional)", TipoCampoMobile.Numero, "0",
            "Multa sobre o valor corrigido, como os 2% de uma conta em atraso; 0 quando não há.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularCorrecaoValorRequest(Numero("valor"), Competencia("inicio"), Competencia("fim"),
            Indices[Opcao("indice")], Numero("juros"), Numero("multa")), cancellationToken);
}
