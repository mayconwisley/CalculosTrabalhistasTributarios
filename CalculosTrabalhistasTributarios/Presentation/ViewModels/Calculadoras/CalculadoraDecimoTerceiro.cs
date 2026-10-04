using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraDecimoTerceiro : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest> _simulador;

    public CalculadoraDecimoTerceiro(ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest> simulador)
    {
        _simulador = simulador;
        _adiantamento.AoAlterar = () => _valorAdiantamento.Visivel = _adiantamento.Valor<AdiantamentoDecimoTerceiro>() == AdiantamentoDecimoTerceiro.ValorInformado;
        _adiantamento.AoAlterar();
    }

    private readonly CampoTextoViewModel _competencia = new("Competência do pagamento", TipoCampo.Competencia, $"12/{DateTime.Today.Year}", "Mês da 2ª parcela, normalmente dezembro (MM/AAAA).");
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário mensal de dezembro.");
    private readonly CampoTextoViewModel _medias = Moeda("Médias de variáveis", "Média anual de horas extras, comissões e adicionais; deixe 0,00 se não houver.");
    private readonly CampoTextoViewModel _avos = Inteiro("Avos (meses trabalhados)", 12, "Meses do ano com 15 dias ou mais de trabalho, de 1 a 12.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");
    private readonly CampoOpcaoViewModel _adiantamento = new("1ª parcela (adiantamento)",
        [
            new("50% do 13º (padrão)", AdiantamentoDecimoTerceiro.CinquentaPorCento),
            new("Não houve adiantamento", AdiantamentoDecimoTerceiro.SemAdiantamento),
            new("Valor informado", AdiantamentoDecimoTerceiro.ValorInformado)
        ], "Como foi paga a 1ª parcela, descontada na 2ª.");
    private readonly CampoTextoViewModel _valorAdiantamento = Moeda("Valor da 1ª parcela", "Valor pago como adiantamento, descontado na 2ª parcela.");
    private readonly CamposPensao _pensao = new("Pensão sobre o 13º definida na decisão ou no acordo, descontada na 2ª parcela.");
    private readonly CampoTextoViewModel _previdencia = Moeda("Previdência complementar", "Contribuição do trabalhador ao PGBL, ao fundo de pensão ou ao Fapi sobre o 13º, descontada na 2ª parcela: deduzida da base do IRRF até 12% do 13º.");

    public override string Titulo => "13º salário";
    public override string Descricao => "Calcule a 1ª e a 2ª parcela do 13º, com o INSS e o IRRF descontados em dezembro.";
    public override string InstrucaoInicial => "Informe o salário, as médias, os avos e os dependentes e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _salario, _medias, _avos, _dependentes, _adiantamento, _valorAdiantamento, .. _pensao.Campos, _previdencia];
    public override string NomeArquivoPdf => $"decimo-terceiro-{_competencia.Valor.Replace('/', '-')}.pdf";

    // A competência fica em dezembro: a do último cálculo raramente é a do pagamento do 13º.
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularDecimoTerceiroRequest(
            leitor.Competencia(_competencia), leitor.Moeda(_salario), leitor.Moeda(_medias), leitor.Inteiro(_avos), leitor.Inteiro(_dependentes),
            _adiantamento.Valor<AdiantamentoDecimoTerceiro>(), leitor.Moeda(_valorAdiantamento), leitor.Pensao(_pensao), leitor.Moeda(_previdencia)), cancellationToken);
}
