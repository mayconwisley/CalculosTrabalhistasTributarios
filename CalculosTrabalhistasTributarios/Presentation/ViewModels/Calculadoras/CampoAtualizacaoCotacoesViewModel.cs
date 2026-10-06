using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoAtualizacaoCotacoesViewModel(ICommand comando) : CampoViewModel("Cotações online", "Busca o dólar fiscal na Receita e, para outra moeda, uma referência PTAX do Banco Central na data informada.")
{
    private string _estado = "Informe a data e atualize. Câmbio efetivo, taxas, juros e imposto retido vêm do seu comprovante.";

    public ICommand Comando { get; } = comando;
    public string Estado { get => _estado; set => SetProperty(ref _estado, value); }
}
