using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class FormatacaoNumericaTests
{
    [Theory]
    [InlineData("30,001")]
    [InlineData("100,001")]
    [InlineData("-0,001")]
    public void Perder_foco_preserva_entrada_rejeitada_pelo_validador(string texto) => EmSta(() =>
    {
        var vm = new BeneficiarioPensaoViewModel("Beneficiário", () => { });
        var campo = new TextBox();
        FormatacaoNumerica.SetFormato(campo, "N2");
        campo.SetBinding(TextBox.TextProperty, new Binding(nameof(vm.Percentual))
        {
            Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            ValidatesOnDataErrors = true
        });
        campo.Text = texto;
        campo.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Foco(campo, Keyboard.LostKeyboardFocusEvent);
        Assert.Equal(texto, campo.Text);
        Assert.Equal(texto, vm.Percentual);
        Assert.True(Validation.GetHasError(campo));
    });

    [Fact]
    public void Valor_valido_continua_formatado_ao_sair() => EmSta(() =>
    {
        var vm = new BeneficiarioPensaoViewModel("Beneficiário", () => { });
        var campo = new TextBox();
        FormatacaoNumerica.SetFormato(campo, "N2");
        campo.SetBinding(TextBox.TextProperty, new Binding(nameof(vm.Percentual))
        {
            Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            ValidatesOnDataErrors = true
        });
        campo.Text = "30";
        Foco(campo, Keyboard.LostKeyboardFocusEvent);
        Assert.Equal("30,00", campo.Text);
        Assert.Equal("30,00", vm.Percentual);
    });

    [Theory]
    [InlineData("0,00")]
    [InlineData("1234,5")]
    public void Campo_somente_leitura_nao_e_alterado_pelo_foco(string texto) => EmSta(() =>
    {
        var campo = new TextBox { IsReadOnly = true, Text = texto };
        FormatacaoNumerica.SetFormato(campo, "N2");
        Foco(campo, Keyboard.GotKeyboardFocusEvent);
        Assert.Equal(texto, campo.Text);
        Foco(campo, Keyboard.LostKeyboardFocusEvent);
        Assert.Equal(texto, campo.Text);
    });

    [Fact]
    public void Zero_editavel_limpa_e_retorna_ao_sair() => EmSta(() =>
    {
        var campo = new TextBox { Text = "0,00" };
        FormatacaoNumerica.SetFormato(campo, "N2");
        Foco(campo, Keyboard.GotKeyboardFocusEvent);
        Assert.Equal("", campo.Text);
        Foco(campo, Keyboard.LostKeyboardFocusEvent);
        Assert.Equal("0,00", campo.Text);
    });

    private static void Foco(TextBox campo, RoutedEvent evento) => campo.RaiseEvent(
        new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, campo, campo) { RoutedEvent = evento });

    private static void EmSta(Action acao)
    {
        Exception? falha = null;
        var thread = new Thread(() =>
        {
            try { acao(); }
            catch (Exception exception) { falha = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (falha is not null)
            ExceptionDispatchInfo.Capture(falha).Throw();
    }
}
