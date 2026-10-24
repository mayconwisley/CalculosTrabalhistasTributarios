using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using System.Globalization;
using System.Windows.Input;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Views;

public partial class ResultadoSimulacaoView : ContentView
{
    public static readonly BindableProperty DemonstrativoProperty = BindableProperty.Create(nameof(Demonstrativo),
        typeof(DemonstrativoDto), typeof(ResultadoSimulacaoView), null, propertyChanged: AoAlterarDemonstrativo);
    public static readonly BindableProperty CompartilharPdfCommandProperty = BindableProperty.Create(nameof(CompartilharPdfCommand),
        typeof(ICommand), typeof(ResultadoSimulacaoView));
    public ResultadoSimulacaoView() => InitializeComponent();
    public DemonstrativoDto? Demonstrativo { get => (DemonstrativoDto?)GetValue(DemonstrativoProperty); set => SetValue(DemonstrativoProperty, value); }
    public ICommand? CompartilharPdfCommand { get => (ICommand?)GetValue(CompartilharPdfCommandProperty); set => SetValue(CompartilharPdfCommandProperty, value); }
    private DetalhesResultadoMobile? _detalhes;
    public DetalhesResultadoMobile? Detalhes => _detalhes ??= Demonstrativo is { } dto ? new(dto) : null;
    public bool TemResultado => Demonstrativo is not null;
    public bool TemComparativo => Demonstrativo?.Comparativo is not null;
    public bool TemVerbas => Demonstrativo is { } demonstrativo && (demonstrativo.TemVerbas || demonstrativo.Comparativo is null);
    public bool TemInformativos => Demonstrativo?.Informativos.Count > 0;
    public string RotuloResultado => Demonstrativo?.Comparativo is { } comparativo && !TemVerbas
        ? comparativo.Titulo : Demonstrativo?.RotuloResultado ?? string.Empty;
    public string ResultadoFormatado => TemVerbas && Demonstrativo is { } demonstrativo
        ? demonstrativo.Resultado.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")) : string.Empty;
    private static void AoAlterarDemonstrativo(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (ResultadoSimulacaoView)bindable;
        view._detalhes = null;
        view.OnPropertyChanged(nameof(Detalhes));
        view.OnPropertyChanged(nameof(TemResultado));
        view.OnPropertyChanged(nameof(TemComparativo));
        view.OnPropertyChanged(nameof(TemVerbas));
        view.OnPropertyChanged(nameof(TemInformativos));
        view.OnPropertyChanged(nameof(RotuloResultado));
        view.OnPropertyChanged(nameof(ResultadoFormatado));
    }
}
