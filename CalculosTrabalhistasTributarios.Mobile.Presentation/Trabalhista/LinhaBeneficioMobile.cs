using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class LinhaBeneficioMobile : ObservableObject
{
    private string _descricao = "";
    private string _custo = "0,00";
    private string _participacao = "0,00";

    public string Descricao { get => _descricao; set => SetProperty(ref _descricao, value ?? string.Empty); }
    public string Custo { get => _custo; set => SetProperty(ref _custo, value ?? string.Empty); }
    public string Participacao { get => _participacao; set => SetProperty(ref _participacao, value ?? string.Empty); }
}
