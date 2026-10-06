using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class CalculadoraWindow : Window
{
    public CalculadoraWindow(CalculadoraViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        if (viewModel?.Campos.Any(campo => campo is CampoReajusteRetroativoViewModel) == true)
            Width = 940;
        if (viewModel?.Campos.Any(campo => campo is CampoMediaVerbasVariaveisViewModel or CampoBancoHorasViewModel) == true)
            MinWidth = 1100;
    }
}
