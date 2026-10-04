using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraIntermitente(ISimularDemonstrativoUseCase<SimularIntermitenteRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _valorHora = Moeda("Valor da hora", "Valor da hora no contrato, não menor que o do salário mínimo.");
    private readonly CampoTextoViewModel _horas = new("Horas trabalhadas", TipoCampo.Horas, "0:00", "Horas trabalhadas no período da convocação, como 40:00.");
    private readonly CampoTextoViewModel _dias = Inteiro("Dias trabalhados", 5, "Dias de trabalho no período.");
    private readonly CampoTextoViewModel _descansos = Inteiro("Domingos e feriados", 1, "Domingos e feriados dentro do período, que geram o DSR.");

    public override string Titulo => "Trabalho intermitente";
    public override string Descricao => "Calcule o pagamento ao fim de cada convocação: horas, DSR, férias com 1/3, 13º proporcional e FGTS.";
    public override string InstrucaoInicial => "Informe o valor da hora, as horas e os dias do período e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _valorHora, _horas, _dias, _descansos];
    public override string NomeArquivoPdf => $"intermitente-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularIntermitenteRequest(leitor.Competencia(_competencia), leitor.Moeda(_valorHora), leitor.Horas(_horas),
            leitor.Inteiro(_dias), leitor.Inteiro(_descansos)), cancellationToken);
}
