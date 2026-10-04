using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>
/// Verbas rescisórias conforme o motivo do desligamento. O aviso prévio indenizado integra o tempo de serviço
/// (CLT, art. 487, § 1º) e gera avos de 13º e de férias.
/// </summary>
public static class CalculadoraRescisao
{
    public static Result<VerbasRescisorias> Calcular(ContratoRescindido c)
    {
        if (Validar(c) is { Falhou: true } invalido)
            return invalido.Erro;
        var diasNoMes = DiasNoMesDoDesligamento(c.Admissao, c.Desligamento);
        if (c.FaltasNoMes + c.SemanasComFalta > diasNoMes)
            return Erro.Validacao($"As faltas no mês e os DSR perdidos não podem passar dos {diasNoMes} dias trabalhados no mês do desligamento.");

        var saldo = Saldo(c, diasNoMes);
        var aviso = Aviso(c);
        var decimoTerceiro = DecimoTerceiro(c, aviso.Projecao);
        return new VerbasRescisorias(
            c,
            RegrasTrabalhistas.AnosCompletos(c.Admissao, c.Desligamento),
            saldo,
            aviso,
            Indenizacoes(c, aviso.Projecao),
            decimoTerceiro,
            Ferias(c, aviso.Projecao),
            Fgts(c, saldo, aviso, decimoTerceiro));
    }

    private static Result Validar(ContratoRescindido c)
    {
        if (c.Desligamento < c.Admissao)
            return Erro.Validacao("A data de desligamento deve ser igual ou posterior à data de admissão.");
        if (c.Salario < 0m || c.Medias < 0m || c.SaldoFgts < 0m || c.AdiantamentoDecimoTerceiro < 0m || c.FaltasPeriodoAtual < 0 || c.OutrosProventos < 0m || c.FaltasNoMes < 0
            || c.SemanasComFalta < 0 || c.OutrosDescontos < 0m || c.VerbasIndenizatorias < 0m)
            return Erro.Validacao("Os valores, as faltas e a quantidade de dependentes não podem ser negativos.");
        if (c.SemanasComFalta > 5)
            return Erro.Validacao("Um mês tem no máximo 5 semanas com falta.");
        // A compensação de descontos na rescisão é limitada a uma remuneração mensal (CLT, art. 477, § 5º).
        if (c.OutrosDescontos > c.Remuneracao)
            return Erro.Validacao($"Os outros descontos não podem passar de uma remuneração mensal (R$ {c.Remuneracao.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}): na rescisão, a compensação é limitada a esse valor (CLT, art. 477, § 5º).");
        if (c.Antecipada && (c.FimPrevistoContrato is not { } fim || fim <= c.Desligamento))
            return Erro.Validacao("Na rescisão antecipada, informe o fim previsto do contrato a prazo, posterior ao desligamento.");
        if (c.MesDataBase is < 1 or > 12)
            return Erro.Validacao("O mês da data-base deve estar entre 1 e 12.");
        if (c.PeriodosFeriasVencidas is < 0 or > 2)
            return Erro.Validacao("Informe de 0 a 2 períodos de férias vencidas.");
        if (c.DataPagamento is { } pagamento && pagamento < c.Desligamento)
            return Erro.Validacao("A data do pagamento não pode ser anterior ao desligamento.");
        if (c.Motivo == MotivoRescisao.PedidoDeDemissao && c.AvisoAplicavel == CumprimentoAvisoPrevio.Indenizado)
            return Erro.Validacao("No pedido de demissão não há aviso prévio indenizado: o empregado cumpre o aviso, é dispensado dele pelo empregador ou tem o valor descontado se não cumprir.");
        if (c.Motivo != MotivoRescisao.PedidoDeDemissao && c.AvisoAplicavel == CumprimentoAvisoPrevio.NaoCumpridoPeloEmpregado)
            return Erro.Validacao("O desconto do aviso prévio não cumprido só se aplica ao pedido de demissão.");
        return Result.Ok();
    }

    /// <summary>Dias de trabalho no mês do desligamento, no mês comercial de 30 dias.</summary>
    private static int DiasNoMesDoDesligamento(DateOnly admissao, DateOnly desligamento)
    {
        var mesmoMes = admissao.Year == desligamento.Year && admissao.Month == desligamento.Month;
        var ultimoDiaDoMes = desligamento.Day == DateTime.DaysInMonth(desligamento.Year, desligamento.Month);
        var ultimoDiaTrabalhado = ultimoDiaDoMes ? 30 : Math.Min(30, desligamento.Day);
        return mesmoMes ? Math.Max(1, ultimoDiaTrabalhado - Math.Min(30, admissao.Day) + 1) : ultimoDiaTrabalhado;
    }

    // As horas extras, os adicionais e as comissões do mês se somam ao saldo nas bases do INSS, do IRRF, da pensão e do FGTS.
    private static SaldoDeSalario Saldo(ContratoRescindido c, int diasNoMes)
    {
        var dias = diasNoMes - c.FaltasNoMes;
        var valor = Arredondar(c.Salario * dias / 30m);
        var dsrPerdido = Arredondar(c.Salario / 30m * c.SemanasComFalta);
        return new SaldoDeSalario(diasNoMes, dias, valor, valor - dsrPerdido + c.OutrosProventos, c.SemanasComFalta, dsrPerdido);
    }

    // No acordo, o aviso indenizado é pago pela metade (CLT, art. 484-A, I, a).
    private static AvisoPrevioRescisao Aviso(ContratoRescindido c)
    {
        var cumprimento = c.AvisoAplicavel;
        var proporcionais = RegrasTrabalhistas.DiasDeAvisoPrevio(c.Admissao, c.Desligamento);
        var indenizados = cumprimento == CumprimentoAvisoPrevio.Indenizado ? (c.Motivo == MotivoRescisao.Acordo ? proporcionais / 2m : proporcionais) : 0m;
        return new AvisoPrevioRescisao(
            cumprimento,
            proporcionais,
            indenizados,
            Arredondar(c.Remuneracao / 30m * indenizados),
            cumprimento == CumprimentoAvisoPrevio.NaoCumpridoPeloEmpregado ? Arredondar(c.Remuneracao) : 0m,
            c.Desligamento.AddDays((int)Math.Floor(indenizados)));
    }

    private static IndenizacoesRescisao Indenizacoes(ContratoRescindido c, DateOnly projecao)
    {
        // No contrato a prazo encerrado antes do fim, metade da remuneração dos dias que faltavam: paga pelo empregador
        // (CLT, art. 479) ou, no máximo, devida pelo empregado que pediu para sair (art. 480, § 1º).
        var diasRestantes = c.Antecipada ? c.FimPrevistoContrato!.Value.DayNumber - c.Desligamento.DayNumber : 0;
        var metadeDosDias = Arredondar(c.Remuneracao * diasRestantes / 60m);

        // Dispensa sem justa causa nos 30 dias que antecedem a data-base da categoria, contando a projeção do aviso:
        // indenização adicional de um salário (Lei 7.238/1984, art. 9º; Súmulas 182 e 242 do TST).
        DateOnly? dataBase = null;
        var adicional = 0m;
        if (c.MesDataBase is { } mesDataBase && c.Motivo == MotivoRescisao.DispensaSemJustaCausa)
        {
            var proxima = new DateOnly(c.Desligamento.Year, mesDataBase, 1);
            dataBase = proxima > c.Desligamento ? proxima : proxima.AddYears(1);
            if (projecao >= dataBase.Value.AddDays(-30) && projecao < dataBase.Value)
                adicional = Arredondar(c.Salario);
        }

        // Verbas pagas depois de 10 dias do fim do contrato: multa de um salário (CLT, art. 477, §§ 6º e 8º).
        var prazo = c.Desligamento.AddDays(10);
        var multaAtraso = c.DataPagamento is { } pago && pago > prazo ? Arredondar(c.Salario) : 0m;
        return new IndenizacoesRescisao(
            diasRestantes,
            c.Motivo == MotivoRescisao.RescisaoAntecipadaPeloEmpregador ? metadeDosDias : 0m,
            c.Motivo == MotivoRescisao.RescisaoAntecipadaPeloEmpregado ? metadeDosDias : 0m,
            dataBase,
            adicional,
            prazo,
            multaAtraso);
    }

    private static DecimoTerceiroRescisao DecimoTerceiro(ContratoRescindido c, DateOnly projecao)
    {
        var ano = c.Desligamento.Year;
        int avos = 0, avosAviso = 0;
        if (!c.JustaCausa)
        {
            avos = RegrasTrabalhistas.AvosDecimoTerceiro(c.Admissao, c.Desligamento, ano);
            var avosProjetados = projecao.Year == ano
                ? RegrasTrabalhistas.AvosDecimoTerceiro(c.Admissao, projecao, ano)
                : RegrasTrabalhistas.AvosDecimoTerceiro(c.Admissao, new DateOnly(ano, 12, 31), ano) + RegrasTrabalhistas.AvosDecimoTerceiro(c.Admissao, projecao, projecao.Year);
            avosAviso = avosProjetados - avos;
        }
        // O total é arredondado uma vez e a parcela da projeção é a diferença, para a memória fechar com o valor pago.
        var total = Arredondar(c.Remuneracao * (avos + avosAviso) / 12m);
        var proporcional = Arredondar(c.Remuneracao * avos / 12m);
        return new DecimoTerceiroRescisao(avos, avosAviso, total, proporcional, total - proporcional);
    }

    private static FeriasRescisao Ferias(ContratoRescindido c, DateOnly projecao)
    {
        // Com dois períodos vencidos, o mais antigo já passou do prazo de concessão e é pago em dobro (CLT, arts. 137 e 146).
        var periodosEmDobro = c.PeriodosFeriasVencidas == 2 ? 1 : 0;
        var vencidas = Arredondar(c.Remuneracao * (c.PeriodosFeriasVencidas - periodosEmDobro));
        var emDobro = Arredondar(c.Remuneracao * 2m * periodosEmDobro);
        var inicioPeriodo = RegrasTrabalhistas.InicioPeriodoAquisitivo(c.Admissao, c.Desligamento);
        var diasDireito = RegrasTrabalhistas.DiasDeFeriasPorFaltas(c.FaltasPeriodoAtual);
        int avos = 0, avosAviso = 0;
        if (!c.JustaCausa)
        {
            avos = RegrasTrabalhistas.AvosFerias(inicioPeriodo, c.Desligamento);
            avosAviso = RegrasTrabalhistas.AvosFerias(inicioPeriodo, projecao) - avos;
        }
        var total = Arredondar(c.Remuneracao * diasDireito * (avos + avosAviso) / 360m);
        var proporcionais = Arredondar(c.Remuneracao * diasDireito * avos / 360m);
        return new FeriasRescisao(vencidas, emDobro, Arredondar((vencidas + emDobro) / 3m), inicioPeriodo, diasDireito, avos, avosAviso,
            total, proporcionais, total - proporcionais, Arredondar(total / 3m));
    }

    private static FgtsRescisao Fgts(ContratoRescindido c, SaldoDeSalario saldo, AvisoPrevioRescisao aviso, DecimoTerceiroRescisao decimoTerceiro)
    {
        // A 1ª parcela do 13º já teve o FGTS depositado no mês em que foi paga: aqui entra só a diferença, e o
        // adiantamento maior que o 13º devido reduz a base do mês.
        var baseDoMes = Math.Max(0m, saldo.VerbasDoMes + aviso.Indenizado + decimoTerceiro.Total - c.AdiantamentoDecimoTerceiro);
        var deposito = Arredondar(baseDoMes * c.PercentualFgts / 100m);
        // A rescisão antecipada pelo empregador equipara-se à dispensa sem justa causa (Decreto 99.684/1990, art. 14).
        // O doméstico não tem a multa: no lugar dela, a indenização compensatória, depositada mês a mês.
        var percentualMulta = c.Domestico ? 0m : c.Motivo switch { MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.RescisaoAntecipadaPeloEmpregador => 40m, MotivoRescisao.Acordo => 20m, _ => 0m };
        var percentualSaque = c.Motivo switch
        {
            MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.TerminoDeContratoPorPrazo or MotivoRescisao.RescisaoAntecipadaPeloEmpregador => 100m,
            MotivoRescisao.Acordo => 80m,
            _ => 0m
        };
        // A estimativa soma os depósitos anteriores ao mês do desligamento: os meses de contrato, o 13º dos anos anteriores
        // e o adiantamento do 13º deste ano; o mês do desligamento e o restante do 13º estão no depósito acima.
        var mesesDepositados = RegrasTrabalhistas.AvosFerias(c.Admissao, c.CompetenciaDesligamento.AddDays(-1));
        var mesesAnosAnteriores = RegrasTrabalhistas.AvosFerias(c.Admissao, new DateOnly(c.Desligamento.Year, 1, 1).AddDays(-1));
        var saldoEstimado = c.SaldoFgts == 0m;
        var saldoFgts = saldoEstimado ? Arredondar((c.Remuneracao * (mesesDepositados + mesesAnosAnteriores / 12m) + c.AdiantamentoDecimoTerceiro) * c.PercentualFgts / 100m) : c.SaldoFgts;
        var multa = Arredondar((saldoFgts + deposito) * percentualMulta / 100m);
        // O percentual de saque vale sobre todo o saldo, inclusive a multa depositada: no acordo, 80% de tudo (Manual de
        // Movimentação da Conta Vinculada do FGTS da Caixa, versão 28, código 07).
        var saque = Arredondar((saldoFgts + deposito + multa) * percentualSaque / 100m);
        return new FgtsRescisao(deposito, percentualMulta, percentualSaque, percentualMulta > 0m || percentualSaque > 0m || c.Domestico,
            mesesDepositados, mesesAnosAnteriores, saldoEstimado, saldoFgts, multa, saque, c.PercentualFgts, c.Domestico ? Compensatoria(c, baseDoMes, saldoFgts) : null);
    }

    /// <summary>
    /// 3,2% do doméstico: o depósito do mês sobre a mesma base do FGTS e o saldo estimado em 40% do saldo do FGTS. Na
    /// dispensa sem justa causa e na rescisão antecipada pelo empregador, vai para o empregado; no acordo, a metade, como a
    /// metade da indenização do art. 484-A da CLT; nos demais motivos, volta ao empregador (LC 150/2015, art. 22, § 1º).
    /// </summary>
    private static CompensatoriaDomestico Compensatoria(ContratoRescindido c, decimal baseDoMes, decimal saldoFgts)
    {
        var deposito = Arredondar(baseDoMes * .032m);
        var saldo = Arredondar(saldoFgts * .4m);
        var percentual = c.Motivo switch
        {
            MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.RescisaoAntecipadaPeloEmpregador => 100m,
            MotivoRescisao.Acordo => 50m,
            _ => 0m
        };
        return new CompensatoriaDomestico(deposito, saldo, percentual, Arredondar((saldo + deposito) * percentual / 100m));
    }

    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
}
