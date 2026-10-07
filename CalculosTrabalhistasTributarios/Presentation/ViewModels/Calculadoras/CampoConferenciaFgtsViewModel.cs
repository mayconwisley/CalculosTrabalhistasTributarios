using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Competências conferidas, com a remuneração e o depósito informado. Gerar meses cria as competências mensais do período
/// e preserva os valores já digitados; a competência rescisória é acrescentada à parte.
/// </summary>
public sealed class CampoConferenciaFgtsViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly RelayCommand _adicionarCommand;
    private readonly RelayCommand _adicionarRescisoriaCommand;
    private string _inicio = DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura);
    private string _fim = DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura);
    private string _remuneracaoMensal = "0,00";
    private string _depositoMensal = "0,00";
    private string _mensagem = string.Empty;
    private string? _periodoPendente;

    public CampoConferenciaFgtsViewModel() : base("Competências do FGTS", "Remuneração e depósito informado de cada competência.")
    {
        GerarCommand = new RelayCommand(_ => Gerar());
        _adicionarCommand = new RelayCommand(_ => Adicionar(TipoCompetenciaFgts.Mensal), _ => Linhas.Count < ConferenciaFgts.LimiteLinhas);
        _adicionarRescisoriaCommand = new RelayCommand(_ => Adicionar(TipoCompetenciaFgts.Rescisoria),
            _ => Linhas.Count < ConferenciaFgts.LimiteLinhas && Linhas.All(linha => (TipoCompetenciaFgts)linha.Tipo.Valor != TipoCompetenciaFgts.Rescisoria));
        RemoverCommand = new RelayCommand(item => { if (item is LinhaConferenciaFgtsViewModel linha) Linhas.Remove(linha); });
        Linhas.CollectionChanged += (_, _) =>
        {
            _adicionarCommand.RaiseCanExecuteChanged();
            _adicionarRescisoriaCommand.RaiseCanExecuteChanged();
        };
    }

    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public string RemuneracaoMensal { get => _remuneracaoMensal; set => SetProperty(ref _remuneracaoMensal, value); }
    public string DepositoMensal { get => _depositoMensal; set => SetProperty(ref _depositoMensal, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public ObservableCollection<LinhaConferenciaFgtsViewModel> Linhas { get; } = [];
    public ICommand GerarCommand { get; }
    public ICommand AdicionarCommand => _adicionarCommand;
    public ICommand AdicionarRescisoriaCommand => _adicionarRescisoriaCommand;
    public ICommand RemoverCommand { get; }

    public bool Gerar()
    {
        if (!TentarCompetencia(Inicio, out var inicio) || !TentarCompetencia(Fim, out var fim) || inicio > fim)
        {
            Mensagem = "Informe início e fim válidos, no formato MM/AAAA, em ordem cronológica.";
            return false;
        }
        if (!LeituraNumerica.TentarLer(RemuneracaoMensal, out var remuneracao) || remuneracao < 0m || !LeituraNumerica.TentarLer(DepositoMensal, out var deposito) || deposito < 0m)
        {
            Mensagem = "Informe remuneração e depósito por mês positivos ou zero, por exemplo 3.000,00.";
            return false;
        }
        var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
        var rescisorias = Linhas.Where(linha => (TipoCompetenciaFgts)linha.Tipo.Valor == TipoCompetenciaFgts.Rescisoria).ToArray();
        if (quantidade + rescisorias.Length > ConferenciaFgts.LimiteLinhas)
        {
            Mensagem = $"A conferência aceita até {ConferenciaFgts.LimiteLinhas} competências.";
            return false;
        }

        var mensais = Linhas.Where(linha => (TipoCompetenciaFgts)linha.Tipo.Valor == TipoCompetenciaFgts.Mensal)
            .GroupBy(linha => linha.Competencia.Trim()).ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var preservados = 0;
        var novas = Enumerable.Range(0, quantidade).Select(indice =>
        {
            var chave = inicio.AddMonths(indice).ToString("MM/yyyy", Cultura);
            if (mensais.TryGetValue(chave, out var existente))
            {
                preservados++;
                return existente;
            }
            return new LinhaConferenciaFgtsViewModel { Competencia = chave, Remuneracao = remuneracao.ToString("N2", Cultura), Deposito = deposito.ToString("N2", Cultura) };
        }).ToArray();
        var removidos = mensais.Count - preservados;
        var periodo = $"{inicio:yyyyMM}-{fim:yyyyMM}";
        if (removidos > 0 && _periodoPendente != periodo)
        {
            _periodoPendente = periodo;
            Mensagem = $"{removidos} competência(s) ficarão fora do período. Clique em Gerar meses novamente para confirmar; os valores das demais e a rescisória serão preservados.";
            return false;
        }
        _periodoPendente = null;
        Linhas.Clear();
        foreach (var linha in novas.Concat(rescisorias))
            Linhas.Add(linha);
        Mensagem = $"{quantidade} competência(s): {preservados} preservada(s), {quantidade - preservados} nova(s)" + (removidos > 0 ? $", {removidos} removida(s)" : "") + ".";
        return true;
    }

    public Result<IReadOnlyList<LancamentoFgts>> Ler()
    {
        if (Linhas.Count == 0)
            return Erro.Validacao("Gere os meses ou adicione as competências a conferir.");
        var lancamentos = new List<LancamentoFgts>(Linhas.Count);
        foreach (var linha in Linhas)
        {
            if (!TentarCompetencia(linha.Competencia, out var competencia))
                return Erro.Validacao($"Revise a competência '{linha.Competencia}': use MM/AAAA.");
            if (!LeituraNumerica.TentarLer(linha.Remuneracao, out var remuneracao) || !LeituraNumerica.TentarLer(linha.Deposito, out var deposito))
                return Erro.Validacao($"Revise a remuneração e o depósito de {linha}: use valores monetários, por exemplo 3.000,00.");
            lancamentos.Add(new(competencia, (TipoCompetenciaFgts)linha.Tipo.Valor, remuneracao, deposito));
        }
        return lancamentos;
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, RemuneracaoMensal, DepositoMensal,
        Linhas.Select(linha => new LinhaSalva(linha.Competencia, (TipoCompetenciaFgts)linha.Tipo.Valor, linha.Remuneracao, linha.Deposito)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.Inicio is null || dados.Fim is null || dados.RemuneracaoMensal is null || dados.DepositoMensal is null || dados.Linhas is null
                || dados.Linhas.Length > ConferenciaFgts.LimiteLinhas
                || dados.Linhas.Any(linha => linha?.Competencia is null || linha.Remuneracao is null || linha.Deposito is null || !Enum.IsDefined(linha.Tipo)))
                return false;
            Inicio = dados.Inicio;
            Fim = dados.Fim;
            RemuneracaoMensal = dados.RemuneracaoMensal;
            DepositoMensal = dados.DepositoMensal;
            Linhas.Clear();
            foreach (var linha in dados.Linhas)
                Linhas.Add(new() { Competencia = linha.Competencia, Tipo = LinhaConferenciaFgtsViewModel.Tipos.First(opcao => (TipoCompetenciaFgts)opcao.Valor == linha.Tipo), Remuneracao = linha.Remuneracao, Deposito = linha.Deposito });
            Mensagem = string.Empty;
            _periodoPendente = null;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private void Adicionar(TipoCompetenciaFgts tipo)
    {
        if (Linhas.Count >= ConferenciaFgts.LimiteLinhas)
            return;
        Linhas.Add(new() { Tipo = LinhaConferenciaFgtsViewModel.Tipos.First(opcao => (TipoCompetenciaFgts)opcao.Valor == tipo) });
        Mensagem = tipo == TipoCompetenciaFgts.Rescisoria
            ? "Na rescisória, informe a competência do desligamento e as verbas rescisórias que integram o FGTS, inclusive o aviso prévio indenizado."
            : string.Empty;
    }

    private static bool TentarCompetencia(string texto, out DateOnly competencia)
    {
        var lido = DateTime.TryParseExact(texto.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data);
        competencia = lido ? new DateOnly(data.Year, data.Month, 1) : default;
        return lido;
    }

    private sealed record LinhaSalva(string Competencia, TipoCompetenciaFgts Tipo, string Remuneracao, string Deposito);
    private sealed record DadosSalvos(string Inicio, string Fim, string RemuneracaoMensal, string DepositoMensal, LinhaSalva[] Linhas);
}
