using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraDecimoTerceiro : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest> _simulador;
    private readonly ISimularDemonstrativoUseCase<SimularComplementoDecimoTerceiroRequest> _complemento;

    public CalculadoraDecimoTerceiro(ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest> simulador,
        ISimularDemonstrativoUseCase<SimularComplementoDecimoTerceiroRequest> complemento)
    {
        _simulador = simulador;
        _complemento = complemento;
        _adiantamento.AoAlterar = Ajustar;
        _calculo.AoAlterar = Ajustar;
        Ajustar();
    }

    private bool EhComplemento => _calculo.Valor<bool>();

    // O complemento usa a média paga em dezembro e as variáveis do ano; adiantamento, pensão e previdência ficam de fora.
    private void Ajustar()
    {
        var complemento = EhComplemento;
        _adiantamento.Visivel = _previdencia.Visivel = !complemento;
        _valorAdiantamento.Visivel = !complemento && _adiantamento.Valor<AdiantamentoDecimoTerceiro>() == AdiantamentoDecimoTerceiro.ValorInformado;
        _pensao.Exibir(!complemento);
        _variaveisAteNovembro.Visivel = _variaveisDezembro.Visivel = _pagamento.Visivel = _tributacao.Visivel = complemento;
    }

    private readonly CampoOpcaoViewModel _calculo = new("Cálculo",
        [new("1ª e 2ª parcelas", false), new("Complemento das médias (até 10/01)", true)],
        "O complemento refaz o 13º com as variáveis de dezembro e apura a diferença paga até 10 de janeiro (Decreto 57.155/1965, art. 2º).") { Largura = 260 };
    private readonly CampoTextoViewModel _variaveisAteNovembro = Moeda("Variáveis de janeiro a novembro", "Soma das horas extras, comissões, adicionais e outras variáveis do ano até novembro.");
    private readonly CampoTextoViewModel _variaveisDezembro = Moeda("Variáveis de dezembro", "Variáveis de dezembro, que não entraram na média da 2ª parcela.");
    private readonly CampoTextoViewModel _pagamento = new("Pagamento do complemento", TipoCampo.Competencia, $"01/{DateTime.Today.Year + 1}", "Mês do pagamento da diferença, normalmente janeiro do ano seguinte (MM/AAAA).");
    private readonly CampoOpcaoViewModel _tributacao = new("IRRF no ano seguinte",
        [new("RRA de 1 mês (eSocial)", TributacaoComplementoDecimoTerceiro.Rra), new("Recálculo do 13º (IN 1.500, art. 13)", TributacaoComplementoDecimoTerceiro.Recalculo)],
        "Pago no ano seguinte, o eSocial trata a diferença de 13º como RRA de um mês; a IN RFB 1.500/2014, art. 13, § 3º, prevê recalcular o 13º total. Pago no mesmo ano, vale o recálculo.") { Largura = 260 };

    private readonly CampoTextoViewModel _competencia = new("Competência do pagamento", TipoCampo.Competencia, $"12/{DateTime.Today.Year}", "Mês da 2ª parcela, normalmente dezembro (MM/AAAA).");
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário mensal de dezembro.");
    private readonly CampoTextoViewModel _medias = Moeda("Médias de variáveis", "Média anual de horas extras, comissões e adicionais; deixe 0,00 se não houver. No complemento, a média usada na 2ª parcela.");
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
    public override string Descricao => "Calcule a 1ª e a 2ª parcela do 13º, com o INSS e o IRRF de dezembro, ou o complemento das médias de variáveis pago até 10 de janeiro.";
    public override string InstrucaoInicial => "Informe o salário, as médias, os avos e os dependentes e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_calculo, _competencia, _salario, _medias, _avos, _dependentes, _variaveisAteNovembro, _variaveisDezembro, _pagamento, _tributacao,
        _adiantamento, _valorAdiantamento, .. _pensao.Campos, _previdencia];
    public override string NomeArquivoPdf => $"decimo-terceiro-{_competencia.Valor.Replace('/', '-')}.pdf";

    // A competência fica em dezembro: a do último cálculo raramente é a do pagamento do 13º.
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) => EhComplemento
        ? LerECalcularAsync(_complemento, leitor => new SimularComplementoDecimoTerceiroRequest(
            leitor.Competencia(_competencia), leitor.Competencia(_pagamento), leitor.Moeda(_salario), leitor.Moeda(_medias),
            leitor.Moeda(_variaveisAteNovembro), leitor.Moeda(_variaveisDezembro), leitor.Inteiro(_avos), leitor.Inteiro(_dependentes),
            _tributacao.Valor<TributacaoComplementoDecimoTerceiro>()), cancellationToken)
        : LerECalcularAsync(_simulador, leitor => new SimularDecimoTerceiroRequest(
            leitor.Competencia(_competencia), leitor.Moeda(_salario), leitor.Moeda(_medias), leitor.Inteiro(_avos), leitor.Inteiro(_dependentes),
            _adiantamento.Valor<AdiantamentoDecimoTerceiro>(), leitor.Moeda(_valorAdiantamento), leitor.Pensao(_pensao), leitor.Moeda(_previdencia)), cancellationToken);
}
