using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraCreditoTrabalhador : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularCreditoTrabalhadorRequest> _simulador;
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _salario = Moeda("Remuneração habitual", "Salário e adicionais habituais com incidência previdenciária do vínculo. Exclua verbas variáveis, como horas extras. INSS e IRRF serão estimados automaticamente.");
    private readonly CampoTextoViewModel _descontosCp = Moeda("Descontos previdenciários", "Faltas, DSR perdido e outros descontos que reduzem a base previdenciária. Não informe INSS aqui: ele será calculado.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0);
    private readonly CampoTextoViewModel _pensao = Moeda("Pensão alimentícia (R$)", "Pensão compulsória mensal e dedutível do IRRF.");
    private readonly CampoTextoViewModel _compulsorios = Moeda("Outros compulsórios (R$)", "Outros descontos compulsórios usados na margem. Não repita INSS, IRRF, pensão ou consignados. Benefícios e descontos voluntários podem ter tratamento próprio pelo banco.");
    private readonly CampoTextoViewModel _existentes = Moeda("Consignados atuais (R$)", "Total mensal das prestações já contratadas neste vínculo. Ocupam a margem estimada; não garantem elegibilidade para novo contrato.");
    private readonly CampoTextoViewModel _margem = new("Margem livre oficial (R$)", TipoCampo.Moeda, "", "Opcional: margem livre confirmada na plataforma/banco, já após contratos existentes. Vazio estima pela remuneração; zero informado significa sem margem.", permiteVazio: true)
    { MostrarDica = true, DicaEmLinha = "Vazio estima a margem; 0,00 informado significa sem margem livre." };
    private readonly CampoOpcaoViewModel _objetivo = new("Objetivo", [new("Valor desejado", ObjetivoCreditoTrabalhador.ValorDesejado), new("Valor pela margem", ObjetivoCreditoTrabalhador.LimitePelaMargem)], "Simular uma proposta ou estimar o crédito que cabe na margem. Não representa aprovação pelo banco.");
    private readonly CampoTextoViewModel _valor = Moeda("Crédito desejado (R$)", "Valor antes dos custos descontados na liberação. IOF e outros custos financiados serão adicionados ao principal.");
    private readonly CampoTextoViewModel _prazo = Inteiro("Parcelas (meses)", 24, "De 1 a 120 meses, sem carência. Limite técnico da simulação; confirme o prazo disponível na proposta.");
    private readonly CampoTextoViewModel _juros = new("Juros (% ao mês)", TipoCampo.Numero, "0", "Taxa mensal da proposta, de 0 a 20%, com até seis casas decimais. Não confunda com CET nem taxa anual. Zero simula ausência de juros.")
    { MostrarDica = true, DicaEmLinha = "Informe a taxa mensal da proposta; não use taxa anual nem CET." };
    private readonly CampoOpcaoViewModel _modoIof = new("Cálculo do IOF", [new("Automático (estimado)", true), new("Informado pelo banco", false)]);
    private readonly CampoTextoViewModel _dataLiberacao = new("Data de liberação", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Data em que o crédito será disponibilizado. Usada na estimativa do IOF.");
    private readonly CampoTextoViewModel _primeiroVencimento = new("Primeiro vencimento", TipoCampo.Data, DateTime.Today.AddMonths(1).ToString("dd/MM/yyyy", Cultura), "Data sugerida: um mês após hoje. Confirme o vencimento contratual; mês da folha não é necessariamente mês do repasse ao banco.");
    private readonly CampoTextoViewModel _iof = Moeda("IOF financiado (R$)", "IOF da proposta incluído no financiamento. Usado somente no modo Informado pelo banco.");
    private readonly CampoTextoViewModel _outros = Moeda("Custos financiados (R$)", "Opcional: seguros e demais custos incluídos no financiamento, exceto o IOF informado separadamente.");
    private readonly CampoTextoViewModel _liberacao = Moeda("Custos na liberação (R$)", "Opcional: custos descontados do crédito recebido. Não repita valores já informados como financiados.");

    public CalculadoraCreditoTrabalhador(ISimularDemonstrativoUseCase<SimularCreditoTrabalhadorRequest> simulador)
    {
        _simulador = simulador;
        _objetivo.AoAlterar = () => _valor.Visivel = _objetivo.Valor<ObjetivoCreditoTrabalhador>() == ObjetivoCreditoTrabalhador.ValorDesejado;
        _modoIof.AoAlterar = AtualizarCamposIof;
        AtualizarCamposIof();
    }
    private void AtualizarCamposIof()
    {
        var automatico = _modoIof.Valor<bool>();
        _iof.Visivel = !automatico;
        _dataLiberacao.Visivel = automatico;
        _primeiroVencimento.Visivel = automatico;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        // Históricos anteriores à estimativa automática preservam o IOF informado, inclusive zero.
        var compativeis = new Dictionary<string, string>(valores);
        compativeis.TryAdd("Cálculo do IOF", "Informado pelo banco");
        base.ImportarCampos(compativeis);
    }
    public override string Titulo => "Crédito do Trabalhador";
    public override string Descricao => "Simule consignado com parcelas mensais, juros e custos da proposta. A margem de referência é 35% da remuneração habitual disponível; informe a margem livre oficial, se conhecida. Valores e custo efetivo são estimados e dependem da análise do banco.";
    public override string InstrucaoInicial => "No modo automático, IOF financiado: 0,38% + 0,0082% ao dia por amortização, limitado a 365 dias. Confira as datas sugeridas; vencimentos seguintes no mesmo dia mensal, sem ajuste por feriados. Datas usadas apenas no IOF; juros e custo efetivo seguem períodos mensais iguais. Custos opcionais: zero; margem oficial: vazio para estimar.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _salario, _descontosCp, _dependentes, _pensao, _compulsorios,
        _existentes, _margem, _objetivo, _valor, _prazo, _juros, _modoIof, _dataLiberacao, _primeiroVencimento, _iof, _outros, _liberacao];
    public override string NomeArquivoPdf => $"credito-trabalhador-{_competencia.Valor.Replace('/', '-')}.pdf";
    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularCreditoTrabalhadorRequest(leitor.Competencia(_competencia), leitor.Moeda(_salario),
            leitor.Moeda(_descontosCp), leitor.Inteiro(_dependentes), leitor.Moeda(_pensao), leitor.Moeda(_compulsorios),
            new EntradaCreditoTrabalhador(_objetivo.Valor<ObjetivoCreditoTrabalhador>(), _valor.Visivel ? leitor.Moeda(_valor) : 0m,
                leitor.Inteiro(_prazo), leitor.Numero(_juros), _iof.Visivel ? leitor.Moeda(_iof) : 0m, leitor.Moeda(_outros), leitor.Moeda(_liberacao),
                0m, leitor.Moeda(_existentes), string.IsNullOrWhiteSpace(_margem.Valor) ? null : leitor.Moeda(_margem),
                _modoIof.Valor<bool>(), _dataLiberacao.Visivel ? leitor.Data(_dataLiberacao) : null,
                _primeiroVencimento.Visivel ? leitor.Data(_primeiroVencimento) : null)), cancellationToken);
}
