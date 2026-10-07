using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class LinhaQuitacaoBancoHorasViewModel(ParcelaQuitacaoBancoHoras parcela)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public string Adicional => parcela.Adicional.ToString("N2", Cultura) + "%";
    public string Horas => $"{parcela.Minutos / 60}:{parcela.Minutos % 60:00}";
    public string Valor => parcela.Valor.ToString("C2", Cultura);
    public string Incidencias => "INSS: sim  •  IRRF: sim  •  FGTS: sim";
}
