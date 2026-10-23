using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class LinhaLancamentoBancoHorasMobile : ObservableObject
{
    private string _data = "";
    private int _tipo;
    private string _horas = "0:00";
    private string _adicional = "";
    private string _descricao = "";

    /// <summary>Na mesma ordem de <see cref="Domain.Trabalhista.TipoLancamentoBancoHoras"/>.</summary>
    public IReadOnlyList<string> Tipos { get; } = ["Crédito (hora extra)", "Compensação (folga)"];
    public string Data { get => _data; set => SetProperty(ref _data, value ?? string.Empty); }
    public int Tipo
    {
        get => _tipo;
        set { if (SetProperty(ref _tipo, value)) OnPropertyChanged(nameof(EhCredito)); }
    }
    public bool EhCredito => Tipo == 0;
    public string Horas { get => _horas; set => SetProperty(ref _horas, value ?? string.Empty); }
    public string Adicional { get => _adicional; set => SetProperty(ref _adicional, value ?? string.Empty); }
    public string Descricao { get => _descricao; set => SetProperty(ref _descricao, value ?? string.Empty); }
}
