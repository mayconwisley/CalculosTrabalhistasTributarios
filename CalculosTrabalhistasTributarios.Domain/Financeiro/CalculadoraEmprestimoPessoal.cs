using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using static CalculosTrabalhistasTributarios.Domain.Financeiro.MatematicaCredito;

namespace CalculosTrabalhistasTributarios.Domain.Financeiro;

public static class CalculadoraEmprestimoPessoal
{
    public static Result<ApuracaoEmprestimoPessoal> Calcular(EntradaEmprestimoPessoal e)
    {
        if (e.NumeroParcelas is < 1 or > 120)
            return Erro.Validacao("Parcelas: informe de 1 a 120 meses. Esse limite é técnico, não uma condição de aprovação.");
        if (e.JurosMensais is < 0m or > 20m || decimal.Round(e.JurosMensais, 6) != e.JurosMensais)
            return Erro.Validacao("Juros: informe de 0 a 20% ao mês, com até seis casas decimais.");
        if (e.DataLiberacao < new DateOnly(2025, 1, 1) || e.DataLiberacao.Year > 2100)
            return Erro.Validacao("Liberação: informe uma data entre 01/01/2025 e 31/12/2100. Simulações futuras mantêm as alíquotas atuais de IOF.");
        if (new[] { e.ValorSolicitado, e.IofInformado, e.SeguroFinanciado, e.OutrosCustosFinanciados, e.CustosNaLiberacao }
            .Any(v => v < 0m || v > 10_000_000m || decimal.Round(v, 2) != v))
            return Erro.Validacao("Valores monetários: informe valores não negativos, até R$ 10 milhões e com até duas casas decimais.");
        if (e.ValorSolicitado == 0m)
            return Erro.Validacao("Valor solicitado: informe um valor maior que zero.");

        var taxa = e.JurosMensais / 100m;
        var primeiro = e.DataLiberacao.AddMonths(1);
        var dias = e.IofAutomatico ? DiasPonderadosIof(e.NumeroParcelas, e.DataLiberacao, primeiro, taxa) : 0m;
        // Decreto 6.306/2007, art. 7º, I, b, 2, §§ 1º e 15; PF com principal definido.
        // https://www.planalto.gov.br/ccivil_03/_ato2007-2010/2007/decreto/d6306compilado.htm
        // Consultado em 07/10/2026. Não abrange regimes especiais ou renegociação.
        var coeficiente = 0.0038m + 0.000082m * dias;
        var baseSemIof = e.ValorSolicitado + e.SeguroFinanciado + e.OutrosCustosFinanciados;
        var iof = e.IofAutomatico
            ? CalculadoraTributacao.Arredondar(baseSemIof * coeficiente / (e.FinanciarIof ? 1m - coeficiente : 1m))
            : e.IofInformado;
        var principal = baseSemIof + (e.FinanciarIof ? iof : 0m);
        var liquido = e.ValorSolicitado - e.CustosNaLiberacao - (e.FinanciarIof ? 0m : iof);
        if (liquido <= 0m)
            return Erro.Validacao("Os custos descontados e o IOF consumiram o crédito. Aumente o valor solicitado ou confira os descontos.");
        // BCB: prestações fixas com juros compostos e capitalização mensal.
        // https://www3.bcb.gov.br/CALCIDADAO/publico/exibirMetodologiaFinanciamentoPrestacoesFixas.do?method=exibirMetodologiaFinanciamentoPrestacoesFixas
        var f = Fluxo(principal, taxa, e.NumeroParcelas);
        if (f.Parcela <= 0m || f.Ultima <= 0m)
            return Erro.Validacao("O valor é insuficiente para o prazo. Aumente o valor solicitado ou reduza as parcelas.");
        var efetiva = TaxaEfetiva(liquido, f.Parcela, f.Ultima, e.NumeroParcelas);
        return new ApuracaoEmprestimoPessoal(principal, liquido, iof, principal * dias,
            f.Parcela, f.Ultima, f.Total, f.Juros, f.Total - liquido,
            (Potencia(1m + taxa, 12) - 1m) * 100m, efetiva * 100m,
            efetiva is { } c ? (Potencia(1m + c, 12) - 1m) * 100m : null,
            primeiro, primeiro.AddMonths(e.NumeroParcelas - 1));
    }
}
