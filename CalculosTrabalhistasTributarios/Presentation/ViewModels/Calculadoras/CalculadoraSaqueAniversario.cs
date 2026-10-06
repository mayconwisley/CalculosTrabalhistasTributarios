using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSaqueAniversario(ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _saldo = Moeda("Saldo do FGTS", "Saldo antes do saque simulado, incluindo a garantia bloqueada e excluindo a multa rescisória. Não desconte novamente saques já debitados.");
    private readonly CampoTextoViewModel _comprometida = Moeda("Parcela ao banco (R$)", "Opcional: zero se não houver antecipação deste saque. Informe o valor do saque simulado cedido ao banco, conforme contrato/extrato; não o total recebido nos empréstimos nem o saldo bloqueado.");
    private readonly CampoOpcaoViewModel _mes = new("Mês de aniversário",
        [.. Enumerable.Range(1, 12).Select(mes => new OpcaoCampo(Cultura.TextInfo.ToTitleCase(Cultura.DateTimeFormat.GetMonthName(mes)), mes))],
        "Mês de nascimento: o saque fica disponível do 1º dia útil dele até o fim do segundo mês seguinte.");

    public override string Titulo => "Saque-aniversário do FGTS";
    public override string Descricao => "Informe o saldo antes do saque, incluindo a garantia bloqueada e excluindo a multa rescisória. " +
        "Parcela ao banco (opcional): valor deste saque anual cedido em empréstimos anteriores, conforme contrato/extrato; zero se não houver. " +
        "Não use o total recebido nos empréstimos nem o saldo bloqueado como parcela.";
    public override string InstrucaoInicial => "Informe o saldo, o mês e a parcela deste saque comprometida com empréstimos anteriores. Consulte o contrato ou extrato para identificar o valor cedido ao banco.";
    public override IReadOnlyList<CampoViewModel> Campos => [_saldo, _mes, _comprometida];
    public override string NomeArquivoPdf => "saque-aniversario-fgts.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularSaqueAniversarioRequest(leitor.Moeda(_saldo), _mes.Valor<int>(), leitor.Moeda(_comprometida)), cancellationToken);

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        // Históricos anteriores não tinham antecipações, inclusive ao reabrir na mesma janela.
        _comprometida.Valor = "0,00";
        base.ImportarCampos(valores);
    }
}
