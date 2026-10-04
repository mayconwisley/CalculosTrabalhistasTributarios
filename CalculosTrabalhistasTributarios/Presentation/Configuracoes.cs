namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>Preferências gravadas em settings.json.</summary>
/// <remarks>Os nomes em inglês são os do arquivo das versões anteriores e precisam continuar iguais.</remarks>
public sealed class Configuracoes
{
    public string Theme { get; set; } = ThemeMode.Automatico.ToString();
    public bool HardwareAcceleration { get; set; }

    /// <summary>O aviso de que os resultados são simulações, exibido na primeira abertura.</summary>
    public bool AvisoSimulacaoLido { get; set; }

    /// <summary>Consulta no GitHub, ao abrir, se há uma versão mais nova.</summary>
    public bool VerificarNovasVersoes { get; set; } = true;
}
