using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Uma linha de beneficiário na janela de pensão: o nome e a regra da pensão dele.</summary>
public sealed class BeneficiarioPensaoViewModel : ViewModelBase, System.ComponentModel.IDataErrorInfo
{
    private readonly Action _aoEscolherSalarioMinimo;
    private string _nome;
    private OpcaoCampo _baseSelecionada;
    private string _percentual = "0,00";
    private string _valorPensao = "0,00";
    private string _salarioMinimo = string.Empty;
    private string _rotuloSalarioMinimo = "Salário mínimo";

    /// <param name="aoEscolherSalarioMinimo">Avisa a janela para buscar o salário mínimo da competência.</param>
    public BeneficiarioPensaoViewModel(string nome, Action aoEscolherSalarioMinimo)
    {
        _nome = nome;
        _aoEscolherSalarioMinimo = aoEscolherSalarioMinimo;
        _baseSelecionada = BasesPensao[0];
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Percentual) or nameof(ValorPensao) or nameof(BaseSelecionada))
                OnPropertyChanged("Item[]");
        };
    }

    /// <summary>Sobre o que a pensão incide, conforme a decisão judicial ou o acordo.</summary>
    public IReadOnlyList<OpcaoCampo> BasesPensao { get; } =
    [
        new("% dos rendimentos líquidos", BasePensao.RendimentosLiquidos),
        new("% dos rendimentos brutos", BasePensao.RendimentosBrutos),
        new("% do salário mínimo", BasePensao.SalarioMinimo),
        new("Valor fixo", BasePensao.ValorFixo)
    ];

    public string Error => string.Empty;
    public string this[string campo]
    {
        get
        {
            if (campo != (EhPercentual ? nameof(Percentual) : nameof(ValorPensao)))
                return "";
            var texto = EhPercentual ? Percentual : ValorPensao;
            if (!LeituraNumerica.TentarLer(texto, out var valor))
                return "Informe um número válido.";
            var regra = EhPercentual ? new RegraPensao(Base, valor, 0m) : new RegraPensao(Base, 0m, valor);
            var validacao = regra.Validar();
            return validacao.Falhou ? validacao.Erro.Mensagem : "";
        }
    }

    public string Nome { get => _nome; set => SetProperty(ref _nome, value); }

    public OpcaoCampo BaseSelecionada
    {
        get => _baseSelecionada;
        set
        {
            if (!SetProperty(ref _baseSelecionada, value))
                return;
            OnPropertyChanged(nameof(EhPercentual));
            OnPropertyChanged(nameof(EhValorFixo));
            OnPropertyChanged(nameof(EhSalarioMinimo));
            if (EhSalarioMinimo)
                _aoEscolherSalarioMinimo();
        }
    }

    internal void DefinirBaseSemConsulta(OpcaoCampo opcao)
    {
        _baseSelecionada = opcao;
        OnPropertyChanged(nameof(BaseSelecionada));
        OnPropertyChanged(nameof(EhPercentual));
        OnPropertyChanged(nameof(EhValorFixo));
        OnPropertyChanged(nameof(EhSalarioMinimo));
    }

    public BasePensao Base => (BasePensao)BaseSelecionada.Valor;

    /// <summary>O percentual vale para as bases percentuais; o valor da pensão, só para o valor fixo.</summary>
    public bool EhPercentual => Base != BasePensao.ValorFixo;
    public bool EhValorFixo => !EhPercentual;
    public bool EhSalarioMinimo => Base == BasePensao.SalarioMinimo;
    public string Percentual { get => _percentual; set => SetProperty(ref _percentual, value); }
    public string ValorPensao { get => _valorPensao; set => SetProperty(ref _valorPensao, value); }

    /// <summary>Salário mínimo vigente na competência, só para leitura, exibido na base "% do salário mínimo".</summary>
    public string SalarioMinimo { get => _salarioMinimo; set => SetProperty(ref _salarioMinimo, value); }
    public string RotuloSalarioMinimo { get => _rotuloSalarioMinimo; set => SetProperty(ref _rotuloSalarioMinimo, value); }
}
