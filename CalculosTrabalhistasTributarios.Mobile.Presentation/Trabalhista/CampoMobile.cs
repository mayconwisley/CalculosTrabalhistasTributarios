using System.Runtime.CompilerServices;
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
    public string Valor { get => _valor; set => Alterar(ref _valor, value ?? string.Empty); }
    public int Indice { get => _indice; set => Alterar(ref _indice, value); }
    public bool Marcado { get => _marcado; set => Alterar(ref _marcado, value); }
    public bool Visivel { get => _visivel; set => SetProperty(ref _visivel, value); }

    // O erro sai antes de avisar a alteração: quem acompanha o campo já encontra o formulário sem ele.
    private void Alterar<T>(ref T atual, T novo, [CallerMemberName] string? propriedade = null)
    {
        if (EqualityComparer<T>.Default.Equals(atual, novo)) return;
        Erro = null;
        SetProperty(ref atual, novo, propriedade);
    }
}
