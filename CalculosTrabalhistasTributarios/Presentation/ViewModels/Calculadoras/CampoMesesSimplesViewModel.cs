using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using RegraSimples = CalculosTrabalhistasTributarios.Domain.Tributacao.CalculadoraSimplesNacional;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Receita e folha das competências que o período de apuração exige. As competências são geradas pela mesma regra do
/// cálculo, conforme o período e o início de atividade; gerar de novo preserva os valores dos meses que continuam.
/// </summary>
public sealed class CampoMesesSimplesViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly CampoTextoViewModel _periodo;
    private readonly CampoTextoViewModel _inicio;
    private string _receitaMensal = "0,00";
    private string _folhaMensal = "0,00";
    private string _mensagem = string.Empty;

    public CampoMesesSimplesViewModel(CampoTextoViewModel periodo, CampoTextoViewModel inicio)
        : base("Receita e folha dos meses anteriores", "Competências que formam a receita (RBT12) e a folha (FS12) de 12 meses do período de apuração.")
    {
        _periodo = periodo;
        _inicio = inicio;
        GerarCommand = new RelayCommand(_ => Gerar());
    }

    public string ReceitaMensal { get => _receitaMensal; set => SetProperty(ref _receitaMensal, value); }
    public string FolhaMensal { get => _folhaMensal; set => SetProperty(ref _folhaMensal, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public ObservableCollection<LinhaMesSimplesViewModel> Meses { get; } = [];
    public ICommand GerarCommand { get; }

    public bool Gerar()
    {
        if (!TentarCompetencia(_periodo.Valor, out var periodo))
        {
            Mensagem = "Informe o período de apuração (MM/AAAA) antes de gerar os meses.";
            return false;
        }
        DateOnly? inicio = null;
        if (!string.IsNullOrWhiteSpace(_inicio.Valor))
        {
            if (!TentarCompetencia(_inicio.Valor, out var mes) || mes > periodo)
            {
                Mensagem = "O início das atividades deve estar em MM/AAAA e não pode ser posterior ao período de apuração.";
                return false;
            }
            inicio = mes;
        }
        if (!LeituraNumerica.TentarLer(ReceitaMensal, out var receita) || receita < 0m || !LeituraNumerica.TentarLer(FolhaMensal, out var folha) || folha < 0m)
        {
            Mensagem = "Informe receita e folha por mês positivas ou zero, por exemplo 20.000,00.";
            return false;
        }

        var janela = RegraSimples.Janela(periodo, inicio);
        var existentes = Meses.GroupBy(linha => linha.Competencia.Trim()).ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var preservados = 0;
        var novas = janela.Meses.Select(mes =>
        {
            var chave = mes.ToString("MM/yyyy", Cultura);
            if (existentes.TryGetValue(chave, out var linha))
            {
                preservados++;
                return linha;
            }
            return new LinhaMesSimplesViewModel { Competencia = chave, Receita = receita.ToString("N2", Cultura), Folha = folha.ToString("N2", Cultura) };
        }).ToArray();
        Meses.Clear();
        foreach (var linha in novas) Meses.Add(linha);
        Mensagem = janela.Meses.Count == 0
            ? "Neste mês de atividade não há meses anteriores a informar: o cálculo usa a receita e a folha do próprio mês ou a 1ª faixa."
            : $"{janela.Meses.Count} competência(s), de {janela.Meses[0]:MM/yyyy} a {janela.Meses[^1]:MM/yyyy}: {preservados} preservada(s)."
              + (janela.Regra2027 ? " Desde 2027, o mês anterior ao período não entra." : "");
        return true;
    }

    public Result<IReadOnlyList<MesSimples>> Ler()
    {
        var meses = new List<MesSimples>(Meses.Count);
        foreach (var linha in Meses)
        {
            if (!TentarCompetencia(linha.Competencia, out var competencia))
                return Erro.Validacao($"Revise a competência '{linha.Competencia}': use MM/AAAA.");
            if (!LeituraNumerica.TentarLer(linha.Receita, out var receita) || !LeituraNumerica.TentarLer(linha.Folha, out var folha))
                return Erro.Validacao($"Revise a receita e a folha de {linha.Competencia}: use valores monetários, por exemplo 20.000,00.");
            meses.Add(new(competencia, receita, folha));
        }
        return meses;
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(ReceitaMensal, FolhaMensal,
        Meses.Select(linha => new LinhaSalva(linha.Competencia, linha.Receita, linha.Folha)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.ReceitaMensal is null || dados.FolhaMensal is null || dados.Meses is null || dados.Meses.Length > 24
                || dados.Meses.Any(linha => linha?.Competencia is null || linha.Receita is null || linha.Folha is null))
                return false;
            ReceitaMensal = dados.ReceitaMensal;
            FolhaMensal = dados.FolhaMensal;
            Meses.Clear();
            foreach (var linha in dados.Meses)
                Meses.Add(new() { Competencia = linha.Competencia, Receita = linha.Receita, Folha = linha.Folha });
            Mensagem = string.Empty;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool TentarCompetencia(string texto, out DateOnly competencia)
    {
        var lido = DateTime.TryParseExact(texto.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data);
        competencia = lido ? new DateOnly(data.Year, data.Month, 1) : default;
        return lido;
    }

    private sealed record LinhaSalva(string Competencia, string Receita, string Folha);
    private sealed record DadosSalvos(string ReceitaMensal, string FolhaMensal, LinhaSalva[] Meses);
}
