using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoMediaVerbasVariaveisViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private string _inicio = DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura);
    private string _fim = DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura);
    private string _divisor = "12";
    private string _mensagem = string.Empty;

    public CampoMediaVerbasVariaveisViewModel() : base("Valores por competência",
        "Informe somente parcelas variáveis salariais efetivamente recebidas no período escolhido.")
    {
        GerarCommand = new RelayCommand(_ => Gerar());
    }

    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public string Divisor { get => _divisor; set => SetProperty(ref _divisor, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public ObservableCollection<MediaVerbasMesViewModel> Meses { get; } = [];
    public ICommand GerarCommand { get; }

    public bool Gerar()
    {
        if (!TentarCompetencia(Inicio, out var inicio) || !TentarCompetencia(Fim, out var fim) || inicio > fim)
        {
            Mensagem = "Informe início e fim válidos em MM/AAAA, em ordem cronológica.";
            return false;
        }
        var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
        if (quantidade > 24)
        {
            Mensagem = "O período pode conter até 24 meses.";
            return false;
        }
        Meses.Clear();
        for (var indice = 0; indice < quantidade; indice++)
        {
            var data = inicio.AddMonths(indice);
            Meses.Add(new() { Competencia = data.ToString("MM/yyyy", Cultura) });
        }
        Divisor = quantidade.ToString(Cultura);
        Mensagem = $"{quantidade} mês(es) gerado(s). Meses sem verba ficam com zero e contam no divisor informado.";
        return true;
    }

    public Result<(IReadOnlyList<VerbasVariaveisDoMes> Meses, int Divisor)> Ler()
    {
        if (Meses.Count == 0)
            return Erro.Validacao("Gere as competências antes de calcular a média.");
        if (!int.TryParse(Divisor, NumberStyles.Integer, Cultura, out var divisor))
            return Erro.Validacao("Informe um divisor inteiro entre 1 e 24.");
        var meses = new List<VerbasVariaveisDoMes>(Meses.Count);
        foreach (var linha in Meses)
        {
            if (!TentarCompetencia(linha.Competencia, out var competencia)
                || !Moeda(linha.Comissoes, out var comissoes) || !Moeda(linha.Dsr, out var dsr)
                || !Moeda(linha.HorasExtras, out var extras) || !Moeda(linha.Adicionais, out var adicionais)
                || !Moeda(linha.Outras, out var outras))
                return Erro.Validacao($"Revise competência e valores de {linha.Competencia}: use MM/AAAA e reais com centavos.");
            meses.Add(new(competencia, comissoes, dsr, extras, adicionais, outras));
        }
        return (meses, divisor);
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, Divisor,
        Meses.Select(linha => new LinhaSalva(linha.Competencia, linha.Comissoes, linha.Dsr,
            linha.HorasExtras, linha.Adicionais, linha.Outras)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados is null || dados.Inicio is null || dados.Fim is null || dados.Divisor is null
                || dados.Meses is null || dados.Meses.Length > 24 || dados.Meses.Any(linha => linha is null
                    || linha.Competencia is null || linha.Comissoes is null || linha.Dsr is null
                    || linha.HorasExtras is null || linha.Adicionais is null || linha.Outras is null))
                return false;
            Inicio = dados.Inicio;
            Fim = dados.Fim;
            Divisor = dados.Divisor;
            Meses.Clear();
            foreach (var linha in dados.Meses)
                Meses.Add(new()
                {
                    Competencia = linha.Competencia, Comissoes = linha.Comissoes, Dsr = linha.Dsr,
                    HorasExtras = linha.HorasExtras, Adicionais = linha.Adicionais, Outras = linha.Outras
                });
            Mensagem = string.Empty;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool Moeda(string? texto, out decimal valor) =>
        LeituraNumerica.TentarLer(texto ?? "", out valor) && valor >= 0m && decimal.Round(valor, 2) == valor;

    private static bool TentarCompetencia(string? texto, out DateOnly competencia)
    {
        var valido = DateTime.TryParseExact(texto?.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data);
        competencia = valido ? new(data.Year, data.Month, 1) : default;
        return valido;
    }

    private sealed record LinhaSalva(string Competencia, string Comissoes, string Dsr, string HorasExtras,
        string Adicionais, string Outras);
    private sealed record DadosSalvos(string Inicio, string Fim, string Divisor, LinhaSalva[] Meses);
}
