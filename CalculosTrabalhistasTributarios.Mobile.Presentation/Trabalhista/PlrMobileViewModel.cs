using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class PlrMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularPlrRequest> _simulador;

    public override string Titulo => "PLR (participação nos lucros)";
    public override string Descricao => "Calcule o IRRF pela tabela anual exclusiva, sem INSS ou FGTS.";

    public PlrMobileViewModel(ISimularDemonstrativoUseCase<SimularPlrRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<PlrMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência do pagamento (mm/aaaa)", TipoCampoMobile.Competencia,
            DateTime.Today.ToString("MM/yyyy", Cultura), "Define a tabela anual de IRRF da PLR.");
        Adicionar("valor", "Valor desta PLR (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("anterior", "PLR já paga no ano (R$)", TipoCampoMobile.Moeda, "0,00",
            "O imposto é recalculado sobre a soma das parcelas do ano.");
        Adicionar("impostoAnterior", "IRRF já retido no ano (R$)", TipoCampoMobile.Moeda, "0,00",
            "Imposto retido na parcela anterior, descontado do novo imposto.");
        Adicionar("pensaoBase", "Pensão alimentícia", TipoCampoMobile.Opcao,
            dica: "Base definida na decisão ou no acordo; descontada desta PLR.",
            opcoes: ["Não há", "% do líquido", "% do bruto", "Valor informado"]);
        Adicionar("pensaoPercentual", "Pensão (%)", TipoCampoMobile.Numero, "0");
        Adicionar("pensaoValor", "Valor da pensão (R$)", TipoCampoMobile.Moeda, "0,00");
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "pensaoBase") Atualizar();
    }

    private void Atualizar()
    {
        var forma = Opcao("pensaoBase");
        Campo("pensaoPercentual").Visivel = forma is 1 or 2;
        Campo("pensaoValor").Visivel = forma == 3;
    }

    private RegraPensao? PensaoPlr() => Opcao("pensaoBase") switch
    {
        1 => new(BasePensao.RendimentosLiquidos, Numero("pensaoPercentual"), 0m),
        2 => new(BasePensao.RendimentosBrutos, Numero("pensaoPercentual"), 0m),
        3 => new(BasePensao.ValorFixo, 0m, Numero("pensaoValor")),
        _ => null
    };

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularPlrRequest(Competencia("competencia"), Numero("valor"),
            Numero("anterior"), Numero("impostoAnterior"), PensaoPlr()), cancellationToken);
}
