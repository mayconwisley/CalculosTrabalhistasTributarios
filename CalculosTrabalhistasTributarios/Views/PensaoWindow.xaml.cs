using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace CalculosTrabalhistasTributarios.Views;

public partial class PensaoWindow : Window
{
    public PensaoWindow(PensaoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // Com a lista rolando, o beneficiário incluído fica à vista.
        viewModel.Beneficiarios.CollectionChanged += (_, argumentos) =>
        {
            if (argumentos.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.BeginInvoke(ListaBeneficiarios.ScrollToEnd, DispatcherPriority.Loaded);
        };
    }
}
