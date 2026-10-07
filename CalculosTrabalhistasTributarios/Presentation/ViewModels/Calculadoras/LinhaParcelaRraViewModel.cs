using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Competência a que se refere uma parte do pagamento acumulado, com o valor tributável recebido.</summary>
public sealed class LinhaParcelaRraViewModel : ViewModelBase
{
    public static IReadOnlyList<OpcaoCampo> Tipos { get; } = [new("Mensal", TipoParcelaRra.Mensal), new("13º salário", TipoParcelaRra.DecimoTerceiro)];

    private string _competencia = string.Empty;
    private OpcaoCampo _tipo = Tipos[0];
    private string _valor = "0,00";

    public IReadOnlyList<OpcaoCampo> Opcoes => Tipos;
    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public OpcaoCampo Tipo { get => _tipo; set { if (value is not null) SetProperty(ref _tipo, value); } }
    public string Valor { get => _valor; set => SetProperty(ref _valor, value); }

    public override string ToString() => $"{Tipo.Texto} {Competencia}".Trim();
}
