using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class ComissoesMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularComissoesRequest> _simulador;

    public override string Titulo => "Comissões e DSR";
    public override string Descricao => "Apure o repouso sobre comissões e o impacto no INSS, IRRF, FGTS e líquido.";

    public ComissoesMobileViewModel(ISimularDemonstrativoUseCase<SimularComissoesRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<ComissoesMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura));
        Adicionar("salario", "Salário fixo (R$)", TipoCampoMobile.Moeda, "0,00", "Use 0,00 para comissionista puro.");
        Adicionar("comissoes", "Valor das comissões (R$)", TipoCampoMobile.Moeda, "0,00",
            "Informe sem DSR, exceto se marcar que o valor já inclui o repouso.");
        Adicionar("incluiDsr", "Valor já inclui DSR?", TipoCampoMobile.SimNao,
            dica: "Marque quando o valor informado já contém comissões e repouso remunerado.");
        Adicionar("diasManuais", "Informar dias do período?", TipoCampoMobile.SimNao,
            dica: "Use para período parcial, afastamento, escala diferente ou regra coletiva.");
        Adicionar("feriados", "Feriados em dias úteis", TipoCampoMobile.Inteiro, "0", "Não inclua feriados que caem no domingo.");
        Adicionar("diasUteis", "Dias úteis considerados", TipoCampoMobile.Inteiro, "0",
            "Dias úteis da apuração. Não desconte os repousos perdidos aqui.");
        Adicionar("diasDescanso", "Repousos previstos", TipoCampoMobile.Inteiro, "0",
            "Repousos e feriados previstos, inclusive os perdidos.");
        Adicionar("descansosPerdidos", "Repousos perdidos", TipoCampoMobile.Inteiro, "0",
            "Sem remuneração por falta injustificada, conforme a apuração semanal.");
        Adicionar("pisoGarantido", "Garantia mínima do período (R$)", TipoCampoMobile.Moeda, "0,00",
            "Use 0,00 para o mínimo nacional no mês completo; informe o piso aplicável em outros casos.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "diasManuais") Atualizar();
    }

    private void Atualizar()
    {
        var manuais = Sim("diasManuais");
        Campo("feriados").Visivel = !manuais;
        Campo("diasUteis").Visivel = Campo("diasDescanso").Visivel = manuais;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var manuais = Sim("diasManuais");
        return _simulador.ExecutarAsync(new SimularComissoesRequest(Competencia("competencia"), Numero("salario"),
            Numero("comissoes"), Sim("incluiDsr"), manuais ? 0 : Inteiro("feriados"),
            Inteiro("descansosPerdidos"), Inteiro("dependentes"), manuais ? Inteiro("diasUteis") : null,
            manuais ? Inteiro("diasDescanso") : null, Numero("pisoGarantido")), cancellationToken);
    }
}
