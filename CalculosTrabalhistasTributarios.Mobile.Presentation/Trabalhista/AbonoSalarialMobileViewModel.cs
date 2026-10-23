using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class AbonoSalarialMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularAbonoSalarialRequest> _simulador;

    public override string Titulo => "Abono salarial (PIS/Pasep)";
    public override string Descricao => "Veja se há direito ao abono salarial e o valor pelos meses trabalhados no ano-base.";

    public AbonoSalarialMobileViewModel(ISimularDemonstrativoUseCase<SimularAbonoSalarialRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<AbonoSalarialMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("anoBase", "Ano-base (aaaa)", TipoCampoMobile.Inteiro, (DateTime.Today.Year - 2).ToString(Cultura),
            "Ano trabalhado; o abono é pago dois anos depois (o de 2024, em 2026).");
        Adicionar("meses", "Meses trabalhados", TipoCampoMobile.Inteiro, "12",
            "Meses com carteira assinada no ano-base; a fração de 15 dias ou mais conta como mês.");
        Adicionar("media", "Remuneração média mensal (R$)", TipoCampoMobile.Moeda, "0,00",
            "Média das remunerações recebidas no ano-base.");
        Adicionar("cadastro", "Cadastrado no PIS/Pasep há 5 anos", TipoCampoMobile.SimNao,
            dica: "Inscrito no PIS/Pasep há pelo menos 5 anos.").Marcado = true;
        Adicionar("limite", "Limite de renda (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Limite da média do calendário; 0,00 usa o publicado (R$ 2.766,00 em 2026).");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularAbonoSalarialRequest(Inteiro("anoBase"), Inteiro("meses"), Numero("media"),
            Sim("cadastro"), Numero("limite")), cancellationToken);
}
