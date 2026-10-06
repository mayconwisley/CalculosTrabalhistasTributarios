using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoBancoHorasViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    public static IReadOnlyList<OpcaoCampo> Regimes { get; } =
    [
        new("Mesmo mês", RegimeBancoHoras.MesmoMes),
        new("Acordo individual escrito · até 6 meses", RegimeBancoHoras.AcordoIndividualEscrito),
        new("Acordo ou convenção coletiva · até 1 ano", RegimeBancoHoras.AcordoColetivo)
    ];
    public static IReadOnlyList<OpcaoCampo> Situacoes { get; } =
    [
        new("Acompanhamento", SituacaoBancoHoras.Acompanhamento),
        new("Fechamento do ciclo", SituacaoBancoHoras.Fechamento),
        new("Rescisão", SituacaoBancoHoras.Rescisao)
    ];

    private string _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("dd/MM/yyyy", Cultura);
    private string _fim = DateTime.Today.ToString("dd/MM/yyyy", Cultura);
    private OpcaoCampo _regime = Regimes[0];
    private OpcaoCampo _situacao = Situacoes[0];
    private string _salario = "0,00";
    private string _divisor = "220";
    private string _adicional = "50,00";
    private string _mensagem = string.Empty;

    public CampoBancoHorasViewModel() : base("Ciclo e lançamentos", "Registre créditos e folgas do mesmo ciclo de compensação.")
    {
        Lancamentos.Add(new());
        AdicionarCommand = new RelayCommand(_ => Adicionar());
        RemoverCommand = new RelayCommand(item => { if (item is LancamentoBancoHorasViewModel linha) Lancamentos.Remove(linha); });
    }

    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public OpcaoCampo Regime { get => _regime; set { if (value is not null) SetProperty(ref _regime, value); } }
    public OpcaoCampo Situacao { get => _situacao; set { if (value is not null) SetProperty(ref _situacao, value); } }
    public string Salario { get => _salario; set => SetProperty(ref _salario, value); }
    public string Divisor { get => _divisor; set => SetProperty(ref _divisor, value); }
    public string Adicional { get => _adicional; set => SetProperty(ref _adicional, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public IReadOnlyList<OpcaoCampo> OpcoesRegime => Regimes;
    public IReadOnlyList<OpcaoCampo> OpcoesSituacao => Situacoes;
    public ObservableCollection<LancamentoBancoHorasViewModel> Lancamentos { get; } = [];
    public ICommand AdicionarCommand { get; }
    public ICommand RemoverCommand { get; }

    public Result<(DateOnly Inicio, DateOnly Fim, RegimeBancoHoras Regime, SituacaoBancoHoras Situacao,
        decimal Salario, decimal Divisor, decimal Adicional, IReadOnlyList<LancamentoBancoHoras> Lancamentos)> Ler()
    {
        if (!DateOnly.TryParseExact(Inicio, "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var inicio)
            || !DateOnly.TryParseExact(Fim, "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var fim))
            return Erro.Validacao("Informe início e fim do ciclo no formato dd/mm/aaaa.");
        if (!LeituraNumerica.TentarLer(Salario, out var salario) || !LeituraNumerica.TentarLer(Divisor, out var divisor)
            || !LeituraNumerica.TentarLer(Adicional, out var adicional))
            return Erro.Validacao("Informe salário, divisor e adicional em formato numérico válido.");
        if (Regime.Valor is not RegimeBancoHoras regime || Situacao.Valor is not SituacaoBancoHoras situacao)
            return Erro.Validacao("Selecione regime e situação válidos para o banco de horas.");
        var lancamentos = new List<LancamentoBancoHoras>(Lancamentos.Count);
        foreach (var linha in Lancamentos)
        {
            if (!DateOnly.TryParseExact(linha.Data, "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data)
                || linha.Tipo.Valor is not TipoLancamentoBancoHoras tipo || !TentarMinutos(linha.Horas, out var minutos))
                return Erro.Validacao($"Revise data, tipo e horas do lançamento de {linha.Data}. Use horas como 1:30.");
            lancamentos.Add(new(data, tipo, minutos, linha.Descricao?.Trim() ?? string.Empty));
        }
        return (inicio, fim, regime, situacao, salario, divisor, adicional, lancamentos);
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim,
        (RegimeBancoHoras)Regime.Valor, (SituacaoBancoHoras)Situacao.Valor, Salario, Divisor, Adicional,
        Lancamentos.Select(linha => new LinhaSalva(linha.Data, (TipoLancamentoBancoHoras)linha.Tipo.Valor,
            linha.Horas, linha.Descricao)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados is null || dados.Inicio is null || dados.Fim is null || dados.Salario is null
                || dados.Divisor is null || dados.Adicional is null || dados.Lancamentos is null
                || dados.Lancamentos.Length > 500 || !Enum.IsDefined(dados.Regime) || !Enum.IsDefined(dados.Situacao)
                || dados.Lancamentos.Any(linha => linha is null || linha.Data is null || linha.Horas is null
                    || linha.Descricao is null || !Enum.IsDefined(linha.Tipo)))
                return false;
            Inicio = dados.Inicio;
            Fim = dados.Fim;
            Regime = Regimes.Single(item => (RegimeBancoHoras)item.Valor == dados.Regime);
            Situacao = Situacoes.Single(item => (SituacaoBancoHoras)item.Valor == dados.Situacao);
            Salario = dados.Salario;
            Divisor = dados.Divisor;
            Adicional = dados.Adicional;
            Lancamentos.Clear();
            foreach (var linha in dados.Lancamentos)
                Lancamentos.Add(new()
                {
                    Data = linha.Data, Tipo = LancamentoBancoHorasViewModel.Tipos.Single(item => (TipoLancamentoBancoHoras)item.Valor == linha.Tipo),
                    Horas = linha.Horas, Descricao = linha.Descricao
                });
            Mensagem = string.Empty;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private void Adicionar()
    {
        if (Lancamentos.Count >= 500)
        {
            Mensagem = "O ciclo aceita até 500 lançamentos.";
            return;
        }
        Lancamentos.Add(new() { Data = Fim });
        Mensagem = string.Empty;
    }

    private static bool TentarMinutos(string? texto, out int minutos)
    {
        minutos = 0;
        var partes = texto?.Trim().Split(':');
        if (partes is not { Length: 2 } || !int.TryParse(partes[0], NumberStyles.None, Cultura, out var horas)
            || !int.TryParse(partes[1], NumberStyles.None, Cultura, out var resto) || horas is < 0 or > 10 || resto is < 0 or > 59)
            return false;
        minutos = horas * 60 + resto;
        return true;
    }

    private sealed record LinhaSalva(string Data, TipoLancamentoBancoHoras Tipo, string Horas, string Descricao);
    private sealed record DadosSalvos(string Inicio, string Fim, RegimeBancoHoras Regime, SituacaoBancoHoras Situacao,
        string Salario, string Divisor, string Adicional, LinhaSalva[] Lancamentos);
}
