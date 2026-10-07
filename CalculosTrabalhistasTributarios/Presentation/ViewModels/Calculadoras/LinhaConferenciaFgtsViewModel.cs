using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Competência conferida: remuneração, base do FGTS, e o depósito que consta no extrato ou na guia.</summary>
public sealed class LinhaConferenciaFgtsViewModel : ViewModelBase
{
    public static IReadOnlyList<OpcaoCampo> Tipos { get; } = [new("Mensal", TipoCompetenciaFgts.Mensal), new("Rescisória", TipoCompetenciaFgts.Rescisoria)];

    private string _competencia = string.Empty;
    private OpcaoCampo _tipo = Tipos[0];
    private string _remuneracao = "0,00";
    private string _deposito = "0,00";

    public IReadOnlyList<OpcaoCampo> Opcoes => Tipos;
    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public OpcaoCampo Tipo { get => _tipo; set { if (value is not null) SetProperty(ref _tipo, value); } }
    public string Remuneracao { get => _remuneracao; set => SetProperty(ref _remuneracao, value); }
    public string Deposito { get => _deposito; set => SetProperty(ref _deposito, value); }

    public override string ToString() => $"{Competencia} {Tipo.Texto}".Trim();
}
