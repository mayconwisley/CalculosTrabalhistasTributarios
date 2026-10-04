using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Uma parcela da pensão em atraso: o valor devido e o pago são editáveis; o resto vem do cálculo.</summary>
public sealed class ParcelaAtrasoViewModel : ViewModelBase
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly Action _aoAlterar;
    private string _devido;
    private string _pago;
    private ParcelaAtrasoDto? _resultado;

    /// <param name="aoAlterar">Avisa a janela de que o resultado ficou desatualizado.</param>
    public ParcelaAtrasoViewModel(ParcelaPensaoInformada parcela, Action aoAlterar)
    {
        Competencia = parcela.Competencia;
        Vencimento = parcela.Vencimento;
        _devido = parcela.Devido.ToString("N2", Cultura);
        _pago = parcela.Pago.ToString("N2", Cultura);
        _aoAlterar = aoAlterar;
    }

    public DateOnly Competencia { get; }
    public DateOnly Vencimento { get; }
    public string Mes => Competencia.ToString("MM/yyyy", Cultura);
    public string DataVencimento => Vencimento.ToString("dd/MM/yyyy", Cultura);
    public string Devido { get => _devido; set { if (SetProperty(ref _devido, value)) _aoAlterar(); } }
    public string Pago { get => _pago; set { if (SetProperty(ref _pago, value)) _aoAlterar(); } }

    public string Saldo => _resultado is null ? "" : Moeda(_resultado.Saldo);
    public string Fator => _resultado?.FatorCorrecao.ToString("N6", Cultura) ?? "";
    public string Corrigido => _resultado is null ? "" : Moeda(_resultado.Corrigido);
    public string PercentualJuros => _resultado is null ? "" : _resultado.PercentualJuros.ToString("N4", Cultura) + "%";
    public string Juros => _resultado is null ? "" : Moeda(_resultado.Juros);
    public string Total => _resultado is null ? "" : Moeda(_resultado.Total);
    public string Rito => _resultado is null ? "" : _resultado.RitoPrisao ? "Prisão" : "Penhora";
    public bool RitoPrisao => _resultado?.RitoPrisao == true;

    /// <summary>Lê os valores digitados; falso com a mensagem do que corrigir.</summary>
    public bool TentarLer(out ParcelaPensaoInformada parcela, out string erro)
    {
        parcela = default!;
        erro = string.Empty;
        if (!LeituraNumerica.TentarLer(Devido, out var devido) || !LeituraNumerica.TentarLer(Pago, out var pago))
        {
            erro = $"Informe o valor devido e o valor pago da parcela de {Mes} em formato válido, por exemplo 1.000,00.";
            return false;
        }
        parcela = new ParcelaPensaoInformada(Competencia, Vencimento, devido, pago);
        return true;
    }

    /// <summary>Nome da linha da grade para leitores de tela.</summary>
    public override string ToString() => Mes;

    public void Apresentar(ParcelaAtrasoDto? resultado)
    {
        _resultado = resultado;
        foreach (var propriedade in new[] { nameof(Saldo), nameof(Fator), nameof(Corrigido), nameof(PercentualJuros), nameof(Juros), nameof(Total), nameof(Rito), nameof(RitoPrisao) })
            OnPropertyChanged(propriedade);
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
}
