using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class TabelaManutencaoWindow : Window
{
    public TabelaManutencaoWindow(TabelaManutencaoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        JanelaLembrada.SetChave(this, $"Tabela.{viewModel.Tipo}");
        Loaded += (_, _) => ((TabelaManutencaoViewModel)DataContext).CarregarCommand.Execute(null);
    }
}
