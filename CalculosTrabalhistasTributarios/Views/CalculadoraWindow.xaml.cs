using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CalculosTrabalhistasTributarios.Views;

public partial class CalculadoraWindow : Window
{
    // Largura do conteúdo a partir da qual o resultado vai para o lado do formulário; abaixo da segunda, volta para baixo.
    // A diferença evita alternar sem parar quando a barra de rolagem aparece e estreita o conteúdo.
    private const double LarguraResultadoAoLado = 1400;
    private const double LarguraResultadoAbaixo = 1370;

    private readonly CalculadoraViewModel _viewModel;
    private bool _resultadoAoLado;

    public CalculadoraWindow(CalculadoraViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        if (viewModel.Campos.Any(campo => campo is CampoReajusteRetroativoViewModel or CampoVinculosInssViewModel))
        {
            Width = 940;
            MinWidth = 920;
        }
        if (viewModel.Campos.Any(campo => campo is CampoMediaVerbasVariaveisViewModel or CampoBancoHorasViewModel))
            MinWidth = 1100;
        JanelaLembrada.SetChave(this, viewModel.TipoHistorico);

        viewModel.ResultadoApresentado += MostrarResultado;
        viewModel.CampoComErro += FocarCampo;
        // O formulário como foi aberto, já com os dados trazidos de outra janela, é a referência para o aviso do Esc.
        Loaded += (_, _) => viewModel.MarcarComoSalvo();
        Closed += (_, _) =>
        {
            viewModel.ResultadoApresentado -= MostrarResultado;
            viewModel.CampoComErro -= FocarCampo;
        };

        // As grades dos campos compostos não avisam cada edição ao ViewModel; os eventos da tela cobrem todos os controles.
        Formulario.AddHandler(TextBoxBase.TextChangedEvent, new RoutedEventHandler(AoEditar));
        Formulario.AddHandler(Selector.SelectionChangedEvent, new RoutedEventHandler(AoEditar));
        Formulario.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(AoEditar), handledEventsToo: true);
        Formulario.AddHandler(UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(AoSairDoCampo));
        if (viewModel.PermiteResultadoAoLado)
            Leiaute.SizeChanged += (_, _) => DisporResultado();
    }

    private void DisporResultado()
    {
        var aoLado = _resultadoAoLado ? Leiaute.ActualWidth >= LarguraResultadoAbaixo : Leiaute.ActualWidth >= LarguraResultadoAoLado;
        if (aoLado == _resultadoAoLado)
            return;
        _resultadoAoLado = aoLado;
        EspacoEntreColunas.Width = new GridLength(aoLado ? 20 : 0);
        ColunaResultado.Width = aoLado ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetRowSpan(CartaoFormulario, aoLado ? 3 : 1);
        CartaoFormulario.VerticalAlignment = aoLado ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        Grid.SetRow(CartaoResultado, aoLado ? 2 : 4);
        Grid.SetRowSpan(CartaoResultado, aoLado ? 3 : 1);
        Grid.SetColumn(CartaoResultado, aoLado ? 2 : 0);
    }

    // O formato errado aparece ao sair do campo, sem esperar o Calcular; depois do binding, que entrega o texto ao sair.
    private void AoSairDoCampo(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OriginalSource is TextBox { DataContext: CampoTextoViewModel campo })
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => LeitorFormulario.ConferirFormato(campo));
    }

    // Depois da atualização dos bindings, para comparar o formulário já com o valor digitado.
    private void AoEditar(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Background, _viewModel.VerificarAlteracoes);

    // Esc fecha a janela, depois dos controles: uma lista aberta de um ComboBox trata o Esc antes e só se fecha.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
            return;
        e.Handled = true;
        if (_viewModel.ConfirmarFechamento())
            Close();
    }

    // Rola só o necessário para o início do resultado aparecer; com o resultado já à vista, a tela não se move.
    private void MostrarResultado() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
    {
        var altura = Math.Min(CartaoResultado.ActualHeight, Pagina.ViewportHeight);
        CartaoResultado.BringIntoView(new Rect(0, 0, CartaoResultado.ActualWidth, altura));
    });

    private void FocarCampo(CampoViewModel campo) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
    {
        if (Formulario.ItemContainerGenerator.ContainerFromItem(campo) is not DependencyObject container || Controle(container) is not { } controle)
            return;
        controle.BringIntoView();
        controle.Focus();
        if (controle is TextBox texto)
            texto.SelectAll();
    });

    private static Control? Controle(DependencyObject elemento)
    {
        for (var indice = 0; indice < VisualTreeHelper.GetChildrenCount(elemento); indice++)
        {
            var filho = VisualTreeHelper.GetChild(elemento, indice);
            if (filho is TextBox or ComboBox)
                return (Control)filho;
            if (Controle(filho) is { } encontrado)
                return encontrado;
        }
        return null;
    }
}
