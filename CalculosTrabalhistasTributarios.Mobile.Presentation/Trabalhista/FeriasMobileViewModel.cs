using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class FeriasMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularFeriasRequest> _simulador;
    public override string Titulo => "Férias";
    public override string Descricao => "Recibo de férias, abono, adiantamento do 13º e INSS por mês de gozo.";

    public FeriasMobileViewModel(ISimularDemonstrativoUseCase<SimularFeriasRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<FeriasMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência do pagamento (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura));
        Adicionar("inicio", "Início do gozo (dd/mm/aaaa, opcional)", TipoCampoMobile.Data, dica: "Informe para separar férias e INSS pelas competências do descanso.");
        Adicionar("fimConcessivo", "Concessão até (dd/mm/aaaa, opcional)", TipoCampoMobile.Data, dica: "Dias gozados depois dessa data são apurados em dobro.");
        Adicionar("salario", "Salário mensal (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("medias", "Médias de variáveis (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("faltas", "Faltas injustificadas no período", TipoCampoMobile.Inteiro, "0");
        Adicionar("dias", "Dias de descanso", TipoCampoMobile.Inteiro, "0", "Zero usa todos os dias de direito que não forem vendidos.");
        Adicionar("segundo", "Dias do 2º período (opcional)", TipoCampoMobile.Inteiro, "0");
        Adicionar("terceiro", "Dias do 3º período (opcional)", TipoCampoMobile.Inteiro, "0");
        Adicionar("abono", "Vender 1/3 das férias (abono)", TipoCampoMobile.SimNao);
        Adicionar("adiantamento", "Adiantar 13º (1ª parcela)", TipoCampoMobile.SimNao);
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        AdicionarPensao();
        Adicionar("previdencia", "Previdência complementar (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("basePrimeiro", "Base fora das férias no 1º mês (R$)", TipoCampoMobile.Moeda, "0,00", "Não repita férias e 1/3.");
        Adicionar("baseSegundo", "Base fora das férias no 2º mês (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("baseTerceiro", "Base fora das férias no 3º mês (R$)", TipoCampoMobile.Moeda, "0,00");
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "pensaoBase") AtualizarPensao();
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularFeriasRequest(Competencia("competencia"), Numero("salario"), Numero("medias"),
            Inteiro("faltas"), Inteiro("dias"), Sim("abono"), Sim("adiantamento"), Inteiro("dependentes"), Pensao(),
            Numero("previdencia"), Numero("basePrimeiro"), DataOpcional("inicio"), Numero("baseSegundo"),
            Numero("baseTerceiro"), [Inteiro("segundo"), Inteiro("terceiro")], DataOpcional("fimConcessivo")), cancellationToken);
}
