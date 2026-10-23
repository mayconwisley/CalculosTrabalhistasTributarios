using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class ProLaboreMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularProLaboreRequest> _simulador;

    public override string Titulo => "Pró-labore e autônomo";
    public override string Descricao => "Estime o valor líquido do pró-labore ou RPA e o custo da empresa.";

    public ProLaboreMobileViewModel(ISimularDemonstrativoUseCase<SimularProLaboreRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<ProLaboreMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura));
        Adicionar("tipo", "Tipo", TipoCampoMobile.Opcao,
            dica: "Pró-labore remunera o sócio; RPA é o recibo de pagamento ao autônomo.",
            opcoes: ["Pró-labore (sócio)", "Autônomo (RPA)"]);
        Adicionar("valor", "Valor bruto (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Adicionar("iss", "ISS retido (%)", TipoCampoMobile.Numero, "0",
            "Alíquota municipal de 2% a 5% quando a lei exigir retenção; use 0 quando não houver.");
        Adicionar("regime", "Regime da empresa", TipoCampoMobile.Opcao,
            dica: "Define se a empresa paga INSS patronal de 20%.",
            opcoes: ["Lucro Real ou Presumido", "Simples (anexos I a III e V)", "Simples (anexo IV)"]);
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "tipo") Atualizar();
    }

    private void Atualizar() => Campo("iss").Visivel = Opcao("tipo") == 1;

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularProLaboreRequest(Competencia("competencia"),
            (TipoContribuinteIndividual)Opcao("tipo"), Numero("valor"), Inteiro("dependentes"),
            Campo("iss").Visivel ? Numero("iss") : 0m, (RegimeTributario)Opcao("regime")), cancellationToken);
}
