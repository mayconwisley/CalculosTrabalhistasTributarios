using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

public sealed class ComparacaoHistoricoViewModel : ViewModelBase
{
    private readonly IReadOnlyList<LinhaComparacaoHistoricoViewModel> _todas;
    private bool _somenteDiferencas;

    public ComparacaoHistoricoViewModel(CalculoSalvoDto primeiro, CalculoSalvoDto segundo)
    {
        Titulo = $"Comparar entradas · {primeiro.Calculadora}";
        NomePrimeiro = primeiro.Nome;
        NomeSegundo = segundo.Nome;
        var entradasPrimeiro = Extrair(DadosFormulario.DeJson(primeiro.Dados));
        var entradasSegundo = Extrair(DadosFormulario.DeJson(segundo.Dados));
        _todas = entradasPrimeiro.Keys.Union(entradasSegundo.Keys).OrderBy(chave => chave, StringComparer.CurrentCultureIgnoreCase)
            .Select(chave => new LinhaComparacaoHistoricoViewModel(chave,
                entradasPrimeiro.GetValueOrDefault(chave, "Não informado"), entradasSegundo.GetValueOrDefault(chave, "Não informado"))).ToArray();
    }

    public string Titulo { get; }
    public string NomePrimeiro { get; }
    public string NomeSegundo { get; }
    public bool SomenteDiferencas
    {
        get => _somenteDiferencas;
        set { if (SetProperty(ref _somenteDiferencas, value)) OnPropertyChanged(nameof(Linhas)); }
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
