using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class HorasExtrasMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularHorasExtrasRequest> _simulador;
    public override string Titulo => "Horas extras e adicionais";
    public override string Descricao => "Horas extras, adicional noturno e reflexo no DSR, com salário líquido do mês. Horas aceitam 10:30 ou 10,5.";

    public HorasExtrasMobileViewModel(ISimularDemonstrativoUseCase<SimularHorasExtrasRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<HorasExtrasMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência das horas (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura));
        Adicionar("salario", "Salário base (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("adicionais", "Adicionais salariais (R$)", TipoCampoMobile.Moeda, "0,00", "Adicionais fixos que integram o valor da hora.");
        Adicionar("divisor", "Divisor de horas", TipoCampoMobile.Numero, "220", "Exemplos: 220 para 44 h/semana, 200 para 40.");
        Adicionar("horas1", "Horas extras da faixa 1", TipoCampoMobile.Horas, "0:00");
        Adicionar("percentual1", "Adicional da faixa 1 (%)", TipoCampoMobile.Numero, "50");
        Adicionar("horas2", "Horas extras da faixa 2", TipoCampoMobile.Horas, "0:00");
        Adicionar("percentual2", "Adicional da faixa 2 (%)", TipoCampoMobile.Numero, "100");
        Adicionar("trabalho", "Trabalho noturno", TipoCampoMobile.Opcao, opcoes: ["Urbano (22h às 5h)", "Rural"]);
        Adicionar("noturnas", "Horas noturnas de relógio", TipoCampoMobile.Horas, "0:00", "Não inclua aqui as horas extras noturnas.");
        Adicionar("percentualNoturno", "Adicional noturno (%)", TipoCampoMobile.Numero, "20");
        Adicionar("extrasNoturnas", "Horas extras noturnas de relógio", TipoCampoMobile.Horas, "0:00", "Não repita nas faixas de horas extras.");
        Adicionar("feriados", "Feriados em dias úteis no mês", TipoCampoMobile.Inteiro, "0");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave != "trabalho") return;
        var percentual = Campo("percentualNoturno");
        if (campo.Indice == 1 && percentual.Valor.Trim() == "20") percentual.Valor = "25";
        else if (campo.Indice == 0 && percentual.Valor.Trim() == "25") percentual.Valor = "20";
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularHorasExtrasRequest(Competencia("competencia"), Numero("salario"), Numero("divisor"),
            Horas("horas1"), Numero("percentual1"), Horas("horas2"), Numero("percentual2"), Horas("noturnas"),
            Numero("percentualNoturno"), Inteiro("feriados"), Inteiro("dependentes"), Numero("adicionais"),
            Horas("extrasNoturnas"), Opcao("trabalho") == 1), cancellationToken);
}
