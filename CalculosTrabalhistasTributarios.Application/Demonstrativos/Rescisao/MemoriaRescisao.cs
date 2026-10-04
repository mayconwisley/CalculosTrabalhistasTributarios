using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using static CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao.TextosRescisao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao;

/// <summary>Memória de cálculo da rescisão: cada verba com a fórmula que a produziu.</summary>
internal static class MemoriaRescisao
{
    private const string DeducaoMensal = "Deduzida da base do IRRF na modalidade de deduções legais; o desconto simplificado substitui essa dedução.";

    public static List<GrupoMemoriaDto> Grupos(VerbasRescisorias v, TributosRescisao t, EstimativaSeguroRescisao? seguro)
    {
        var c = v.Contrato;
        var rotuloMes = VerbasDoMes(c);
        var memoria = new List<GrupoMemoriaDto> { Contrato(v), Saldo(c, v.Saldo) };
        if (Indenizacoes(c, v.Indenizacoes, v.Aviso.Projecao) is { } indenizacoes)
            memoria.Add(indenizacoes);
        if (!c.JustaCausa)
            memoria.Add(DecimoTerceiro(c, v.DecimoTerceiro, v.Aviso.Projecao));
        memoria.Add(Ferias(c, v.Ferias));
        memoria.Add(Fgts(c, v));
        if (seguro is not null)
            memoria.Add(seguro.Domestico ? SeguroDomestico(seguro) : Seguro(c.Remuneracao, seguro));
        if (v.Saldo.VerbasDoMes > 0m)
        {
            memoria.Add(MemoriaTributaria.Inss($"INSS sobre o {rotuloMes}", t.InssSaldo, rotuloMes));
            memoria.Add(MemoriaTributaria.Irrf($"IRRF sobre o {rotuloMes}", t.IrrfSaldo, rotuloMes));
        }
        if (v.DecimoTerceiro.Total > 0m)
        {
            memoria.Add(MemoriaTributaria.Inss("INSS sobre o 13º", t.Inss13, "13º proporcional"));
            memoria.Add(MemoriaTributaria.Irrf("IRRF sobre o 13º", t.Irrf13, "13º proporcional"));
        }
        if (t.Pensao is { } regra && t.PensaoSaldo is { } pensaoSaldo)
            memoria.Add(DemonstrativoPensao.Memoria(regra.EhPercentual ? $"Pensão alimentícia sobre o {rotuloMes}" : "Pensão alimentícia", regra, v.Saldo.VerbasDoMes, rotuloMes, t.InssSaldo.Valor, t.IrrfSaldo.Imposto, pensaoSaldo.Base, pensaoSaldo.Pensao,
                regra.EhPercentual ? DeducaoMensal : "Descontada do saldo de salário e deduzida da base do IRRF dele, na modalidade de deduções legais."));
        if (t.Pensao is { } regra13 && t.Pensao13 is { } pensao13)
            memoria.Add(DemonstrativoPensao.Memoria("Pensão alimentícia sobre o 13º", regra13, v.DecimoTerceiro.Total, "13º proporcional", t.Inss13.Valor, t.Irrf13.Imposto, pensao13.Base, pensao13.Pensao, DeducaoMensal));
        return memoria;
    }

    private static GrupoMemoriaDto Contrato(VerbasRescisorias v)
    {
        var c = v.Contrato;
        var aviso = v.Aviso;
        var formulas = new List<FormulaDto>
        {
            new("Contrato", $"De {Formato.Data(c.Admissao)} a {Formato.Data(c.Desligamento)}: {v.AnosCompletos} ano(s) completo(s) de serviço")
        };
        if (c.Antecipada && c.FimPrevistoContrato is { } fim)
        {
            var dias = fim.DayNumber - c.Desligamento.DayNumber;
            var metade = CalculadoraTributacao.Arredondar(c.Remuneracao * dias / 60m);
            formulas.Add(new("Contrato a prazo", $"Fim previsto em {Formato.Data(fim)}: faltavam {Formato.Dias(dias)}"));
            formulas.Add(c.Motivo == MotivoRescisao.RescisaoAntecipadaPeloEmpregador
                ? new("Indenização (art. 479)", $"{Remuneracao(c)} ÷ 30 x {Formato.Dias(dias)} ÷ 2 = {Formato.Moeda(metade)}: metade da remuneração até o fim previsto")
                : new("Indenização ao empregador (art. 480)", $"Até {Remuneracao(c)} ÷ 30 x {Formato.Dias(dias)} ÷ 2 = {Formato.Moeda(metade)}, se o empregador comprovar prejuízo; não foi descontada"));
        }
        if (c.Motivo is MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.Acordo)
            formulas.Add(new("Aviso prévio proporcional", $"30 dias + 3 x {v.AnosCompletos} ano(s) = {Formato.Dias(aviso.DiasProporcionais)}{(aviso.DiasProporcionais == 90 ? " (limite de 90 dias)" : "")} (Lei 12.506/2011)"));
        if (aviso.Cumprimento == CumprimentoAvisoPrevio.Indenizado)
        {
            if (c.Motivo == MotivoRescisao.Acordo)
                formulas.Add(new("Metade do aviso no acordo", $"{Formato.Dias(aviso.DiasProporcionais)} ÷ 2 = {Dias(aviso.DiasIndenizados)} (CLT, art. 484-A)"));
            formulas.Add(new("Aviso prévio indenizado", $"{Remuneracao(c)} ÷ 30 x {Dias(aviso.DiasIndenizados)} = {Formato.Moeda(aviso.Indenizado)}"));
            formulas.Add(new("Projeção do contrato", $"O aviso indenizado conta como tempo de serviço: o contrato se estende até {Formato.Data(aviso.Projecao)} para o 13º e as férias."));
        }
        else if (aviso.Desconto > 0m)
            formulas.Add(new("Aviso prévio não cumprido", $"{Remuneracao(c)} de 30 dias = {Formato.Moeda(aviso.Desconto)}, descontados (CLT, art. 487, § 2º)"));
        else if (c.Motivo is MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.Acordo or MotivoRescisao.PedidoDeDemissao)
            formulas.Add(new("Aviso prévio", "Trabalhado ou dispensado: os dias trabalhados já estão no saldo de salário."));
        else
            formulas.Add(new("Aviso prévio", "Não há aviso prévio neste motivo de desligamento."));
        return new GrupoMemoriaDto("Contrato e aviso prévio", aviso.Cumprimento == CumprimentoAvisoPrevio.Indenizado ? $"Aviso: {Formato.Moeda(aviso.Indenizado)}" : "Aviso sem valor", formulas);
    }

    private static GrupoMemoriaDto Saldo(ContratoRescindido c, SaldoDeSalario saldo)
    {
        var formulas = new List<FormulaDto>
        {
            new("Dias trabalhados no mês", c.FaltasNoMes > 0
                ? $"{Formato.Dias(saldo.DiasNoMes)} até {Formato.Data(c.Desligamento)}, no mês comercial de 30 dias, menos {Formato.Dias(c.FaltasNoMes)} de falta = {Formato.Dias(saldo.Dias)}"
                : $"{Formato.Dias(saldo.Dias)} até {Formato.Data(c.Desligamento)}, no mês comercial de 30 dias"),
            new("Saldo de salário", $"{Formato.Moeda(c.Salario)} ÷ 30 x {Formato.Dias(saldo.Dias)} = {Formato.Moeda(saldo.Valor)}")
        };
        if (saldo.DsrPerdido > 0m)
            formulas.Add(new("DSR perdido", $"{Formato.Moeda(c.Salario)} ÷ 30 x {saldo.SemanasComFalta} semana(s) com falta = {Formato.Moeda(saldo.DsrPerdido)}, um dia de salário por semana (Lei 605/1949, art. 6º)"));
        if (c.OutrosProventos > 0m || saldo.DsrPerdido > 0m)
            formulas.Add(new("Verbas do mês", $"{Formato.Moeda(saldo.Valor)} (saldo){(saldo.DsrPerdido > 0m ? $" - {Formato.Moeda(saldo.DsrPerdido)} (DSR perdido)" : "")}{(c.OutrosProventos > 0m ? $" + {Formato.Moeda(c.OutrosProventos)} (outros proventos)" : "")} = {Formato.Moeda(saldo.VerbasDoMes)}, base do INSS, do IRRF e do FGTS do mês"));
        return new GrupoMemoriaDto("Saldo de salário", $"Saldo: {Formato.Moeda(saldo.Valor)}", formulas);
    }

    private static GrupoMemoriaDto? Indenizacoes(ContratoRescindido c, IndenizacoesRescisao i, DateOnly projecao)
    {
        var formulas = new List<FormulaDto>();
        if (i.DataBase is { } data)
            formulas.Add(new("Indenização adicional (Lei 7.238/1984)", i.Adicional > 0m
                ? $"Data-base em {Formato.Data(data)}; o contrato projetado termina em {Formato.Data(projecao)}, nos 30 dias anteriores (a partir de {Formato.Data(data.AddDays(-30))}): um salário de {Formato.Moeda(i.Adicional)}"
                : $"Data-base em {Formato.Data(data)}; o contrato projetado termina em {Formato.Data(projecao)}, fora dos 30 dias anteriores (de {Formato.Data(data.AddDays(-30))} a {Formato.Data(data.AddDays(-1))}): não é devida"));
        if (c.DataPagamento is { } pagamento)
            formulas.Add(new("Multa por atraso (art. 477, § 8º)", i.MultaAtraso > 0m
                ? $"Pagamento em {Formato.Data(pagamento)}, depois do prazo de {Formato.Data(i.PrazoPagamento)}: um salário de {Formato.Moeda(i.MultaAtraso)}"
                : $"Pagamento em {Formato.Data(pagamento)}, dentro do prazo de {Formato.Data(i.PrazoPagamento)}: não há multa"));
        return formulas.Count == 0 ? null
            : new GrupoMemoriaDto("Indenizações e multas", i.Adicional + i.MultaAtraso > 0m ? $"Total: {Formato.Moeda(i.Adicional + i.MultaAtraso)}" : "Não são devidas", formulas);
    }

    private static GrupoMemoriaDto DecimoTerceiro(ContratoRescindido c, DecimoTerceiroRescisao d, DateOnly projecao) =>
        new("13º salário", $"13º: {Formato.Moeda(d.Total)}",
        [
            new("Avos até o desligamento", $"{Formato.Avos(d.Avos)}: meses de {c.Desligamento.Year} com 15 dias ou mais de trabalho"),
            new("Avos pela projeção do aviso", d.AvosAviso > 0 ? $"{Formato.Avos(d.AvosAviso)} a mais, com o contrato projetado até {Formato.Data(projecao)}" : "Nenhum avo a mais"),
            new("13º proporcional", $"{Remuneracao(c)} ÷ 12 x {d.Avos + d.AvosAviso} avos = {Formato.Moeda(d.Total)}")
        ]);

    private static GrupoMemoriaDto Ferias(ContratoRescindido c, FeriasRescisao f)
    {
        var formulas = new List<FormulaDto>();
        if (f.Vencidas > 0m)
            formulas.Add(new("Férias vencidas", $"{Remuneracao(c)} x 1 período = {Formato.Moeda(f.Vencidas)}"));
        if (f.EmDobro > 0m)
            formulas.Add(new("Férias vencidas em dobro", $"{Remuneracao(c)} x 2 = {Formato.Moeda(f.EmDobro)}: com dois períodos vencidos, o mais antigo já passou do prazo de concessão (CLT, art. 137)"));
        if (f.TercoVencidas > 0m)
            formulas.Add(new("1/3 sobre férias vencidas", f.EmDobro > 0m
                ? $"({Formato.Moeda(f.Vencidas)} + {Formato.Moeda(f.EmDobro)}) ÷ 3 = {Formato.Moeda(f.TercoVencidas)}"
                : $"{Formato.Moeda(f.Vencidas)} ÷ 3 = {Formato.Moeda(f.TercoVencidas)}"));
        if (c.JustaCausa)
            formulas.Add(new("Férias proporcionais", "Não são devidas na dispensa por justa causa (Súmula 171 do TST)."));
        else
        {
            formulas.Add(new("Período aquisitivo em curso", $"Desde {Formato.Data(f.InicioPeriodo)}: {Formato.Avos(f.Avos)} até o desligamento{(f.AvosAviso > 0 ? $" e {Formato.Avos(f.AvosAviso)} a mais pela projeção do aviso" : "")}"));
            formulas.Add(new("Dias de direito", $"{c.FaltasPeriodoAtual} falta(s) injustificada(s): {Formato.Dias(f.DiasDireito)} por período completo (CLT, art. 130)"));
            formulas.Add(new("Férias proporcionais", $"{Remuneracao(c)} ÷ 30 x {Formato.Dias(f.DiasDireito)} ÷ 12 x {f.Avos + f.AvosAviso} avos = {Formato.Moeda(f.Total)}"));
            formulas.Add(new("1/3 sobre férias proporcionais", $"{Formato.Moeda(f.Total)} ÷ 3 = {Formato.Moeda(f.TercoProporcionais)}"));
        }
        return new GrupoMemoriaDto("Férias", $"Férias + 1/3: {Formato.Moeda(f.TotalComTerco)}", formulas);
    }

    private static GrupoMemoriaDto Fgts(ContratoRescindido c, VerbasRescisorias v)
    {
        var f = v.Fgts;
        var adiantamento = c.AdiantamentoDecimoTerceiro;
        var formulas = new List<FormulaDto>
        {
            new("Depósito do mês", adiantamento > 0m
                ? $"({Formato.Moeda(v.Saldo.VerbasDoMes)} (verbas do mês) + {Formato.Moeda(v.Aviso.Indenizado)} (aviso) + {Formato.Moeda(v.DecimoTerceiro.Total)} (13º) - {Formato.Moeda(adiantamento)} (1ª parcela do 13º, que já teve FGTS)) x {Formato.PercentualCurto(f.PercentualDeposito)} = {Formato.Moeda(f.Deposito)}"
                : $"({Formato.Moeda(v.Saldo.VerbasDoMes)} (verbas do mês) + {Formato.Moeda(v.Aviso.Indenizado)} (aviso) + {Formato.Moeda(v.DecimoTerceiro.Total)} (13º)) x {Formato.PercentualCurto(f.PercentualDeposito)} = {Formato.Moeda(f.Deposito)}")
        };
        if (f.UsaSaldo)
            formulas.Add(new("Saldo do FGTS", f.SaldoEstimado
                ? $"Estimado: ({Formato.Moeda(c.Remuneracao)} x ({f.MesesDepositados} meses antes do desligamento + {f.MesesAnosAnteriores}/12 de 13º dos anos anteriores){(adiantamento > 0m ? $" + {Formato.Moeda(adiantamento)} (1ª parcela do 13º)" : "")}) x {Formato.PercentualCurto(f.PercentualDeposito)} = {Formato.Moeda(f.Saldo)}"
                : $"Informado: {Formato.Moeda(f.Saldo)}"));
        if (f.PercentualMulta > 0m)
            formulas.Add(new("Multa rescisória", $"({Formato.Moeda(f.Saldo)} + {Formato.Moeda(f.Deposito)}) x {Formato.PercentualCurto(f.PercentualMulta)} = {Formato.Moeda(f.Multa)}"));
        if (f.Compensatoria is { } compensatoria)
        {
            formulas.Add(new("Indenização compensatória do mês", $"Mesma base do depósito do mês x 3,2% = {Formato.Moeda(compensatoria.Deposito)}"));
            formulas.Add(new("Saldo da indenização compensatória", $"{Formato.Moeda(f.Saldo)} x 40% = {Formato.Moeda(compensatoria.Saldo)}, estimado: 3,2% é 40% dos 8% do FGTS; confira o valor no extrato"));
            formulas.Add(new("Destino da indenização", compensatoria.AoEmpregado > 0m
                ? $"({Formato.Moeda(compensatoria.Saldo)} + {Formato.Moeda(compensatoria.Deposito)}) x {Formato.PercentualCurto(compensatoria.PercentualAoEmpregado)} = {Formato.Moeda(compensatoria.AoEmpregado)} para o empregado (LC 150/2015, art. 22)"
                : $"{Formato.Moeda(compensatoria.AoEmpregador)} voltam ao empregador neste motivo (LC 150/2015, art. 22, § 1º)"));
        }
        formulas.Add(new("Saque", f.PercentualSaque > 0m
            ? $"({Formato.Moeda(f.Saldo)} + {Formato.Moeda(f.Deposito)}{(f.Multa > 0m ? $" + {Formato.Moeda(f.Multa)} (multa)" : "")}) x {Formato.PercentualCurto(f.PercentualSaque)} = {Formato.Moeda(f.Saque)}"
            : "Neste motivo de desligamento, o FGTS não pode ser sacado."));
        return new GrupoMemoriaDto("FGTS", f.PercentualSaque > 0m ? $"Saque: {Formato.Moeda(f.Saque)}" : $"Depósito: {Formato.Moeda(f.Deposito)}", formulas);
    }

    private static GrupoMemoriaDto SeguroDomestico(EstimativaSeguroRescisao estimativa)
    {
        var formulas = new List<FormulaDto>
        {
            new("Valor da parcela", $"Um salário mínimo: {Formato.Moeda(estimativa.Parcela.Valor)} (LC 150/2015, art. 26)"),
            new("Parcelas", estimativa.Parcelas > 0
                ? $"{estimativa.Meses} meses neste contrato nos últimos 24: até 3 parcelas"
                : $"{estimativa.Meses} meses neste contrato: o doméstico precisa de {RegrasSeguroDesemprego.MesesMinimosDomestico} meses de trabalho nos últimos 24 (art. 28)")
        };
        return new GrupoMemoriaDto("Seguro-desemprego do doméstico (estimado)", estimativa.Parcelas > 0 ? $"{estimativa.Parcelas} x {Formato.Moeda(estimativa.Parcela.Valor)}" : "Sem direito", formulas);
    }

    private static GrupoMemoriaDto Seguro(decimal remuneracao, EstimativaSeguroRescisao estimativa)
    {
        var parcela = estimativa.Parcela;
        var formulas = new List<FormulaDto>
        {
            new("Média estimada", $"{Formato.Moeda(remuneracao)}: a remuneração atual no lugar da média dos 3 últimos salários"),
            new("Valor da parcela", parcela.Percentual == 0m
                ? $"Média acima de {Formato.Moeda(parcela.LimiteAnterior)}: valor máximo de {Formato.Moeda(parcela.Valor)}"
                : parcela.Valor > parcela.ValorPelaTabela
                    ? $"{Formato.Moeda(parcela.ValorPelaTabela)} pela tabela, abaixo do salário mínimo: {Formato.Moeda(parcela.Valor)}"
                    : $"Faixa {parcela.Faixa} da tabela: {Formato.Moeda(parcela.Valor)}"),
            new("Parcelas", estimativa.Parcelas > 0
                ? $"{estimativa.Meses} meses neste contrato, na 1ª solicitação: {estimativa.Parcelas} parcelas"
                : $"{estimativa.Meses} meses neste contrato: a 1ª solicitação exige 12 meses")
        };
        return new GrupoMemoriaDto("Seguro-desemprego (estimado)", estimativa.Parcelas > 0 ? $"{estimativa.Parcelas} x {Formato.Moeda(parcela.Valor)}" : "Sem direito na 1ª solicitação", formulas);
    }
}
