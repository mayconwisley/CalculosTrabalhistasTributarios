using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraPlr(ISimularDemonstrativoUseCase<SimularPlrRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia("Competência do pagamento", "Mês do pagamento da PLR, que define a tabela anual usada (MM/AAAA).");
    private readonly CampoTextoViewModel _valor = Moeda("Valor da PLR", "Valor bruto desta parcela da participação nos lucros ou resultados.");
    private readonly CampoTextoViewModel _anterior = Moeda("PLR já paga no ano", "Outra parcela paga no mesmo ano; o imposto é recalculado sobre o total.");
    private readonly CampoTextoViewModel _impostoAnterior = Moeda("IRRF já retido no ano", "Imposto retido na parcela anterior, que é descontado do imposto recalculado.");
    private readonly CamposPensao _pensao = new("Pensão alimentícia judicial descontada desta PLR, que reduz a base do imposto.");

    public override string Titulo => "PLR (participação nos lucros)";
    public override string Descricao => "Calcule o IRRF da participação nos lucros ou resultados pela tabela anual exclusiva, sem INSS e sem FGTS.";
    public override string InstrucaoInicial => "Informe a competência do pagamento e o valor da PLR e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _valor, _anterior, _impostoAnterior, .. _pensao.Campos];
    public override string NomeArquivoPdf => $"plr-{_competencia.Valor.Replace('/', '-')}.pdf";

    // O valor da PLR não é um salário mensal e não é trocado com as outras calculadoras.
    protected override CampoTextoViewModel CampoCompetencia => _competencia;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularPlrRequest(leitor.Competencia(_competencia), leitor.Moeda(_valor), leitor.Moeda(_anterior), leitor.Moeda(_impostoAnterior), leitor.Pensao(_pensao)), cancellationToken);
}
