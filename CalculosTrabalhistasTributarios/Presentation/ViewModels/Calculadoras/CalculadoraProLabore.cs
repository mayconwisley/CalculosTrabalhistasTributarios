using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraProLabore : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularProLaboreRequest> _simulador;
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoOpcaoViewModel _tipo = new("Tipo",
        [new("Pró-labore (sócio)", TipoContribuinteIndividual.ProLabore), new("Autônomo (RPA)", TipoContribuinteIndividual.Autonomo)],
        "Pró-labore é a remuneração do sócio; RPA é o recibo de pagamento a autônomo.");
    private readonly CampoTextoViewModel _valor = Moeda("Valor bruto", "Pró-labore ou valor do serviço.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");
    private readonly CampoTextoViewModel _iss = new("ISS retido (%)", TipoCampo.Numero, "0", "Alíquota do ISS do município, de 2% a 5%, quando a lei municipal exige a retenção; 0 para não reter.");
    private readonly CampoOpcaoViewModel _regime = new("Regime da empresa",
        [
            new("Lucro Real ou Presumido", RegimeTributario.LucroRealOuPresumido),
            new("Simples (anexos I a III e V)", RegimeTributario.SimplesNacional),
            new("Simples (anexo IV)", RegimeTributario.SimplesNacionalAnexoIV)
        ], "Define se a empresa paga o INSS patronal de 20%.");

    public CalculadoraProLabore(ISimularDemonstrativoUseCase<SimularProLaboreRequest> simulador)
    {
        _simulador = simulador;
        _tipo.AoAlterar = () => _iss.Visivel = _tipo.Valor<TipoContribuinteIndividual>() == TipoContribuinteIndividual.Autonomo;
        _tipo.AoAlterar();
    }

    public override string Titulo => "Pró-labore e autônomo";
    public override string Descricao => "Calcule o líquido do pró-labore do sócio ou do pagamento a autônomo (RPA) e o custo para a empresa.";
    public override string InstrucaoInicial => "Informe a competência, o tipo, o valor bruto e os dependentes e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _tipo, _valor, _dependentes, _iss, _regime];
    public override string NomeArquivoPdf => $"{(_tipo.Valor<TipoContribuinteIndividual>() == TipoContribuinteIndividual.Autonomo ? "rpa" : "pro-labore")}-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _valor;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularProLaboreRequest(
            leitor.Competencia(_competencia), _tipo.Valor<TipoContribuinteIndividual>(), leitor.Moeda(_valor), leitor.Inteiro(_dependentes),
            _iss.Visivel ? leitor.Numero(_iss) : 0m, _regime.Valor<RegimeTributario>()), cancellationToken);
}
