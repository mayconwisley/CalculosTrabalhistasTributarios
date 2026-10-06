using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class VinculoInssLinhaViewModel : ViewModelBase
{
    public static IReadOnlyList<OpcaoCampo> Categorias { get; } =
    [
        new("Empregado", TipoVinculoInss.Empregado),
        new("Doméstico", TipoVinculoInss.Domestico),
        new("Avulso", TipoVinculoInss.Avulso),
        new("Individual (11%)", TipoVinculoInss.ContribuinteIndividual),
        new("Individual EBAS (20%)", TipoVinculoInss.ContribuinteIndividualEbas)
    ];

    private int _numero;
    private OpcaoCampo _categoria = Categorias[0];
    private string _remuneracao = "0,00";
    private string _identificacao = string.Empty;
    private bool _podeSubir;
    private bool _podeDescer;
    private bool _podeRemover;

    public IReadOnlyList<OpcaoCampo> Opcoes => Categorias;
    public int Numero { get => _numero; set => SetProperty(ref _numero, value); }
    public OpcaoCampo Categoria { get => _categoria; set { if (value is not null) SetProperty(ref _categoria, value); } }
    public string Remuneracao { get => _remuneracao; set => SetProperty(ref _remuneracao, value); }
    public string Identificacao { get => _identificacao; set => SetProperty(ref _identificacao, value); }
    public bool PodeSubir { get => _podeSubir; set => SetProperty(ref _podeSubir, value); }
    public bool PodeDescer { get => _podeDescer; set => SetProperty(ref _podeDescer, value); }
    public bool PodeRemover { get => _podeRemover; set => SetProperty(ref _podeRemover, value); }
}
