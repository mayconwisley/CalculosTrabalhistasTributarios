using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;
using static CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao.TextosRescisao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao;

/// <summary>Monta o demonstrativo da rescisão a partir das verbas, dos tributos e da estimativa do seguro-desemprego.</summary>
internal static class DemonstrativoRescisao
{
    public static DemonstrativoDto Montar(VerbasRescisorias v, TributosRescisao t, EstimativaSeguroRescisao? seguro)
    {
        var c = v.Contrato;
        var proventos = Proventos(v);
        var descontos = Descontos(v, t);
        var liquido = proventos.Sum(verba => verba.Valor) - descontos.Sum(verba => verba.Valor);
        var fgts = v.Fgts;
        return new DemonstrativoDto(
            "Rescisão do contrato de trabalho",
            $"{NomeMotivo(c.Motivo)} • Desligamento em {Formato.Data(c.Desligamento)}",
            [
                new("Líquido da rescisão", Formato.Moeda(liquido), "Pago em até 10 dias"),
                new("Aviso prévio", Aviso(c.Motivo, v.Aviso), v.Aviso.Cumprimento == CumprimentoAvisoPrevio.Indenizado ? $"Indenizado: {Formato.Moeda(v.Aviso.Indenizado)}" : "Sem valor a pagar"),
                fgts.Compensatoria is { } compensatoria
                    ? new("Indenização compensatória", compensatoria.AoEmpregado > 0m ? Formato.Moeda(compensatoria.AoEmpregado) : "Volta ao empregador",
                        compensatoria.AoEmpregado > 0m ? $"{Formato.PercentualCurto(compensatoria.PercentualAoEmpregado)} dos 3,2% depositados" : "Os 3,2% não são do empregado neste motivo")
                    : new("Multa do FGTS", fgts.PercentualMulta > 0m ? Formato.Moeda(fgts.Multa) : "Não se aplica", fgts.PercentualMulta > 0m ? $"{Formato.PercentualCurto(fgts.PercentualMulta)} sobre o saldo do FGTS" : "Só na dispensa sem justa causa e no acordo"),
                new("FGTS para saque", fgts.PercentualSaque > 0m ? Formato.Moeda(fgts.Saque) : "Sem saque", fgts.PercentualSaque > 0m ? (fgts.SaldoEstimado ? "Com saldo estimado" : "Com o saldo informado") : "O FGTS fica na conta")
            ],
            proventos,
            descontos,
            Informativos(v, seguro),
            MemoriaRescisao.Grupos(v, t, seguro),
            ObservacoesRescisao.Listar(v, t, seguro, liquido),
            RotuloResultado: "Líquido da rescisão");
    }

    private static List<VerbaDto> Proventos(VerbasRescisorias v)
    {
        var (c, aviso, i, d, f) = (v.Contrato, v.Aviso, v.Indenizacoes, v.DecimoTerceiro, v.Ferias);
        var proventos = new List<VerbaDto> { new("Saldo de salário", Formato.Dias(v.Saldo.Dias), v.Saldo.Valor) };
        if (c.OutrosProventos > 0m) proventos.Add(new("Outros proventos do mês", "", c.OutrosProventos));
        if (aviso.Indenizado > 0m) proventos.Add(new("Aviso prévio indenizado", Dias(aviso.DiasIndenizados), aviso.Indenizado));
        if (i.Artigo479 > 0m) proventos.Add(new("Indenização da rescisão antecipada (art. 479)", $"{Formato.Dias(i.DiasRestantes)} ÷ 2", i.Artigo479));
        if (i.Adicional > 0m) proventos.Add(new("Indenização adicional (Lei 7.238/1984)", "1 salário", i.Adicional));
        if (i.MultaAtraso > 0m) proventos.Add(new("Multa por atraso no pagamento (art. 477, § 8º)", "1 salário", i.MultaAtraso));
        if (c.VerbasIndenizatorias > 0m) proventos.Add(new("Verbas indenizatórias da convenção ou do acordo", "Sem tributos", c.VerbasIndenizatorias));
        if (d.Proporcional > 0m) proventos.Add(new("13º salário proporcional", Formato.Avos(d.Avos), d.Proporcional));
        if (d.SobreAviso > 0m) proventos.Add(new("13º sobre o aviso prévio indenizado", Formato.Avos(d.AvosAviso), d.SobreAviso));
        if (f.Vencidas > 0m) proventos.Add(new("Férias vencidas", "1 período", f.Vencidas));
        if (f.EmDobro > 0m) proventos.Add(new("Férias vencidas em dobro", "1 período", f.EmDobro));
        if (f.TercoVencidas > 0m) proventos.Add(new("1/3 sobre férias vencidas", "", f.TercoVencidas));
        if (f.Proporcionais > 0m) proventos.Add(new("Férias proporcionais", Formato.Avos(f.Avos), f.Proporcionais));
        if (f.SobreAviso > 0m) proventos.Add(new("Férias sobre o aviso prévio indenizado", Formato.Avos(f.AvosAviso), f.SobreAviso));
        if (f.TercoProporcionais > 0m) proventos.Add(new("1/3 sobre férias proporcionais", "", f.TercoProporcionais));
        return proventos;
    }

    private static List<VerbaDto> Descontos(VerbasRescisorias v, TributosRescisao t)
    {
        var c = v.Contrato;
        var rotuloMes = VerbasDoMes(c);
        var descontos = new List<VerbaDto>();
        if (v.Saldo.DsrPerdido > 0m)
            descontos.Add(new("DSR perdido por faltas", v.Saldo.SemanasComFalta == 1 ? "1 semana" : $"{v.Saldo.SemanasComFalta} semanas", v.Saldo.DsrPerdido));
        if (v.Saldo.VerbasDoMes > 0m)
        {
            descontos.Add(new($"INSS sobre o {rotuloMes}", "", t.InssSaldo.Valor));
            descontos.Add(new(MemoriaTributaria.DescricaoIrrf($"IRRF sobre o {rotuloMes}", t.IrrfSaldo), MemoriaTributaria.ReferenciaIrrf(t.IrrfSaldo), t.IrrfSaldo.Imposto));
        }
        if (v.DecimoTerceiro.Total > 0m)
        {
            descontos.Add(new("INSS sobre o 13º", "", t.Inss13.Valor));
            descontos.Add(new(MemoriaTributaria.DescricaoIrrf("IRRF sobre o 13º", t.Irrf13), MemoriaTributaria.ReferenciaIrrf(t.Irrf13), t.Irrf13.Imposto));
        }
        if (t.Pensao is { } regra && t.PensaoSaldo is { Pensao: > 0m } pensaoSaldo)
            descontos.Add(new(regra.EhPercentual ? $"Pensão alimentícia sobre o {rotuloMes}" : "Pensão alimentícia", DemonstrativoPensao.Referencia(regra), pensaoSaldo.Pensao));
        if (t.Pensao is { } regra13 && t.Pensao13 is { Pensao: > 0m } pensao13)
            descontos.Add(new("Pensão alimentícia sobre o 13º", DemonstrativoPensao.Referencia(regra13), pensao13.Pensao));
        if (v.Aviso.Desconto > 0m) descontos.Add(new("Aviso prévio não cumprido", "30 dias", v.Aviso.Desconto));
        if (c.AdiantamentoDecimoTerceiro > 0m) descontos.Add(new("Adiantamento do 13º já pago", "", c.AdiantamentoDecimoTerceiro));
        if (c.OutrosDescontos > 0m) descontos.Add(new("Outros descontos (benefícios, vales e adiantamentos)", "", c.OutrosDescontos));
        return descontos;
    }

    private static List<VerbaDto> Informativos(VerbasRescisorias v, EstimativaSeguroRescisao? seguro)
    {
        var f = v.Fgts;
        var informativos = new List<VerbaDto> { new("Depósito do FGTS do mês da rescisão", Formato.PercentualCurto(f.PercentualDeposito), f.Deposito) };
        if (f.UsaSaldo) informativos.Add(new(f.SaldoEstimado ? "Saldo do FGTS (estimado)" : "Saldo do FGTS (informado)", "", f.Saldo));
        if (f.Multa > 0m) informativos.Add(new("Multa rescisória do FGTS", Formato.PercentualCurto(f.PercentualMulta), f.Multa));
        if (f.Compensatoria is { } compensatoria)
        {
            informativos.Add(new("Indenização compensatória do mês (DAE rescisório)", "3,2%", compensatoria.Deposito));
            informativos.Add(new("Saldo da indenização compensatória (estimado)", "40% do saldo do FGTS", compensatoria.Saldo));
            informativos.Add(compensatoria.AoEmpregado > 0m
                ? new("Indenização compensatória para o empregado", Formato.PercentualCurto(compensatoria.PercentualAoEmpregado), compensatoria.AoEmpregado)
                : new("Indenização compensatória que volta ao empregador", "100%", compensatoria.AoEmpregador));
        }
        if (f.PercentualSaque > 0m) informativos.Add(new("FGTS disponível para saque", v.Contrato.Motivo == MotivoRescisao.Acordo ? "80% do saldo e da multa" : Formato.PercentualCurto(f.PercentualSaque) + (f.Multa > 0m ? " + multa" : ""), f.Saque));
        if (v.Indenizacoes.Limite480 > 0m) informativos.Add(new("Indenização máxima ao empregador (art. 480)", $"{Formato.Dias(v.Indenizacoes.DiasRestantes)} ÷ 2", v.Indenizacoes.Limite480));
        if (seguro is { Parcelas: > 0 })
            informativos.Add(new("Seguro-desemprego (estimado)", $"{seguro.Parcelas} parcelas de {Formato.Moeda(seguro.Parcela.Valor)}", seguro.Parcela.Valor * seguro.Parcelas));
        return informativos;
    }
}
