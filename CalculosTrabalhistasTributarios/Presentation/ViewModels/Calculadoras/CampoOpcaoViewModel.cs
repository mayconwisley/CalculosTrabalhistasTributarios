namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoOpcaoViewModel : CampoViewModel
{
    private OpcaoCampo _selecionada;

    public CampoOpcaoViewModel(string rotulo, IReadOnlyList<OpcaoCampo> opcoes, string? dica = null) : base(rotulo, dica)
    {
        Opcoes = opcoes;
        _selecionada = opcoes[0];
    }

    public IReadOnlyList<OpcaoCampo> Opcoes { get; }
    public double Largura { get; init; } = 170;
    public OpcaoCampo Selecionada
    {
        get => _selecionada;
        set
        {
            if (!SetProperty(ref _selecionada, value))
                return;
            MensagemErro = null;
            AoAlterar?.Invoke();
        }
    }
    public T Valor<T>() => (T)Selecionada.Valor;

    /// <summary>Chamado a cada mudança de opção, para mostrar ou ocultar os campos que dependem dela.</summary>
    public Action? AoAlterar { get; set; }

    public static CampoOpcaoViewModel SimNao(string rotulo, bool inicial, string? dica = null)
    {
        var campo = new CampoOpcaoViewModel(rotulo, [new("Não", false), new("Sim", true)], dica);
        campo.Selecionada = campo.Opcoes[inicial ? 1 : 0];
        return campo;
    }
}
