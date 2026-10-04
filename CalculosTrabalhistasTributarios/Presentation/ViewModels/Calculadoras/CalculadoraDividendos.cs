using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraDividendos(ISimularDemonstrativoUseCase<SimularDividendosRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia(dica: "Mês dos pagamentos (MM/AAAA), desde 01/2026.");
    private readonly CampoTextoViewModel _valor = Moeda("Dividendos no mês", "Total pago no mês pela mesma empresa à mesma pessoa, somando todos os pagamentos.");
    private readonly CampoTextoViewModel _transicao = Moeda("Lucros até 2025", "Parte de lucros apurados até 2025, com distribuição aprovada até 31/12/2025, que não sofre retenção.");
    private readonly CampoTextoViewModel _jaRetido = Moeda("IRRF já retido no mês", "Imposto retido em pagamentos anteriores do mesmo mês, descontado do imposto refeito sobre o total.");
    private readonly CampoOpcaoViewModel _residencia = new("Beneficiário",
        [
            new("Residente no Brasil", false),
            new("Residente no exterior", true)
        ], "No exterior, a retenção de 10% vale para qualquer valor, sem o limite de R$ 50 mil.");

    public override string Titulo => "Dividendos";
    public override string Descricao => "Retenção de 10% sobre lucros e dividendos acima de R$ 50 mil no mês, desde 2026 (Lei 15.270/2025).";
    public override string InstrucaoInicial => "Informe o mês, o total pago pela empresa e o que já foi retido e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _valor, _transicao, _jaRetido, _residencia];
    public override string NomeArquivoPdf => $"dividendos-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularDividendosRequest(leitor.Competencia(_competencia), leitor.Moeda(_valor), leitor.Moeda(_transicao), leitor.Moeda(_jaRetido), _residencia.Valor<bool>()), cancellationToken);
}
