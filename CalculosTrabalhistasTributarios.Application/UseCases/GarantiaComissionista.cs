using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

internal static class GarantiaComissionista
{
    public static Result<decimal> ObterPiso(TabelasDaCompetencia tabelas, decimal pisoInformado, bool periodoPersonalizado)
    {
        if (pisoInformado < 0m)
            return Erro.Validacao("A garantia mínima do período não pode ser negativa.");
        if (periodoPersonalizado)
            return pisoInformado > 0m ? pisoInformado : Erro.Validacao("Para um período ou escala com dias informados, informe a garantia mínima aplicável ao período.");
        var minimo = tabelas.ObterSalarioMinimo();
        if (minimo.Falhou)
            return minimo.Erro;
        if (pisoInformado > 0m && pisoInformado < minimo.Valor)
            return Erro.Validacao("A garantia mínima de um mês completo não pode ser inferior ao salário mínimo vigente.");
        return pisoInformado > 0m ? pisoInformado : minimo.Valor;
    }
}
