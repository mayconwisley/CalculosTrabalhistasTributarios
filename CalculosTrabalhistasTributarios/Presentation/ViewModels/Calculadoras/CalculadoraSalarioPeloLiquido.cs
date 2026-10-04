using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSalarioPeloLiquido(ISimularDemonstrativoUseCase<SimularSalarioPeloLiquidoRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _liquido = Moeda("Salário líquido desejado", "Valor que o trabalhador deve receber, já descontados INSS e IRRF.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");

    public override string Titulo => "Salário bruto a partir do líquido";
    public override string Descricao => "Descubra o salário bruto necessário para que o trabalhador receba um líquido desejado.";
    public override string InstrucaoInicial => "Informe a competência, o salário líquido desejado e os dependentes e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _liquido, _dependentes];
    public override string NomeArquivoPdf => $"salario-bruto-pelo-liquido-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularSalarioPeloLiquidoRequest(leitor.Competencia(_competencia), leitor.Moeda(_liquido), leitor.Inteiro(_dependentes)), cancellationToken);
}
