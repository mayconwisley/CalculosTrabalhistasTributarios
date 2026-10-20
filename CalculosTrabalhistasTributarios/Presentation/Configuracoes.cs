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

    /// <summary>Na primeira abertura do dia, atualiza todas as tabelas pela internet na tela de abertura.</summary>
    public bool AtualizarTabelasAoAbrir { get; set; } = true;

    /// <summary>Dia da última atualização ao abrir em que alguma fonte respondeu.</summary>
    public DateOnly? UltimaAtualizacaoTabelas { get; set; }

    /// <summary>Chaves dos cartões favoritos da tela inicial, na ordem em que foram marcados.</summary>
    public List<string> Favoritos { get => _favoritos; set => _favoritos = value ?? []; }

    /// <summary>Último tamanho e posição de cada janela, pela chave da calculadora, como "Calculadora.Rescisao".</summary>
    public Dictionary<string, PosicaoJanela> Janelas { get => _janelas; set => _janelas = value ?? []; }

    private List<string> _favoritos = [];
    private Dictionary<string, PosicaoJanela> _janelas = [];
}
