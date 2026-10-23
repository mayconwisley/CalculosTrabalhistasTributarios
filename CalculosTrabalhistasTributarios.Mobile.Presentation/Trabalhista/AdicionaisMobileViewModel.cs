using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class AdicionaisMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularAdicionaisRequest> _simulador;

    public override string Titulo => "Insalubridade e periculosidade";
    public override string Descricao => "Calcule os adicionais e a estimativa do salário líquido do mês.";

    public AdicionaisMobileViewModel(ISimularDemonstrativoUseCase<SimularAdicionaisRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<AdicionaisMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Define o salário mínimo e as tabelas de INSS e IRRF.");
        Adicionar("salario", "Salário-base (R$)", TipoCampoMobile.Moeda, "0,00", "Sem gratificações, prêmios ou outros adicionais.");
        Adicionar("grau", "Insalubridade", TipoCampoMobile.Opcao, dica: "Grau definido por laudo técnico, conforme a NR-15.",
            opcoes: ["Não há", "Grau mínimo (10%)", "Grau médio (20%)", "Grau máximo (40%)"]);
        Adicionar("base", "Base da insalubridade", TipoCampoMobile.Opcao,
            dica: "Salário mínimo, salvo previsão diferente em convenção coletiva, como o piso da categoria.",
            opcoes: ["Salário mínimo", "Salário", "Valor informado"]);
        Adicionar("valorBase", "Valor da base (R$)", TipoCampoMobile.Moeda, "0,00",
            "Piso da categoria ou outra base prevista em convenção coletiva.");
        Adicionar("periculosidade", "Periculosidade (30%)", TipoCampoMobile.SimNao,
            dica: "Para atividade perigosa enquadrada na CLT, art. 193.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Campo("grau").Indice = 2;
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "grau" or "base") Atualizar();
    }

    private void Atualizar()
    {
        Campo("base").Visivel = Opcao("grau") != 0;
        Campo("valorBase").Visivel = Campo("base").Visivel && Opcao("base") == 2;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var grau = Opcao("grau") switch
        {
            0 => GrauInsalubridade.Nenhum,
            1 => GrauInsalubridade.Minimo,
            2 => GrauInsalubridade.Medio,
            _ => GrauInsalubridade.Maximo
        };
        var baseInsalubridade = (BaseInsalubridade)Opcao("base");
        return _simulador.ExecutarAsync(new SimularAdicionaisRequest(Competencia("competencia"), Numero("salario"),
            grau, baseInsalubridade, Campo("valorBase").Visivel ? Numero("valorBase") : 0m,
            Sim("periculosidade"), Inteiro("dependentes")), cancellationToken);
    }
}
