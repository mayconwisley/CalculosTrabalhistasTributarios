using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class ParcelaReajusteLinhaViewModel(TipoParcelaReajuste tipo) : ViewModelBase
{
    private string _competencia = DateTime.Today.ToString("MM/yyyy");
    private string _basePaga = "0,00";
    private string _baseDevida = "0,00";
    private string _quantidade = "12";

    public TipoParcelaReajuste Tipo { get; } = tipo;
    public string TipoTexto => Tipo switch
    {
        TipoParcelaReajuste.Salario => "Salário",
        TipoParcelaReajuste.DecimoTerceiro => "13º salário",
        _ => "Férias gozadas + 1/3"
    };
    public string Unidade => Tipo == TipoParcelaReajuste.DecimoTerceiro ? "Avos" : "Dias";
    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public string BasePaga { get => _basePaga; set => SetProperty(ref _basePaga, value); }
    public string BaseDevida { get => _baseDevida; set => SetProperty(ref _baseDevida, value); }
    public string Quantidade { get => _quantidade; set => SetProperty(ref _quantidade, value); }
}
