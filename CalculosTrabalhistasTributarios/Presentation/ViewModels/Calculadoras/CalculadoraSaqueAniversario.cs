using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSaqueAniversario(ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _saldo = Moeda("Saldo do extrato (R$)", "Copie o saldo do extrato e indique se ele já inclui a garantia. Informe separadamente qualquer multa que esteja dentro desse saldo.");
    private readonly CampoOpcaoViewModel _incluiGarantia = new("Saldo inclui garantia", [new("Não (saldo livre)", false), new("Sim (saldo total)", true)], "Selecione Sim se o saldo copiado já inclui o valor bloqueado; a garantia não será somada novamente.");
    private readonly CampoTextoViewModel _garantia = Moeda("Garantia bloqueada (R$)", "Opcional: saldo bloqueado como garantia de antecipações. A calculadora soma esse valor somente se ainda não estiver no saldo informado; não é o valor recebido no empréstimo.");
    private readonly CampoTextoViewModel _multa = Moeda("Multa incluída (R$)", "Opcional: somente a multa rescisória que está incluída no saldo do extrato informado. A calculadora a exclui da base. Se a multa já estiver separada no extrato, deixe zero.");
    private readonly CampoTextoViewModel _comprometida = Moeda("Parcela ao banco (R$)", "Opcional: zero se não houver antecipação deste saque. Informe o valor do saque simulado cedido ao banco, conforme contrato/extrato; não o total recebido nos empréstimos nem o saldo bloqueado.");
    private readonly CampoTextoViewModel _dataAnalise = new("Data da análise", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Data de referência para as regras de nova contratação, desde 01/11/2025.");
    private readonly CampoOpcaoViewModel _carencia = Confirmacao("Adesão há 90 dias", "Confirme se a adesão ao saque-aniversário ocorreu há pelo menos 90 dias. Não significa data do último empréstimo.");
    private readonly CampoOpcaoViewModel _contratoNovo = Confirmacao("Contrato desde 11/2025", "A antecipação do próximo saque foi contratada a partir de 01/11/2025? Confira esse contrato, não apenas a existência de outro empréstimo recente. Contratos anteriores têm tratamento de transição próprio.");
    private readonly CampoOpcaoViewModel _proximoCedido = Confirmacao("Próximo saque cedido", "O próximo saque anual já está antecipado em contrato ativo? Confira a competência com o banco. O valor bloqueado não identifica os anos cedidos.");
    private readonly CampoOpcaoViewModel _mes = new("Mês de aniversário",
        [.. Enumerable.Range(1, 12).Select(mes => new OpcaoCampo(Cultura.TextInfo.ToTitleCase(Cultura.DateTimeFormat.GetMonthName(mes)), mes))],
        "Mês de nascimento: o saque fica disponível do 1º dia útil dele até o fim do segundo mês seguinte.");

    public override string Titulo => "Saque-aniversário do FGTS";
    public override string Descricao => "Copie o saldo e a garantia bloqueada do extrato em campos separados. Indique se o saldo já inclui a garantia: a calculadora soma somente quando necessário. " +
        "A análise de novo empréstimo considera carência e situação do próximo saque; saldo livre não garante aprovação. Use Não informado quando não souber quais parcelas estão cedidas.";
    public override string InstrucaoInicial => "Preencha os valores do extrato, o aniversário e as confirmações para nova antecipação. A simulação distingue o saque anual da possibilidade de contratar outro empréstimo.";
    public override IReadOnlyList<CampoViewModel> Campos => [_saldo, _incluiGarantia, _garantia, _multa, _mes, _comprometida,
        _dataAnalise, _carencia, _contratoNovo, _proximoCedido];
    protected override IReadOnlyDictionary<string, string> RotulosAnteriores { get; } =
        new Dictionary<string, string> { ["Saldo do FGTS"] = "Saldo do extrato (R$)" };
    public override string NomeArquivoPdf => "saque-aniversario-fgts.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularSaqueAniversarioRequest(leitor.Moeda(_saldo), _mes.Valor<int>(), leitor.Moeda(_comprometida),
            leitor.Moeda(_garantia), leitor.Moeda(_multa), _incluiGarantia.Valor<bool>(),
            new EntradaAnaliseAntecipacaoFgts(leitor.Data(_dataAnalise), _carencia.Valor<ConfirmacaoAntecipacaoFgts>(),
                _contratoNovo.Valor<ConfirmacaoAntecipacaoFgts>(), _proximoCedido.Valor<ConfirmacaoAntecipacaoFgts>())), cancellationToken);

    private static CampoOpcaoViewModel Confirmacao(string rotulo, string dica) => new(rotulo, [
        new("Não informado", ConfirmacaoAntecipacaoFgts.NaoInformado), new("Sim", ConfirmacaoAntecipacaoFgts.Sim),
        new("Não", ConfirmacaoAntecipacaoFgts.Nao)
    ], dica);

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        // Históricos anteriores não tinham antecipações, inclusive ao reabrir na mesma janela.
        _comprometida.Valor = "0,00";
        _garantia.Valor = "0,00";
        _multa.Valor = "0,00";
        _incluiGarantia.Selecionada = _incluiGarantia.Opcoes[valores.ContainsKey("Saldo do FGTS") ? 1 : 0];
        _dataAnalise.Valor = DateTime.Today.ToString("dd/MM/yyyy", Cultura);
        foreach (var campo in new[] { _carencia, _contratoNovo, _proximoCedido })
            campo.Selecionada = campo.Opcoes[0];
        base.ImportarCampos(valores);
    }
}
