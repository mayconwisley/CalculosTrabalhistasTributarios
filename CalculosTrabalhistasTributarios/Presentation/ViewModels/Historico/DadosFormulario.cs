using System.Text.Encodings.Web;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

/// <summary>
/// Valores do formulário como o usuário digitou, por nome do campo, e as linhas das listas, como os beneficiários da
/// pensão ou as parcelas em atraso. Guardar o texto digitado, e não o valor lido, reabre o cálculo exatamente como estava.
/// </summary>
public sealed record DadosFormulario(Dictionary<string, string> Campos, List<Dictionary<string, string>>? Linhas = null)
{
    private static readonly JsonSerializerOptions Opcoes = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string Valor(string campo, string padrao = "") => Campos.TryGetValue(campo, out var valor) ? valor : padrao;

    public string ParaJson() => JsonSerializer.Serialize(this, Opcoes);

    public static DadosFormulario DeJson(string json) =>
        JsonSerializer.Deserialize<DadosFormulario>(json, Opcoes) ?? throw new InvalidOperationException("Os dados do cálculo salvo estão vazios.");
}
