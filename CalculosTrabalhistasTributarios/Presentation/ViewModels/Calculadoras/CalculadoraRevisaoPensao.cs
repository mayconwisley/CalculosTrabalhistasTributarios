using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraRevisaoPensao(ISimularDemonstrativoUseCase<SimularRevisaoPensaoRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _valorBruto = Moeda("Valor bruto", "Rendimentos de quem paga a pensão, que definem o INSS e o IRRF.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes de quem paga, para a dedução do IRRF.");
    private readonly CamposPensao _atual = CamposPensao.Cenario("atual", "atual");
    private readonly CamposPensao _proposta = CamposPensao.Cenario("proposta", "proposto");

    public override string Titulo => "Revisão de pensão";
    public override string Descricao => "Compare a pensão atual com a proposta, com o efeito de cada uma no IRRF e no líquido de quem paga.";
    public override string InstrucaoInicial => "Informe os rendimentos de quem paga, a pensão atual e a proposta e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _valorBruto, _dependentes, .. _atual.Campos, .. _proposta.Campos];
    public override string NomeArquivoPdf => $"revisao-pensao-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _valorBruto;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularRevisaoPensaoRequest(leitor.Competencia(_competencia), leitor.Moeda(_valorBruto), leitor.Inteiro(_dependentes), leitor.Pensao(_atual)!, leitor.Pensao(_proposta)!), cancellationToken);
}
