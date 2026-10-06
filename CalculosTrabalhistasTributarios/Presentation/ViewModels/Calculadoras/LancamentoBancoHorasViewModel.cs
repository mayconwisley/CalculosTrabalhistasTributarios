using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class LancamentoBancoHorasViewModel : ViewModelBase
{
    public static IReadOnlyList<OpcaoCampo> Tipos { get; } =
    [
        new("Horas trabalhadas", TipoLancamentoBancoHoras.Credito),
        new("Folga ou redução", TipoLancamentoBancoHoras.Compensacao)
    ];

    private string _data = DateTime.Today.ToString("dd/MM/yyyy");
    private OpcaoCampo _tipo = Tipos[0];
    private string _horas = "0:00";
    private string _descricao = string.Empty;

    public string Data { get => _data; set => SetProperty(ref _data, value); }
    public OpcaoCampo Tipo { get => _tipo; set { if (value is not null) SetProperty(ref _tipo, value); } }
    public string Horas { get => _horas; set => SetProperty(ref _horas, value); }
    public string Descricao { get => _descricao; set => SetProperty(ref _descricao, value); }
    public IReadOnlyList<OpcaoCampo> Opcoes => Tipos;
}
