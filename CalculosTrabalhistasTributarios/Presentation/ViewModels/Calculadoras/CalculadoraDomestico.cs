using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraDomestico(ISimularDemonstrativoUseCase<SimularDomesticoRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário mensal registrado na carteira.");
    private readonly CampoTextoViewModel _adicionais = Moeda("Horas extras e adicionais", "Horas extras, adicional noturno e outros valores do mês, que têm INSS, IRRF e FGTS.");
    private readonly CampoTextoViewModel _faltas = Inteiro("Faltas", 0, "Faltas injustificadas no mês, descontadas a 1/30 do salário cada.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes do empregado para a dedução do IRRF.");
    private readonly CampoTextoViewModel _valeTransporte = Moeda("Custo do vale-transporte", "Custo mensal das passagens do empregado; o desconto é de até 6% do salário. Deixe 0,00 se não há.");

    public override string Titulo => "Empregado doméstico (DAE)";
    public override string Descricao => "Calcule o salário líquido do doméstico e o DAE do mês, com INSS, IRRF, FGTS e os encargos do empregador.";
    public override string InstrucaoInicial => "Informe a competência, o salário e os demais valores do mês e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _salario, _adicionais, _faltas, _dependentes, _valeTransporte];
    public override string NomeArquivoPdf => $"domestico-dae-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularDomesticoRequest(leitor.Competencia(_competencia), leitor.Moeda(_salario), leitor.Moeda(_adicionais),
            leitor.Inteiro(_faltas), leitor.Inteiro(_dependentes), leitor.Moeda(_valeTransporte)), cancellationToken);
}
