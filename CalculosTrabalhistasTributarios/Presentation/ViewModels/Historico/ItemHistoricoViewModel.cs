using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

public sealed class ItemHistoricoViewModel(CalculoSalvoDto calculo) : ViewModelBase
{
    private bool _selecionado;
    public CalculoSalvoDto Calculo { get; } = calculo;
    public string Nome => Calculo.Nome;
    public string Detalhe => $"{Calculo.Calculadora} • alterado em {Calculo.AlteradoEm:dd/MM/yyyy} às {Calculo.AlteradoEm:HH:mm}";

    /// <summary>Quando foi alterado, de forma curta para os recentes: "hoje às 14:30", "ontem às 09:10" ou a data.</summary>
    public string Quando => Descrever(Calculo.AlteradoEm, DateTime.Today);

    public static string Descrever(DateTime alteradoEm, DateTime hoje) => (hoje - alteradoEm.Date).Days switch
    {
        0 => $"hoje às {alteradoEm:HH:mm}",
        1 => $"ontem às {alteradoEm:HH:mm}",
        _ => alteradoEm.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))
    };
    public bool Selecionado { get => _selecionado; set => SetProperty(ref _selecionado, value); }
}
