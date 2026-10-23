using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>Cálculo salvo mostrado em "Continuar de onde parou", como nos recentes da tela inicial do desktop.</summary>
public sealed record CalculoRecenteMobile(CalculoSalvoDto Calculo, string Quando)
{
    public string Nome => Calculo.Nome;
    public string Calculadora => Calculo.Calculadora;
    public string DescricaoAcessivel => $"Abrir o cálculo recente {Nome}, {Calculadora}, alterado {Quando}";

    public static CalculoRecenteMobile Criar(CalculoSalvoDto calculo, DateTime hoje) => new(calculo, Descrever(calculo.AlteradoEm, hoje));

    /// <summary>Forma curta: "hoje às 14:30", "ontem às 09:10" ou a data.</summary>
    public static string Descrever(DateTime alteradoEm, DateTime hoje) => (hoje.Date - alteradoEm.Date).Days switch
    {
        0 => $"hoje às {alteradoEm:HH:mm}",
        1 => $"ontem às {alteradoEm:HH:mm}",
        _ => alteradoEm.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))
    };
}
