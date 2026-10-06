using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

public static class DemonstrativoComissoes
{
    public static IReadOnlyList<FormulaDto> Formulas(ComissoesApuradas apuracao)
    {
        var dias = $"{apuracao.DiasUteis} dias úteis e {apuracao.DescansosPagos} descansos remunerados";
        var origem = apuracao.DiasInformados ? "Dias informados" : "Calendário da competência";
        var formulas = new List<FormulaDto>
        {
            new(origem, apuracao.DescansosPerdidos > 0
                ? $"{dias} ({apuracao.DiasDescanso} previstos - {apuracao.DescansosPerdidos} perdidos)"
                : dias)
        };
        if (apuracao.IncluiDsr)
        {
            formulas.Add(new("Comissões sem DSR", $"{Formato.Moeda(apuracao.ValorInformado)} (total informado) x {apuracao.DiasUteis} ÷ ({apuracao.DiasUteis} + {apuracao.DescansosPagos}) = {Formato.Moeda(apuracao.Comissoes)}"));
            formulas.Add(new("DSR já incluído", $"{Formato.Moeda(apuracao.ValorInformado)} - {Formato.Moeda(apuracao.Comissoes)} = {Formato.Moeda(apuracao.Dsr)}"));
        }
        else
        {
            formulas.Add(new("DSR das comissões", $"{Formato.Moeda(apuracao.Comissoes)} ÷ {apuracao.DiasUteis} dias úteis x {apuracao.DescansosPagos} descansos = {Formato.Moeda(apuracao.Dsr)}"));
        }
        formulas.Add(new("Total tributável", $"{Formato.Moeda(apuracao.Comissoes)} + {Formato.Moeda(apuracao.Dsr)} = {Formato.Moeda(apuracao.Total)}"));
        return formulas;
    }
}
