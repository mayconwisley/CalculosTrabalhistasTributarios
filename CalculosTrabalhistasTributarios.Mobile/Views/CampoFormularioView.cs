using CalculosTrabalhistasTributarios.Mobile.Comportamentos;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Views;

/// <summary>Um único controle nativo por tipo, com rótulo, dica e erro acessíveis.</summary>
public sealed class CampoFormularioView : ContentView
{
    private View? _entrada;
    public CampoMobile? Campo => BindingContext as CampoMobile;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (Campo is not { } campo) { Content = null; return; }
        var recursos = Microsoft.Maui.Controls.Application.Current?.Resources
            ?? throw new InvalidOperationException("Recursos da aplicação indisponíveis.");
        this.SetBinding(IsVisibleProperty, nameof(CampoMobile.Visivel));
        var pilha = new VerticalStackLayout { Spacing = 6 };
        var rotulo = new Label { Text = campo.Rotulo, Style = (Style)recursos["RotuloCampo"] };
        pilha.Add(rotulo);
        _entrada = campo.Tipo switch
        {
            TipoCampoMobile.Opcao => CriarOpcao(campo),
            TipoCampoMobile.SimNao => CriarSimNao(),
            TipoCampoMobile.Data or TipoCampoMobile.Competencia => CriarPeriodo(campo),
            _ => CriarTexto(campo)
        };
        if (campo.EhSimNao) pilha.Add(_entrada);
        else
        {
            var borda = new Border { Style = (Style)recursos["CaixaCampo"], Content = _entrada };
            borda.Triggers.Add(new DataTrigger(typeof(Border))
            {
                Binding = new Binding(nameof(CampoMobile.TemErro)), Value = true,
                Setters = { new Setter { Property = Border.StrokeProperty, Value = recursos["ErroCampoBrush"] }, new Setter { Property = Border.StrokeThicknessProperty, Value = 2d } }
            });
            pilha.Add(borda);
        }
        SemanticProperties.SetDescription(_entrada, campo.Rotulo);
        if (_entrada is not EntradaPeriodoView) _entrada.AutomationId = "Campo_" + campo.Chave;
        var dica = new Label { Text = campo.Dica, Style = (Style)recursos["TextoSecundario"], IsVisible = !string.IsNullOrWhiteSpace(campo.Dica) };
        pilha.Add(dica);
        var erro = new Label { Style = (Style)recursos["TextoErroCampo"] };
        erro.SetBinding(Label.TextProperty, nameof(CampoMobile.Erro));
        erro.SetBinding(IsVisibleProperty, nameof(CampoMobile.TemErro));
        pilha.Add(erro);
        Content = pilha;
    }

    public void FocarEntrada()
    {
        if (_entrada is EntradaPeriodoView periodo) periodo.FocarEntrada();
        else _entrada?.Focus();
    }
    private static Entry CriarTexto(CampoMobile campo)
    {
        var entrada = new Entry { MaxLength = campo.EhMoeda ? 40 : 120, Keyboard = campo.EhNumerico || campo.EhMoeda ? Keyboard.Numeric : Keyboard.Text, ReturnType = ReturnType.Next };
        entrada.SetBinding(Entry.TextProperty, nameof(CampoMobile.Valor), BindingMode.TwoWay);
        if (campo.EhMoeda) entrada.Behaviors.Add(new EntradaMonetariaBehavior());
        else if (campo.EhNumerico) entrada.Behaviors.Add(new EntradaNumericaBehavior());
        if (campo.Tipo == TipoCampoMobile.Horas) entrada.Placeholder = "10:30 ou 10,5";
        if (campo.Tipo == TipoCampoMobile.Texto) entrada.HorizontalTextAlignment = TextAlignment.Start;
        return entrada;
    }
    private static Picker CriarOpcao(CampoMobile campo)
    {
        var entrada = new Picker { ItemsSource = campo.Opcoes.ToArray(), Title = "Selecione" };
        entrada.SetBinding(Picker.SelectedIndexProperty, nameof(CampoMobile.Indice), BindingMode.TwoWay);
        return entrada;
    }
    private static Switch CriarSimNao()
    {
        var entrada = new Switch { HorizontalOptions = LayoutOptions.Start };
        entrada.SetBinding(Switch.IsToggledProperty, nameof(CampoMobile.Marcado), BindingMode.TwoWay);
        return entrada;
    }
    private static EntradaPeriodoView CriarPeriodo(CampoMobile campo)
    {
        var entrada = new EntradaPeriodoView { Tipo = campo.Tipo, Rotulo = campo.Rotulo, Identificador = "Campo_" + campo.Chave };
        entrada.SetBinding(EntradaPeriodoView.TextoProperty, nameof(CampoMobile.Valor), BindingMode.TwoWay);
        return entrada;
    }
}
