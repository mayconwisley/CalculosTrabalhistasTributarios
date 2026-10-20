using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Views;

/// <summary>Tela exibida enquanto o aplicativo prepara o banco e atualiza as tabelas; sem borda, pode ser arrastada.</summary>
public partial class AberturaWindow : Window
{
    public AberturaWindow(AberturaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        MouseLeftButtonDown += (_, evento) =>
        {
            if (evento.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };
    }
}
