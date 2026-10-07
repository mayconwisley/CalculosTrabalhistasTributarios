using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoDepositosFgtsViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly CampoTextoViewModel _admissao;
    private readonly CampoTextoViewModel _desligamento;
    private readonly RelayCommand _adicionarCommand;
    private string _mensagem = string.Empty;

    public CampoDepositosFgtsViewModel(CampoTextoViewModel admissao, CampoTextoViewModel desligamento)
        : base("Depósitos históricos do FGTS", "Valores mensais devidos antes da competência de desligamento; o mês da rescisão é calculado separadamente.")
    {
        _admissao = admissao;
        _desligamento = desligamento;
        GerarCommand = new RelayCommand(_ => GerarMeses());
        _adicionarCommand = new RelayCommand(_ => Adicionar(), _ => Depositos.Count < 720);
        Depositos.CollectionChanged += (_, _) => _adicionarCommand.RaiseCanExecuteChanged();
        RemoverCommand = new RelayCommand(item => { if (item is LinhaDepositoFgtsViewModel linha) Depositos.Remove(linha); });
    }

    public ObservableCollection<LinhaDepositoFgtsViewModel> Depositos { get; } = [];
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public ICommand GerarCommand { get; }
    public ICommand AdicionarCommand => _adicionarCommand;
    public ICommand RemoverCommand { get; }

    public Result<IReadOnlyList<DepositoFgtsHistorico>> Ler()
    {
        if (Depositos.Count > 720)
            return Erro.Validacao("O histórico do FGTS aceita até 720 competências.");
        var valores = new List<DepositoFgtsHistorico>(Depositos.Count);
        foreach (var linha in Depositos)
        {
            if (!DateTime.TryParseExact(linha.Competencia.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data))
                return Erro.Validacao($"Revise a competência '{linha.Competencia}' do FGTS: use MM/AAAA.");
            if (!LeituraNumerica.TentarLer(linha.Valor, out var valor))
                return Erro.Validacao($"Revise o depósito do FGTS de {linha.Competencia}: use valor monetário, por exemplo 250,00.");
            valores.Add(new(new DateOnly(data.Year, data.Month, 1), valor));
        }
        return valores;
    }

    public string Exportar() => JsonSerializer.Serialize(Depositos.Select(linha => new LinhaSalva(linha.Competencia, linha.Valor)).ToArray());

    public bool Importar(string? json)
    {
        try
        {
            var linhas = JsonSerializer.Deserialize<LinhaSalva[]>(json ?? "");
            if (linhas is null || linhas.Length > 720 || linhas.Any(linha => linha is null || linha.Competencia is null || linha.Valor is null))
                return false;
            Depositos.Clear();
            foreach (var linha in linhas)
                Depositos.Add(new() { Competencia = linha.Competencia, Valor = linha.Valor });
            Mensagem = string.Empty;
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Depósitos vindos da conferência do FGTS, no formato salvo no histórico, para abrir a rescisão já preenchida.</summary>
    public static string CriarImportacao(IReadOnlyList<DepositoFgtsHistorico> depositos) => JsonSerializer.Serialize(depositos
        .Select(item => new LinhaSalva(item.Competencia.ToString("MM/yyyy", Cultura), item.Valor.ToString("N2", Cultura))).ToArray());

    private void GerarMeses()
    {
        if (!DateOnly.TryParseExact(_admissao.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var admissao)
            || !DateOnly.TryParseExact(_desligamento.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var desligamento)
            || desligamento < admissao)
        {
            Mensagem = "Informe datas válidas de admissão e desligamento antes de gerar as competências.";
            return;
        }
        var primeiro = new DateOnly(admissao.Year, admissao.Month, 1);
        var quantidade = (desligamento.Year - admissao.Year) * 12 + desligamento.Month - admissao.Month;
        if (quantidade > 720)
        {
            Mensagem = "O histórico aceita até 720 competências. Informe o saldo do extrato para contratos mais longos.";
            return;
        }
        var existentes = Depositos.GroupBy(linha => linha.Competencia).ToDictionary(grupo => grupo.Key, grupo => grupo.First().Valor);
        Depositos.Clear();
        for (var i = 0; i < quantidade; i++)
        {
            var competencia = primeiro.AddMonths(i).ToString("MM/yyyy", Cultura);
            Depositos.Add(new() { Competencia = competencia, Valor = existentes.GetValueOrDefault(competencia) ?? "0,00" });
        }
        Mensagem = $"{quantidade} competência(s) gerada(s); valores já digitados para esses meses foram preservados.";
    }

    private void Adicionar()
    {
        if (Depositos.Count >= 720)
        {
            Mensagem = "O histórico aceita até 720 competências.";
            return;
        }
        Depositos.Add(new());
        Mensagem = string.Empty;
    }

    private sealed record LinhaSalva(string Competencia, string Valor);
}
