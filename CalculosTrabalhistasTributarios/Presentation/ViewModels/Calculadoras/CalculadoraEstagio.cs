using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraEstagio(ISimularDemonstrativoUseCase<SimularEstagioRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _bolsa = Moeda("Bolsa", "Valor mensal da bolsa de estágio.");
    private readonly CampoTextoViewModel _auxilio = Moeda("Auxílio-transporte", "Auxílio-transporte pago com a bolsa.");
    private readonly CampoTextoViewModel _inicio = new("Início do estágio", TipoCampo.Data, DateTime.Today.AddYears(-1).ToString("dd/MM/yyyy", Cultura), "Data de início no termo de compromisso (dd/mm/aaaa).");
    private readonly CampoTextoViewModel _fim = new("Fim do estágio", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Fim do estágio, ou a data até a qual o recesso é contado (dd/mm/aaaa).");
    private readonly CampoTextoViewModel _gozados = new("Recesso já usufruído (dias)", TipoCampo.Numero, "0", "Dias de recesso que o estagiário já tirou.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");

    public override string Titulo => "Estágio";
    public override string Descricao => "Calcule o líquido da bolsa de estágio, com o IRRF, e o recesso remunerado proporcional.";
    public override string InstrucaoInicial => "Informe a bolsa, o auxílio e as datas do estágio e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _bolsa, _auxilio, _inicio, _fim, _gozados, _dependentes];
    public override string NomeArquivoPdf => $"estagio-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularEstagioRequest(leitor.Competencia(_competencia), leitor.Moeda(_bolsa), leitor.Moeda(_auxilio),
            leitor.Data(_inicio), leitor.Data(_fim), leitor.Numero(_gozados), leitor.Inteiro(_dependentes)), cancellationToken);
}
