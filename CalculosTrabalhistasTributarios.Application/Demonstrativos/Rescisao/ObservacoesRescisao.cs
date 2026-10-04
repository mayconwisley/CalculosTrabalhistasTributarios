using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao;

/// <summary>Observações da rescisão: o que o motivo garante, prazos, estimativas e limites do cálculo.</summary>
internal static class ObservacoesRescisao
{
    public static List<string> Listar(VerbasRescisorias v, TributosRescisao t, EstimativaSeguroRescisao? seguro, decimal liquido)
    {
        var c = v.Contrato;
        var i = v.Indenizacoes;
        var observacoes = new List<string>
        {
            TextosRescisao.DescreverMotivo(c.Motivo),
            "Aviso prévio indenizado, férias indenizadas, indenizações e multas não têm INSS nem IRRF; o 13º tem INSS e IRRF calculados à parte do saldo de salário.",
            c.DataPagamento is { } dataInformada
                ? i.MultaAtraso > 0m
                    ? $"Pagamento em {Formato.Data(dataInformada)}, depois do prazo de 10 dias, que ia até {Formato.Data(i.PrazoPagamento)}: é devida a multa de um salário (CLT, art. 477, § 8º). Ela foi tratada como indenização, sem INSS, IRRF nem FGTS, como entende a maior parte dos tribunais."
                    : $"Pagamento em {Formato.Data(dataInformada)}, dentro do prazo de 10 dias após o fim do contrato (CLT, art. 477, § 6º)."
                : "O pagamento deve ser feito em até 10 dias após o fim do contrato (CLT, art. 477, § 6º); depois disso, é devida a multa de um salário (§ 8º).",
            "Verbas e descontos específicos da convenção coletiva entram nos campos de outros proventos, verbas indenizatórias e outros descontos; confira a incidência de cada um na convenção da categoria."
        };
        if (c.Domestico)
            observacoes.Add("Empregado doméstico (LC 150/2015): no lugar da multa de 40% do FGTS, o empregador deposita 3,2% todo mês. Na dispensa sem justa causa, o empregado saca esse valor; no acordo, a metade, e a outra metade volta ao empregador, como a metade da indenização do art. 484-A da CLT; na justa causa, no pedido de demissão e no fim do contrato a prazo, tudo volta ao empregador. O FGTS do mês e os 3,2% vão no DAE rescisório.");
        if (c.Vinculo == TipoVinculo.Aprendiz)
            observacoes.Add("Jovem aprendiz: o FGTS é de 2% (Lei 8.036/1990, art. 15, § 7º). No fim do contrato, no desempenho insuficiente, na falta disciplinar grave, na ausência à escola e a pedido do aprendiz, não há as indenizações dos arts. 479 e 480 (CLT, art. 433, § 2º); a dispensa antecipada fora dessas hipóteses segue o art. 479.");
        var projecao = v.Aviso.Projecao;
        if (i.DataBase is { } dataBase)
            observacoes.Add(i.Adicional > 0m
                ? $"O contrato, projetado até {Formato.Data(projecao)}, termina nos 30 dias antes da data-base de {Formato.Data(dataBase)}: indenização adicional de um salário, sem INSS, IRRF nem FGTS (Lei 7.238/1984, art. 9º)."
                : projecao >= dataBase
                    ? $"O contrato projetado pelo aviso passa da data-base de {Formato.Data(dataBase)}: não há indenização adicional, mas as verbas devem ser pagas com o salário reajustado (Súmula 314 do TST)."
                    : $"O contrato, projetado até {Formato.Data(projecao)}, termina antes dos 30 dias que antecedem a data-base de {Formato.Data(dataBase)}: não há indenização adicional.");
        if (seguro is { Domestico: true })
            observacoes.Add("O seguro-desemprego do doméstico vale um salário mínimo, em até 3 parcelas, para quem trabalhou 15 meses nos últimos 24 (LC 150/2015, arts. 26 e 28); a estimativa considera só este contrato.");
        else if (seguro is not null)
            observacoes.Add(seguro.Parcelas > 0
                ? "O seguro-desemprego foi estimado para a 1ª solicitação, só com os meses deste contrato e com a remuneração atual como média dos últimos salários; para outra situação, use a calculadora Seguro-desemprego. Ele é pago pelo governo, não pela empresa."
                : "Com menos de 12 meses neste contrato, não há seguro-desemprego na 1ª solicitação, a não ser que haja outros empregos nos últimos 18 meses; confira na calculadora Seguro-desemprego.");
        if (t.CompetenciaIrrf != c.CompetenciaDesligamento)
            observacoes.Insert(1, $"O IRRF usa a tabela de {Formato.Competencia(t.CompetenciaIrrf)}, mês do pagamento (regime de caixa); o INSS, a da competência do desligamento, {Formato.Competencia(c.CompetenciaDesligamento)}.");
        if (c.Motivo == MotivoRescisao.Acordo)
        {
            observacoes.Add("No acordo, o saque é de 80% do saldo da conta, inclusive da multa de 20% depositada, como orienta o Manual de Movimentação da Conta Vinculada do FGTS da Caixa (versão 28, código 07); o restante fica na conta. O texto da CLT fala em 80% do valor dos depósitos (art. 484-A, § 1º).");
            if (v.Aviso.Cumprimento == CumprimentoAvisoPrevio.Indenizado)
                observacoes.Add("No acordo, a projeção do aviso indenizado no 13º, nas férias e na data de término usa só os dias pagos, a metade, como na tabela do eSocial para o código 33 (Manual do Empregador Doméstico). Há entendimento minoritário pela projeção dos dias inteiros (CLT, art. 487, § 1º).");
        }
        if (v.Fgts.UsaSaldo && v.Fgts.SaldoEstimado)
            observacoes.Insert(1, "O saldo do FGTS foi estimado com o salário atual; informe o saldo do extrato do FGTS para obter a multa e o saque exatos.");
        if (t.Pensao is { } regra)
            observacoes.Add(regra.EhPercentual
                ? "A pensão incide sobre as verbas salariais: o saldo de salário e o 13º. As indenizatórias (aviso prévio indenizado, férias indenizadas com 1/3, FGTS e multa) ficaram fora da base; se a decisão determinar a incidência sobre elas, informe o valor da pensão."
                : "O valor informado da pensão foi descontado do saldo de salário e deduzido da base do IRRF dele.");
        if (liquido < 0m)
            observacoes.Insert(0, "Os descontos superam os proventos. Na rescisão, a compensação de descontos é limitada a uma remuneração mensal (CLT, art. 477, § 5º).");
        return observacoes;
    }
}
