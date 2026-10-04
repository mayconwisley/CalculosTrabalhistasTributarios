using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSaqueAniversario(ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _saldo = Moeda("Saldo do FGTS", "Soma dos saldos de todas as contas, ativas e inativas, como no aplicativo FGTS.");
    private readonly CampoOpcaoViewModel _mes = new("Mês de aniversário",
        [.. Enumerable.Range(1, 12).Select(mes => new OpcaoCampo(Cultura.TextInfo.ToTitleCase(Cultura.DateTimeFormat.GetMonthName(mes)), mes))],
        "Mês de nascimento: o saque fica disponível do 1º dia útil dele até o fim do segundo mês seguinte.");

    public override string Titulo => "Saque-aniversário do FGTS";
    public override string Descricao => "Calcule quanto do FGTS pode ser sacado no mês do aniversário e o que muda numa dispensa.";
    public override string InstrucaoInicial => "Informe o saldo do FGTS e o mês de aniversário e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_saldo, _mes];
    public override string NomeArquivoPdf => "saque-aniversario-fgts.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularSaqueAniversarioRequest(leitor.Moeda(_saldo), _mes.Valor<int>()), cancellationToken);
}
