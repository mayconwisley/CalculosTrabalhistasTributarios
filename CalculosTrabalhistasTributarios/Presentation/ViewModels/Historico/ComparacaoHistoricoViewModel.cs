using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

/// <summary>
/// Dois cálculos salvos da mesma calculadora lado a lado: os resultados recalculados, quando a calculadora usa a janela
/// padrão, e os dados informados em cada um.
/// </summary>
public sealed class ComparacaoHistoricoViewModel : ViewModelBase
{
    private readonly IReadOnlyList<LinhaComparacaoHistoricoViewModel> _todas;
    private readonly IReadOnlyList<LinhaResultadoComparadoViewModel> _resultados = [];
    private bool _somenteDiferencas;

    /// <param name="resultadoPrimeiro">Demonstrativo recalculado do primeiro; nulo quando a calculadora não tem um demonstrativo padrão.</param>
    /// <param name="cenarios">Dois cenários da mesma janela, já calculados nela, em vez de dois cálculos salvos refeitos agora.</param>
    public ComparacaoHistoricoViewModel(CalculoSalvoDto primeiro, CalculoSalvoDto segundo, Result<DemonstrativoDto>? resultadoPrimeiro = null, Result<DemonstrativoDto>? resultadoSegundo = null,
        bool cenarios = false)
    {
        Titulo = cenarios ? $"Comparar cenários · {primeiro.Calculadora}" : $"Comparar cálculos · {primeiro.Calculadora}";
        Cabecalho = cenarios ? "Comparação de cenários" : "Comparação de cálculos salvos";
        NomePrimeiro = primeiro.Nome;
        NomeSegundo = segundo.Nome;
        var entradasPrimeiro = Extrair(DadosFormulario.DeJson(primeiro.Dados));
        var entradasSegundo = Extrair(DadosFormulario.DeJson(segundo.Dados));
        _todas = entradasPrimeiro.Keys.Union(entradasSegundo.Keys).OrderBy(chave => chave, StringComparer.CurrentCultureIgnoreCase)
            .Select(chave => new LinhaComparacaoHistoricoViewModel(chave,
                entradasPrimeiro.GetValueOrDefault(chave, "Não informado"), entradasSegundo.GetValueOrDefault(chave, "Não informado"))).ToArray();

        if (resultadoPrimeiro is null || resultadoSegundo is null)
            MensagemResultados = "Esta calculadora tem janela própria: a comparação mostra os dados informados. Abra cada cálculo para ver o resultado.";
        else if (resultadoPrimeiro.Falhou || resultadoSegundo.Falhou)
            MensagemResultados = string.Join(" ", new[] { (primeiro, resultadoPrimeiro), (segundo, resultadoSegundo) }
                .Where(item => item.Item2.Falhou).Select(item => $"Não foi possível recalcular “{item.Item1.Nome}”: {item.Item2.Erro.Mensagem}"));
        else
        {
            _resultados = ComparacaoResultados.Linhas(resultadoPrimeiro.Valor, resultadoSegundo.Valor);
            MensagemResultados = cenarios
                ? $"O cenário A foi calculado às {primeiro.CriadoEm:HH:mm}; o B é o formulário atual. A diferença é a do cenário B para o A."
                : "Os dois cálculos foram refeitos agora, com as tabelas atuais da competência de cada um. A diferença é a do segundo para o primeiro.";
        }
    }

    public string Titulo { get; }
    public string Cabecalho { get; }
    public bool TemResultados => _resultados.Count > 0;
    public string MensagemResultados { get; }
    public IReadOnlyList<LinhaResultadoComparadoViewModel> LinhasResultado =>
        SomenteDiferencas ? _resultados.Where(linha => linha.Diferente).ToArray() : _resultados;
    public string NomePrimeiro { get; }
    public string NomeSegundo { get; }
    public bool SomenteDiferencas
    {
        get => _somenteDiferencas;
        set
        {
            if (!SetProperty(ref _somenteDiferencas, value)) return;
            OnPropertyChanged(nameof(Linhas));
            OnPropertyChanged(nameof(LinhasResultado));
        }
    }
    public IReadOnlyList<LinhaComparacaoHistoricoViewModel> Linhas =>
        SomenteDiferencas ? _todas.Where(linha => linha.Diferente).ToArray() : _todas;

    private static Dictionary<string, string> Extrair(DadosFormulario formulario)
    {
        var entradas = new Dictionary<string, string>();
        foreach (var (rotulo, valor) in formulario.Campos)
            Adicionar(entradas, rotulo, valor);
        if (formulario.Linhas is not null)
            for (var indice = 0; indice < formulario.Linhas.Count; indice++)
                foreach (var (rotulo, valor) in formulario.Linhas[indice])
                    Adicionar(entradas, $"Linha {indice + 1} · {rotulo}", valor);
        return entradas;
    }

    private static void Adicionar(Dictionary<string, string> entradas, string rotulo, string valor)
    {
        if (valor.Length > 0 && valor[0] is '{' or '[')
        {
            try
            {
                using var documento = JsonDocument.Parse(valor);
                Desdobrar(entradas, rotulo, documento.RootElement);
                return;
            }
            catch (JsonException)
            {
                // Históricos antigos podem conter texto que começa por colchete sem ser JSON.
            }
        }
        entradas[rotulo] = valor;
    }

    private static void Desdobrar(Dictionary<string, string> entradas, string caminho, JsonElement elemento)
    {
        if (elemento.ValueKind == JsonValueKind.Object)
        {
            foreach (var propriedade in elemento.EnumerateObject())
                Desdobrar(entradas, $"{caminho} · {propriedade.Name}", propriedade.Value);
        }
        else if (elemento.ValueKind == JsonValueKind.Array)
        {
            var indice = 0;
            foreach (var item in elemento.EnumerateArray())
                Desdobrar(entradas, $"{caminho} · linha {++indice}", item);
            if (indice == 0)
                entradas[caminho] = "Sem linhas";
        }
        else
            entradas[caminho] = elemento.ToString();
    }
}
