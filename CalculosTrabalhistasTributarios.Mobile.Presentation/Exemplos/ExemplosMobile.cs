using System.Text.Json;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Exemplos;

/// <summary>
/// Exemplos do botão "Preencher exemplo", com os dados fictícios do manual, em Exemplos/Calculadora.{Tipo}.json.
/// O arquivo traz só o que difere do formulário vazio: texto preenche o valor, número escolhe a opção e verdadeiro ou
/// falso marca o campo. Os demais campos voltam ao valor inicial, para que nada digitado antes se misture ao exemplo.
/// </summary>
public static class ExemplosMobile
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNameCaseInsensitive = true };

    public static bool Existe(TipoCalculadoraMobile tipo) => typeof(ExemplosMobile).Assembly.GetManifestResourceInfo(Recurso(tipo)) is not null;

    /// <summary>O formulário vazio (<paramref name="padrao"/>) com os valores do exemplo da calculadora.</summary>
    public static Result<DadosFormularioMobile> Montar(TipoCalculadoraMobile tipo, DadosFormularioMobile padrao)
    {
        using var stream = typeof(ExemplosMobile).Assembly.GetManifestResourceStream(Recurso(tipo));
        if (stream is null) return Erro.Validacao("Esta calculadora ainda não tem exemplo.");
        using var documento = JsonDocument.Parse(stream);
        var raiz = documento.RootElement;

        var campos = new Dictionary<string, ValorCampoMobile>(padrao.Campos);
        if (raiz.TryGetProperty("Campos", out var exemploCampos))
            foreach (var campo in exemploCampos.EnumerateObject())
            {
                if (!campos.TryGetValue(campo.Name, out var atual))
                    return Erro.Validacao($"Exemplo: o campo {campo.Name} não existe no formulário.");
                campos[campo.Name] = Aplicar(atual, campo.Value);
            }

        var grades = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>>(padrao.Grades);
        if (raiz.TryGetProperty("Grades", out var exemploGrades))
            foreach (var grade in exemploGrades.EnumerateObject())
                grades[grade.Name] = grade.Value.EnumerateArray()
                    .Select(linha => (IReadOnlyDictionary<string, ValorCampoMobile>)linha.EnumerateObject()
                        .ToDictionary(celula => celula.Name, celula => Aplicar(new(string.Empty, 0, false), celula.Value)))
                    .ToArray();

        return padrao with
        {
            Campos = campos,
            Grades = grades,
            DepositosFgts = Lista<DepositoFgtsSalvoMobile>(raiz, "DepositosFgts") ?? padrao.DepositosFgts,
            Beneficios = Lista<BeneficioSalvoMobile>(raiz, "Beneficios") ?? padrao.Beneficios,
            LancamentosBanco = Lista<LancamentoBancoSalvoMobile>(raiz, "LancamentosBanco") ?? padrao.LancamentosBanco,
            QuitacaoBancoHoras = null,
            QuitacaoConfirmada = false
        };
    }

    private static string Recurso(TipoCalculadoraMobile tipo) => $"Exemplos.Calculadora.{tipo}.json";

    // Na grade, a célula informada só altera o que o exemplo traz; as demais ficam com o valor inicial da coluna.
    private static ValorCampoMobile Aplicar(ValorCampoMobile atual, JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => atual with { Valor = valor.GetString()! },
        JsonValueKind.Number => atual with { Indice = valor.GetInt32() },
        JsonValueKind.True or JsonValueKind.False => atual with { Marcado = valor.GetBoolean() },
        _ => throw new JsonException($"Valor de exemplo sem tipo reconhecido: {valor}.")
    };

    private static T[]? Lista<T>(JsonElement raiz, string nome) =>
        raiz.TryGetProperty(nome, out var lista) ? lista.Deserialize<T[]>(Opcoes) : null;
}
