using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class CampoMobile(string chave, string rotulo, string dica, TipoCampoMobile tipo, string valor = "", IReadOnlyList<string>? opcoes = null) : ObservableObject
{
    private string _valor = valor;
    private int _indice;
    private bool _marcado;
    private bool _visivel = true;
    private string? _erro;

    public string Chave { get; } = chave;
    public string Rotulo { get; } = rotulo;
    public string Dica { get; } = dica;
    public TipoCampoMobile Tipo { get; } = tipo;
    public IReadOnlyList<string> Opcoes { get; } = opcoes ?? [];
    public bool EhTexto => Tipo is not (TipoCampoMobile.Moeda or TipoCampoMobile.Opcao or TipoCampoMobile.SimNao or TipoCampoMobile.Secao);
    /// <summary>Aceita só dígitos e separador decimal; datas, competências, horas e textos precisam do teclado de texto para "/", ":" e letras.</summary>
    public bool EhNumerico => Tipo is TipoCampoMobile.Numero or TipoCampoMobile.Inteiro;
    public bool EhSecao => Tipo == TipoCampoMobile.Secao;
    public bool EhEntrada => !EhSecao;
    public bool EhOpcao => Tipo == TipoCampoMobile.Opcao;
    public bool EhSimNao => Tipo == TipoCampoMobile.SimNao;
    public bool EhMoeda => Tipo == TipoCampoMobile.Moeda;
    public string? Erro
    {
        get => _erro;
        set { if (SetProperty(ref _erro, value)) OnPropertyChanged(nameof(TemErro)); }
    }
    public bool TemErro => !string.IsNullOrEmpty(Erro);
    public string Valor { get => _valor; set { if (SetProperty(ref _valor, value ?? string.Empty)) Erro = null; } }
    public int Indice { get => _indice; set { if (SetProperty(ref _indice, value)) Erro = null; } }
    public bool Marcado { get => _marcado; set { if (SetProperty(ref _marcado, value)) Erro = null; } }
    public bool Visivel { get => _visivel; set => SetProperty(ref _visivel, value); }
}
