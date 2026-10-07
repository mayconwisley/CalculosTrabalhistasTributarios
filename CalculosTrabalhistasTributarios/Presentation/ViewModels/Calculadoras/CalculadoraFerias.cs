using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraFerias(ISimularDemonstrativoUseCase<SimularFeriasRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia("Competência do pagamento");
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário mensal na data das férias.");
    private readonly CampoTextoViewModel _medias = Moeda("Médias de variáveis", "Média de horas extras, comissões e adicionais do período aquisitivo; deixe 0,00 se não houver.");
    private readonly CampoTextoViewModel _faltas = Inteiro("Faltas injustificadas", 0, "Faltas no período aquisitivo: até 5 dão 30 dias; de 6 a 14, 24; de 15 a 23, 18; de 24 a 32, 12.");
    private readonly CampoTextoViewModel _diasGozo = Inteiro("Dias de descanso", 0, "Deixe 0 para usar todos os dias de direito que não forem vendidos; informe menos para dividir as férias.");
    private readonly CampoOpcaoViewModel _abono = CampoOpcaoViewModel.SimNao("Vender 1/3 (abono)", false, "Converte 1/3 dos dias em dinheiro, sem INSS e sem IRRF.");
    private readonly CampoOpcaoViewModel _adiantamento13 = CampoOpcaoViewModel.SimNao("Adiantar 13º (1ª parcela)", false, "Paga metade do 13º junto com as férias, quando solicitado.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");
    private readonly CamposPensao _pensao = new("Pensão sobre as férias + 1/3 definida na decisão ou no acordo.");
    private readonly CampoTextoViewModel _previdencia = Moeda("Previdência complementar", "Contribuição do trabalhador ao PGBL, ao fundo de pensão ou ao Fapi sobre as férias: deduzida por inteiro da base do IRRF nas deduções legais.");
    private readonly CampoTextoViewModel _baseForaFerias = Moeda("Base fora das férias (R$)", "Opcional: remuneração com INSS do mesmo mês de pagamento e gozo, excluindo férias e seu terço. Permite conciliar o INSS total da folha com o provisionado no recibo de férias.");

    public override string Titulo => "Férias";
    public override string Descricao => "Calcule as férias com o terço constitucional, a venda de dias (abono) e o adiantamento do 13º.";
    public override string InstrucaoInicial => "Informe o salário, as médias, as faltas e as opções de abono e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _salario, _medias, _faltas, _diasGozo, _abono, _adiantamento13, _dependentes, .. _pensao.Campos, _previdencia, _baseForaFerias];
    public override string NomeArquivoPdf => $"ferias-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularFeriasRequest(
            leitor.Competencia(_competencia), leitor.Moeda(_salario), leitor.Moeda(_medias), leitor.Inteiro(_faltas), leitor.Inteiro(_diasGozo),
            _abono.Valor<bool>(), _adiantamento13.Valor<bool>(), leitor.Inteiro(_dependentes), leitor.Pensao(_pensao), leitor.Moeda(_previdencia),
            leitor.Moeda(_baseForaFerias)), cancellationToken);
}
