using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class SalarioFamiliaMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularSalarioFamiliaRequest> _simulador;

    public override string Titulo => "Salário-família";
    public override string Descricao => "Verifique o direito e calcule a cota conforme a remuneração, os filhos e os dias trabalhados.";

    public SalarioFamiliaMobileViewModel(ISimularDemonstrativoUseCase<SimularSalarioFamiliaRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<SalarioFamiliaMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês do pagamento, que define o limite e a cota.");
        Adicionar("remuneracao", "Remuneração do mês cheio (R$)", TipoCampoMobile.Moeda, "0,00",
            "Inclua horas extras e adicionais, sem o 13º e o 1/3 de férias. Na admissão ou dispensa, informe o valor do mês completo.");
        Adicionar("filhos", "Filhos com direito", TipoCampoMobile.Inteiro, "1",
            "Filhos ou equiparados até 14 anos, ou inválidos de qualquer idade.");
        Adicionar("dias", "Dias trabalhados", TipoCampoMobile.Inteiro, "30",
            "Use 30 no mês completo; na admissão ou dispensa, informe os dias trabalhados.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularSalarioFamiliaRequest(Competencia("competencia"), Numero("remuneracao"),
            Inteiro("filhos"), Inteiro("dias")), cancellationToken);
}
