using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraComissoes : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularComissoesRequest> _simulador;

    private readonly CampoTextoViewModel _competencia = Competencia(dica: "Mês das comissões, dos repousos e das tabelas de INSS e IRRF (MM/AAAA).");
    private readonly CampoTextoViewModel _salario = Moeda("Salário fixo", "Opcional; use 0,00 para comissionista puro. Entra na estimativa de INSS, IRRF e líquido.");
    private readonly CampoTextoViewModel _comissoes = Moeda("Valor das comissões", "Informe apenas as comissões, sem o DSR, salvo se marcar que o valor já inclui o repouso.");
    private readonly CampoOpcaoViewModel _incluiDsr = CampoOpcaoViewModel.SimNao("Valor já inclui DSR?", false,
        "Marque Sim somente quando o valor informado já contém comissões e repouso remunerado; o total não será somado duas vezes.");
    private readonly CampoOpcaoViewModel _diasManuais = CampoOpcaoViewModel.SimNao("Informar dias do período?", false,
        "Use para admissão, desligamento, afastamento, escala com folga em outro dia ou regra coletiva diferente do calendário padrão.");
    private readonly CampoTextoViewModel _feriados = Inteiro("Feriados em dias úteis", 0, "Feriados que não caem no domingo; o sábado conta como dia útil na contagem automática.");
    private readonly CampoTextoViewModel _diasUteis = Inteiro("Dias úteis considerados", 0, "Dias úteis do período de apuração; informe também os repousos previstos. Não desconte os repousos perdidos aqui.");
    private readonly CampoTextoViewModel _diasDescanso = Inteiro("Repousos previstos", 0, "Dias de repouso e feriados previstos no período, inclusive os perdidos; estes são informados no campo seguinte.");
    private readonly CampoTextoViewModel _descansosPerdidos = Inteiro("Repousos perdidos", 0, "Quantidade de repousos e feriados sem remuneração por falta injustificada, conforme a apuração da semana.");
    private readonly CampoTextoViewModel _pisoGarantido = Moeda("Garantia mínima do período", "Deixe 0,00 para usar o salário mínimo nacional no mês completo. Informe o piso da categoria, ou o valor proporcional aplicável quando informar os dias do período.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes (IRRF)", 0, "Dependentes para a dedução do IRRF.");

    public CalculadoraComissoes(ISimularDemonstrativoUseCase<SimularComissoesRequest> simulador)
    {
        _simulador = simulador;
        _diasManuais.AoAlterar = AtualizarCamposDias;
        AtualizarCamposDias();
    }

    public override string Titulo => "Comissões e DSR";
    public override string Descricao => "Apure o repouso sobre comissões e o impacto no INSS, no IRRF, no FGTS e no líquido.";
    public override string InstrucaoInicial => "Informe as comissões do mês. Acrescente salário fixo e ajuste os dias do período quando necessário.";
    public override IReadOnlyList<CampoViewModel> Campos =>
        [_competencia, _salario, _comissoes, _incluiDsr, _diasManuais, _feriados, _diasUteis, _diasDescanso, _descansosPerdidos, _pisoGarantido, _dependentes];
    public override string NomeArquivoPdf => $"comissoes-dsr-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularComissoesRequest(
            leitor.Competencia(_competencia), leitor.Moeda(_salario), leitor.Moeda(_comissoes), _incluiDsr.Valor<bool>(),
            _diasManuais.Valor<bool>() ? 0 : leitor.Inteiro(_feriados), leitor.Inteiro(_descansosPerdidos), leitor.Inteiro(_dependentes),
            _diasManuais.Valor<bool>() ? leitor.Inteiro(_diasUteis) : null,
            _diasManuais.Valor<bool>() ? leitor.Inteiro(_diasDescanso) : null,
            leitor.Moeda(_pisoGarantido)), cancellationToken);

    private void AtualizarCamposDias()
    {
        var manuais = _diasManuais.Valor<bool>();
        _feriados.Visivel = !manuais;
        _diasUteis.Visivel = _diasDescanso.Visivel = manuais;
    }
}
