using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class HoleriteMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularHoleriteRequest> _simulador;

    public override string Titulo => "Holerite do mês";
    public override string Descricao => "Monte a folha do mês com salário, horas, comissões, descontos, benefícios e memória das incidências. A quitação do banco de horas chega pelo botão Usar no holerite do banco de horas.";

    public HoleriteMobileViewModel(ISimularDemonstrativoUseCase<SimularHoleriteRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<HoleriteMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("remuneracao", "Remuneração");
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Define domingos, feriados e tabelas de INSS, IRRF e salário-família.");
        Adicionar("salario", "Salário-base mensal (R$)", TipoCampoMobile.Moeda, "0,00",
            "Sem adicionais. Use 0,00 para comissionista puro e informe as comissões abaixo.");
        Adicionar("insalubridade", "Insalubridade", TipoCampoMobile.Opcao,
            dica: "Percentual sobre o mínimo conforme laudo. Com periculosidade, aplica-se o adicional maior.",
            opcoes: ["Não há", "Grau mínimo (10%)", "Grau médio (20%)", "Grau máximo (40%)"]);
        Adicionar("periculosidade", "Periculosidade (30%)", TipoCampoMobile.SimNao,
            dica: "30% do salário; com insalubridade, aplica-se o adicional maior.");
        Adicionar("outrosProventos", "Proventos tributáveis (R$)", TipoCampoMobile.Moeda, "0,00",
            "Com INSS, IRRF e FGTS. Se incluir comissões aqui, acrescente o DSR e não as repita abaixo.");
        Adicionar("premios", "Prêmios: só IRRF (R$)", TipoCampoMobile.Moeda, "0,00",
            "Prêmios por desempenho superior ao esperado, sem INSS ou FGTS.");
        Adicionar("naoTributaveis", "Proventos não tributáveis (R$)", TipoCampoMobile.Moeda, "0,00",
            "Ajuda de custo, diárias e reembolsos; apenas somam no líquido.");

        Secao("jornada", "Horas e jornada");
        Adicionar("divisor", "Divisor de horas", TipoCampoMobile.Numero, "220", "220 para 44 h/semana; 200 para 40; 180 para 36; 150 para 30.");
        Adicionar("horas1", "Horas extras: faixa 1", TipoCampoMobile.Horas, "0:00", "Exemplo: 10:30 ou 10,5 horas decimais.");
        Adicionar("percentual1", "Adicional da faixa 1 (%)", TipoCampoMobile.Numero, "50", "Mínimo legal de 50%; confira a convenção.");
        Adicionar("horas2", "Horas extras: faixa 2", TipoCampoMobile.Horas, "0:00", "Horas com o segundo adicional.");
        Adicionar("percentual2", "Adicional da faixa 2 (%)", TipoCampoMobile.Numero, "100", "Comumente usado para domingos e feriados.");
        Adicionar("trabalho", "Trabalho noturno", TipoCampoMobile.Opcao,
            dica: "Urbano: hora reduzida; rural: sem redução. O adicional sugerido muda conforme a opção.",
            opcoes: ["Urbano (22h às 5h)", "Rural"]);
        Adicionar("horasNoturnas", "Horas noturnas de relógio", TipoCampoMobile.Horas, "0:00",
            "Não repita aqui as horas extras noturnas.");
        Adicionar("percentualNoturno", "Adicional noturno (%)", TipoCampoMobile.Numero, "20",
            "Mínimo de 20% no urbano e 25% no rural.");
        Adicionar("extrasNoturnas", "Horas extras noturnas de relógio", TipoCampoMobile.Horas, "0:00",
            "Não repita nas faixas de horas extras ou nas horas noturnas comuns.");
        Adicionar("feriados", "Feriados em dias úteis no mês", TipoCampoMobile.Inteiro, "0", "Usados no DSR.");
        Adicionar("intervaloIntra", "Pausa suprimida (h)", TipoCampoMobile.Horas, "0:00",
            "Intervalo intrajornada não concedido, em horas:minutos. Não repita nas horas extras.");
        Adicionar("intervaloInter", "Interjornada faltante (h)", TipoCampoMobile.Horas, "0:00",
            "Tempo faltante para 11 horas entre jornadas, em horas:minutos.");

        Secao("comissoesSecao", "Comissões e DSR");
        Adicionar("comissoes", "Comissões do mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Sem DSR, exceto se marcar que o valor já o inclui.");
        Adicionar("comissoesIncluemDsr", "Comissões já incluem DSR?", TipoCampoMobile.SimNao);
        Adicionar("diasComissoesManuais", "Informar dias das comissões?", TipoCampoMobile.SimNao,
            dica: "Para escala ou período parcial. Afeta apenas o DSR das comissões.");
        Adicionar("diasUteisComissoes", "Dias úteis das comissões", TipoCampoMobile.Inteiro, "0");
        Adicionar("diasDescansoComissoes", "Repousos previstos das comissões", TipoCampoMobile.Inteiro, "0",
            "Inclua os repousos perdidos; informe a perda separadamente abaixo.");
        Adicionar("pisoComissoes", "Garantia mínima das comissões (R$)", TipoCampoMobile.Moeda, "0,00",
            "Use 0,00 para o mínimo nacional no mês cheio; informe o piso do período quando necessário.");

        Secao("faltasSecao", "Faltas e atrasos");
        Adicionar("faltas", "Faltas injustificadas (dias)", TipoCampoMobile.Inteiro, "0");
        Adicionar("descansos", "Descansos perdidos", TipoCampoMobile.Inteiro, "0",
            "Domingos e feriados perdidos nas semanas com falta injustificada.");
        Adicionar("atrasos", "Atrasos e saídas antecipadas (h)", TipoCampoMobile.Horas, "0:00");

        Secao("impostos", "IRRF, pensão e salário-família");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Adicionar("pensaoBase", "Pensão alimentícia", TipoCampoMobile.Opcao,
            dica: "Base definida na decisão ou no acordo.",
            opcoes: ["Não há", "% do líquido", "% do bruto", "Valor informado"]);
        Adicionar("pensaoPercentual", "Pensão (%)", TipoCampoMobile.Numero, "0",
            "Percentual definido na decisão ou no acordo, de 0 a 100. Líquido: rendimentos menos INSS e IRRF.");
        Adicionar("pensaoValor", "Valor da pensão (R$)", TipoCampoMobile.Moeda, "0,00", "Pensão mensal definida na decisão ou no acordo.");
        Adicionar("previdencia", "Previdência complementar (R$)", TipoCampoMobile.Moeda, "0,00",
            "Contribuição do trabalhador ao PGBL, fundo de pensão ou Fapi; deduzida do IRRF nas deduções legais.");
        Adicionar("filhos", "Filhos para salário-família", TipoCampoMobile.Inteiro, "0",
            "Até 14 anos ou inválidos; não confunda com dependentes de IRRF.");

        Secao("descontosSecao", "Benefícios e descontos");
        Adicionar("valeTransporte", "Custo do vale-transporte (R$)", TipoCampoMobile.Moeda, "0,00",
            "Custo das passagens; desconto do empregado limitado a 6% do salário-base.");
        Adicionar("adiantamento", "Adiantamento salarial (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("planoSaude", "Plano de saúde (R$)", TipoCampoMobile.Moeda, "0,00",
            "Mensalidade descontada do empregado; reduz apenas o líquido.");
        Adicionar("coparticipacao", "Coparticipação do plano (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("valeAlimentacao", "Vale-refeição/alimentação (R$)", TipoCampoMobile.Moeda, "0,00",
            "Participação do empregado, descontada do líquido.");
        Adicionar("outrosDescontos", "Outros descontos (R$)", TipoCampoMobile.Moeda, "0,00",
            "Consignado e mensalidades; não reduzem bases de INSS, IRRF ou FGTS.");
        Atualizar();
    }

    // O fechamento vai para o holerite da competência do fim do ciclo, com o salário e o divisor usados no banco.
    protected override void PreencherComQuitacao(QuitacaoBancoHoras quitacao)
    {
        Campo("competencia").Valor = quitacao.FimCiclo.ToString("MM/yyyy", Cultura);
        Campo("salario").Valor = quitacao.SalarioReferencia.ToString("N2", Cultura);
        Campo("divisor").Valor = quitacao.Divisor.ToString("N2", Cultura);
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "diasComissoesManuais" or "pensaoBase") Atualizar();
        if (campo.Chave != "trabalho") return;
        var percentual = Campo("percentualNoturno");
        if (campo.Indice == 1 && percentual.Valor.Trim() == "20") percentual.Valor = "25";
        else if (campo.Indice == 0 && percentual.Valor.Trim() == "25") percentual.Valor = "20";
    }

    private void Atualizar()
    {
        var diasManuais = Sim("diasComissoesManuais");
        Campo("diasUteisComissoes").Visivel = Campo("diasDescansoComissoes").Visivel = diasManuais;
        var forma = Opcao("pensaoBase");
        Campo("pensaoPercentual").Visivel = forma is 1 or 2;
        Campo("pensaoValor").Visivel = forma == 3;
    }

    private RegraPensao? PensaoHolerite() => Opcao("pensaoBase") switch
    {
        1 => new(BasePensao.RendimentosLiquidos, Numero("pensaoPercentual"), 0m),
        2 => new(BasePensao.RendimentosBrutos, Numero("pensaoPercentual"), 0m),
        3 => new(BasePensao.ValorFixo, 0m, Numero("pensaoValor")),
        _ => null
    };

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var grau = Opcao("insalubridade") switch
        {
            0 => GrauInsalubridade.Nenhum,
            1 => GrauInsalubridade.Minimo,
            2 => GrauInsalubridade.Medio,
            _ => GrauInsalubridade.Maximo
        };
        var diasManuais = Sim("diasComissoesManuais");
        var pedido = new SimularHoleriteRequest(
            Competencia: Competencia("competencia"), Salario: Numero("salario"), Insalubridade: grau,
            Periculosidade: Sim("periculosidade"), OutrosProventos: Numero("outrosProventos"),
            Divisor: Numero("divisor"), HorasFaixa1: Horas("horas1"), PercentualFaixa1: Numero("percentual1"),
            HorasFaixa2: Horas("horas2"), PercentualFaixa2: Numero("percentual2"),
            HorasNoturnas: Horas("horasNoturnas"), PercentualNoturno: Numero("percentualNoturno"),
            Feriados: Inteiro("feriados"), Faltas: Inteiro("faltas"), DescansosPerdidos: Inteiro("descansos"),
            HorasAtraso: Horas("atrasos"), Dependentes: Inteiro("dependentes"), Filhos: Inteiro("filhos"),
            Pensao: PensaoHolerite(), CustoValeTransporte: Numero("valeTransporte"),
            Adiantamento: Numero("adiantamento"), OutrosDescontos: Numero("outrosDescontos"),
            Rural: Opcao("trabalho") == 1, HorasExtrasNoturnas: Horas("extrasNoturnas"),
            Premios: Numero("premios"), ProventosNaoTributaveis: Numero("naoTributaveis"),
            PrevidenciaComplementar: Numero("previdencia"), Comissoes: Numero("comissoes"),
            ComissoesIncluemDsr: Sim("comissoesIncluemDsr"),
            DiasUteisComissoes: diasManuais ? Inteiro("diasUteisComissoes") : null,
            DiasDescansoComissoes: diasManuais ? Inteiro("diasDescansoComissoes") : null,
            PisoGarantidoComissoes: Numero("pisoComissoes"),
            HorasIntervaloIntrajornada: Horas("intervaloIntra"),
            HorasIntervaloInterjornada: Horas("intervaloInter"), QuitacaoBancoHoras: LerQuitacao(),
            PlanoSaude: Numero("planoSaude"), CoparticipacaoSaude: Numero("coparticipacao"),
            ValeAlimentacao: Numero("valeAlimentacao"));
        return _simulador.ExecutarAsync(pedido, cancellationToken);
    }
}
