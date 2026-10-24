using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Salários de contribuição desde 07/1994 por período, como no extrato do CNIS: cada linha junta as competências seguidas
/// com o mesmo salário. + Período começa a nova linha no mês seguinte ao fim da anterior.
/// </summary>
public sealed class CampoSalariosContribuicaoViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly RelayCommand _adicionarCommand;

    /// <param name="referencia">Até quando os salários são corrigidos, como "a data da simulação" ou "o óbito".</param>
    public CampoSalariosContribuicaoViewModel(string referencia = "a data da simulação")
        : base("Salários de contribuição", "Períodos com o mesmo salário de contribuição, desde 07/1994, como no CNIS.")
    {
        Explicacao = "Informe os salários de contribuição desde 07/1994, como no extrato do CNIS: uma linha para cada sequência de meses com o mesmo salário, ou um mês por linha, repetindo a competência em De e Até. Some os vínculos do mesmo mês numa só linha. "
            + $"Cada mês é corrigido pelo INPC até {referencia}, e a média é a soma dos salários corrigidos dividida pela quantidade de meses.";
        _adicionarCommand = new RelayCommand(_ => Adicionar(), _ => Linhas.Count < CalculadoraMediaSalarios.LimitePeriodos);
        RemoverCommand = new RelayCommand(item => { if (item is LinhaSalarioContribuicaoViewModel linha) Linhas.Remove(linha); });
        Linhas.CollectionChanged += (_, _) => _adicionarCommand.RaiseCanExecuteChanged();
    }

    public string Explicacao { get; }
    public ObservableCollection<LinhaSalarioContribuicaoViewModel> Linhas { get; } = [];
    public ICommand AdicionarCommand => _adicionarCommand;
    public ICommand RemoverCommand { get; }

    public Result<IReadOnlyList<PeriodoSalarioContribuicao>> Ler()
    {
        if (Linhas.Count == 0)
            return Erro.Validacao("Adicione os períodos de salário de contribuição desde 07/1994, ou escolha informar a média pronta.");
        var periodos = new List<PeriodoSalarioContribuicao>(Linhas.Count);
        foreach (var linha in Linhas)
        {
            if (!TentarCompetencia(linha.Inicio, out var inicio) || !TentarCompetencia(linha.Fim, out var fim))
                return Erro.Validacao($"Revise o período '{linha}': informe o início e o fim no formato mm/aaaa; para um só mês, repita a competência.");
            if (!LeituraNumerica.TentarLer(linha.Salario, out var salario))
                return Erro.Validacao($"Revise o salário do período {linha}: use um valor monetário, por exemplo 3.500,00.");
            periodos.Add(new PeriodoSalarioContribuicao(inicio, fim, salario));
        }
        return periodos;
    }

    public string Exportar() => JsonSerializer.Serialize(Linhas.Select(linha => new LinhaSalva(linha.Inicio, linha.Fim, linha.Salario)).ToArray());

    public bool Importar(string? json)
    {
        try
        {
            var linhas = JsonSerializer.Deserialize<LinhaSalva[]>(json ?? "");
            if (linhas is null || linhas.Length > CalculadoraMediaSalarios.LimitePeriodos
                || linhas.Any(linha => linha?.Inicio is null || linha.Fim is null || linha.Salario is null))
                return false;
            Linhas.Clear();
            foreach (var linha in linhas)
                Linhas.Add(new() { Inicio = linha.Inicio, Fim = linha.Fim, Salario = linha.Salario });
            return true;
        }
        catch (JsonException) { return false; }
    }

    private void Adicionar()
    {
        if (Linhas.Count >= CalculadoraMediaSalarios.LimitePeriodos)
            return;
        var inicio = Linhas.Count == 0 ? CalculadoraMediaSalarios.InicioDaMedia.ToString("MM/yyyy", Cultura)
            : TentarCompetencia(Linhas[^1].Fim, out var fim) ? fim.AddMonths(1).ToString("MM/yyyy", Cultura) : string.Empty;
        Linhas.Add(new() { Inicio = inicio });
    }

    private static bool TentarCompetencia(string texto, out DateOnly competencia) =>
        DateOnly.TryParseExact("01/" + texto.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out competencia);

    private sealed record LinhaSalva(string Inicio, string Fim, string Salario);
}
