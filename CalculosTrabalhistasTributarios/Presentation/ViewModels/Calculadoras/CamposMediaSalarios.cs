using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Origem da média dos salários de contribuição na aposentadoria e na pensão por morte: calculada pela grade de salários ou
/// informada pronta. Cuida da visibilidade, do histórico e da leitura dos três campos.
/// </summary>
public sealed class CamposMediaSalarios
{
    /// <param name="referencia">Até quando os salários são corrigidos, como "a data da simulação" ou "o óbito".</param>
    public CamposMediaSalarios(string referencia, string dicaOrigem)
    {
        Salarios = new(referencia);
        Origem = new("Média dos salários", [new("Calcular pelos salários", true), new("Informar a média pronta", false)], dicaOrigem) { Largura = 250 };
        Media = new("Média informada", TipoCampo.Moeda, "0,00", "Média de todos os salários de contribuição desde 07/1994, corrigidos, como no simulador do Meu INSS.");
    }

    public CampoOpcaoViewModel Origem { get; }
    public CampoTextoViewModel Media { get; }
    public CampoSalariosContribuicaoViewModel Salarios { get; }
    public IReadOnlyList<CampoViewModel> Campos => [Origem, Media, Salarios];
    public bool Calcular => Origem.Valor<bool>();

    /// <param name="aplicavel">Falso quando a média não é usada, como na pensão do segurado já aposentado.</param>
    public void AtualizarVisibilidade(bool aplicavel = true)
    {
        Origem.Visivel = aplicavel;
        Salarios.Visivel = aplicavel && Calcular;
        Media.Visivel = aplicavel && !Calcular;
    }

    public void Exportar(Dictionary<string, string> campos) => campos[Salarios.Rotulo] = Salarios.Exportar();

    /// <summary>
    /// O histórico anterior guardava a média digitada em "Média dos salários", rótulo que hoje é a escolha da origem: o valor
    /// antigo passa para a média informada. Devolve os valores atualizados para a importação dos campos comuns.
    /// </summary>
    public Dictionary<string, string> Migrar(IReadOnlyDictionary<string, string> valores)
    {
        var atualizados = new Dictionary<string, string>(valores);
        if (!atualizados.ContainsKey(Media.Rotulo) && atualizados.TryGetValue(Origem.Rotulo, out var antigo) && Origem.Opcoes.All(opcao => opcao.Texto != antigo))
        {
            atualizados[Media.Rotulo] = antigo;
            atualizados[Origem.Rotulo] = Origem.Opcoes[1].Texto;
        }
        return atualizados;
    }

    public void ImportarSalarios(IReadOnlyDictionary<string, string> valores)
    {
        if (!valores.TryGetValue(Salarios.Rotulo, out var json) || !Salarios.Importar(json))
            Salarios.Linhas.Clear();
    }

    /// <summary>Os salários da grade quando a média é calculada; nulo quando ela é informada pronta.</summary>
    public Result<IReadOnlyList<PeriodoSalarioContribuicao>?> LerSalarios()
    {
        if (!Calcular)
            return Result<IReadOnlyList<PeriodoSalarioContribuicao>?>.Ok(null);
        var lidos = Salarios.Ler();
        return lidos.Falhou ? lidos.Erro : Result<IReadOnlyList<PeriodoSalarioContribuicao>?>.Ok(lidos.Valor);
    }
}
