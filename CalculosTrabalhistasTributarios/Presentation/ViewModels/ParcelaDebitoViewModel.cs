using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Uma parcela do débito: a descrição, o vencimento e o valor são editáveis; o resto vem do cálculo.</summary>
public sealed class ParcelaDebitoViewModel(string descricao, string vencimento, string valor, Action aoAlterar) : ViewModelBase
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private string _descricao = descricao;
    private string _vencimento = vencimento;
    private string _valor = valor;
    private ParcelaDebitoDto? _resultado;

    public string Descricao { get => _descricao; set { if (SetProperty(ref _descricao, value)) aoAlterar(); } }
    public string Vencimento { get => _vencimento; set { if (SetProperty(ref _vencimento, value)) aoAlterar(); } }
    public string Valor { get => _valor; set { if (SetProperty(ref _valor, value)) aoAlterar(); } }

    public string Fator => _resultado?.FatorCorrecao.ToString("N6", Cultura) ?? "";
    public string Selic => _resultado is { PercentualSelic: > 0m } r ? r.PercentualSelic.ToString("N4", Cultura) + "%" : "";
    public string Atualizado => _resultado is null ? "" : _resultado.Atualizado.ToString("C2", Cultura);
    public string PercentualJuros => _resultado is null ? "" : _resultado.PercentualJuros.ToString("N4", Cultura) + "%";
    public string Juros => _resultado is null ? "" : _resultado.Juros.ToString("C2", Cultura);
    public string Total => _resultado is null ? "" : _resultado.Total.ToString("C2", Cultura);

    public bool TentarLer(int numero, out ParcelaDebito parcela, out string erro)
    {
        parcela = default!;
        erro = string.Empty;
        if (!DateOnly.TryParseExact(Vencimento.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var vencimento))
        {
            erro = $"Informe o vencimento da parcela {numero} no formato dd/mm/aaaa.";
            return false;
        }
        if (!LeituraNumerica.TentarLer(Valor, out var valor))
        {
            erro = $"Informe o valor da parcela {numero} em formato válido, por exemplo 1.000,00.";
            return false;
        }
        parcela = new ParcelaDebito(string.IsNullOrWhiteSpace(Descricao) ? $"Parcela {numero}" : Descricao.Trim(), vencimento, valor);
        return true;
    }

    /// <summary>Nome da linha da grade para leitores de tela.</summary>
    public override string ToString() => $"{Descricao} {Vencimento}".Trim();

    public void Apresentar(ParcelaDebitoDto? resultado)
    {
        _resultado = resultado;
        foreach (var propriedade in new[] { nameof(Fator), nameof(Selic), nameof(Atualizado), nameof(PercentualJuros), nameof(Juros), nameof(Total) })
            OnPropertyChanged(propriedade);
    }
}
