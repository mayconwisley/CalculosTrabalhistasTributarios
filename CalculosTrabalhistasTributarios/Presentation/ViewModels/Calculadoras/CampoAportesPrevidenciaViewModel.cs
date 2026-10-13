using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using RegraResgate = CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar.CalculadoraResgatePrevidencia;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Aportes do plano de previdência, com o saldo atual de cada um. Gerar aportes cria aportes mensais a partir do primeiro,
/// com o saldo estimado por uma rentabilidade mensal até a data do resgate; se já houver aportes, o primeiro clique avisa
/// que eles serão substituídos, e o segundo confirma.
/// </summary>
public sealed class CampoAportesPrevidenciaViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly CampoTextoViewModel _dataResgate;
    private readonly RelayCommand _adicionarCommand;
    private string _primeiroAporte = DateTime.Today.AddYears(-3).ToString("dd/MM/yyyy", Cultura);
    private string _quantidade = "12";
    private string _valorAporte = "0,00";
    private string _rentabilidade = "0,00";
    private string _mensagem = string.Empty;
    private bool _confirmarSubstituicao;

    public CampoAportesPrevidenciaViewModel(CampoTextoViewModel dataResgate)
        : base("Aportes do plano", "Data, valor aportado e saldo atual de cada aporte, conforme o extrato.")
    {
        _dataResgate = dataResgate;
        GerarCommand = new RelayCommand(_ => Gerar());
        _adicionarCommand = new RelayCommand(_ => Adicionar(), _ => Linhas.Count < RegraResgate.LimiteAportes);
        RemoverCommand = new RelayCommand(item => { if (item is LinhaAportePrevidenciaViewModel linha) Linhas.Remove(linha); });
        Linhas.CollectionChanged += (_, _) =>
        {
            _adicionarCommand.RaiseCanExecuteChanged();
            _confirmarSubstituicao = false;
        };
    }

    public string PrimeiroAporte { get => _primeiroAporte; set => SetProperty(ref _primeiroAporte, value); }
    public string Quantidade { get => _quantidade; set => SetProperty(ref _quantidade, value); }
    public string ValorAporte { get => _valorAporte; set => SetProperty(ref _valorAporte, value); }
    public string Rentabilidade { get => _rentabilidade; set => SetProperty(ref _rentabilidade, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public ObservableCollection<LinhaAportePrevidenciaViewModel> Linhas { get; } = [];
    public ICommand GerarCommand { get; }
    public ICommand AdicionarCommand => _adicionarCommand;
    public ICommand RemoverCommand { get; }

    public bool Gerar()
    {
        if (!TentarData(PrimeiroAporte, out var primeiro) || !TentarData(_dataResgate.Valor, out var resgate))
        {
            Mensagem = "Informe a data do primeiro aporte e a data do resgate no formato dd/mm/aaaa.";
            return false;
        }
        if (!int.TryParse(Quantidade.Trim(), NumberStyles.Integer, Cultura, out var quantidade) || quantidade is < 1 or > RegraResgate.LimiteAportes)
        {
            Mensagem = $"Informe de 1 a {RegraResgate.LimiteAportes} aportes mensais.";
            return false;
        }
        if (!LeituraNumerica.TentarLer(ValorAporte, out var valor) || valor <= 0m || !LeituraNumerica.TentarLer(Rentabilidade, out var rentabilidade) || rentabilidade is < -10m or > 10m)
        {
            Mensagem = "Informe o valor por aporte, como 500,00, e a rentabilidade ao mês entre -10% e 10%, como 0,60.";
            return false;
        }
        if (primeiro.AddMonths(quantidade - 1) > resgate)
        {
            Mensagem = "Os aportes gerados passariam da data do resgate: reduza a quantidade ou antecipe o primeiro aporte.";
            return false;
        }
        if (Linhas.Count > 0 && !_confirmarSubstituicao)
        {
            Mensagem = $"Os {Linhas.Count} aportes da grade serão substituídos. Clique em Gerar aportes novamente para confirmar.";
            _confirmarSubstituicao = true;
            return false;
        }

        var aportado = decimal.Round(valor, 2);
        var novas = Enumerable.Range(0, quantidade).Select(indice =>
        {
            var data = primeiro.AddMonths(indice);
            return new LinhaAportePrevidenciaViewModel
            {
                Data = data.ToString("dd/MM/yyyy", Cultura),
                Aportado = aportado.ToString("N2", Cultura),
                Saldo = RegraResgate.EstimarSaldo(aportado, data, resgate, rentabilidade).ToString("N2", Cultura)
            };
        }).ToArray();
        Linhas.Clear();
        foreach (var linha in novas)
            Linhas.Add(linha);
        Mensagem = $"{quantidade} aporte(s) de {novas[0].Data} a {novas[^1].Data}, com saldo estimado a {rentabilidade.ToString("0.##", Cultura)}% ao mês. Substitua pelos saldos do extrato, se os tiver.";
        return true;
    }

    public Result<IReadOnlyList<AportePrevidencia>> Ler()
    {
        if (Linhas.Count == 0)
            return Erro.Validacao("Gere os aportes ou adicione os aportes do plano.");
        var aportes = new List<AportePrevidencia>(Linhas.Count);
        foreach (var linha in Linhas)
        {
            if (!TentarData(linha.Data, out var data))
                return Erro.Validacao($"Revise a data do aporte '{linha.Data}': use dd/mm/aaaa.");
            if (!LeituraNumerica.TentarLer(linha.Aportado, out var aportado) || !LeituraNumerica.TentarLer(linha.Saldo, out var saldo))
                return Erro.Validacao($"Revise o valor aportado e o saldo do aporte de {linha}: use valores monetários, por exemplo 500,00.");
            aportes.Add(new AportePrevidencia(data, aportado, saldo));
        }
        return aportes;
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(PrimeiroAporte, Quantidade, ValorAporte, Rentabilidade,
        Linhas.Select(linha => new LinhaSalva(linha.Data, linha.Aportado, linha.Saldo)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.PrimeiroAporte is null || dados.Quantidade is null || dados.ValorAporte is null || dados.Rentabilidade is null || dados.Linhas is null
                || dados.Linhas.Length > RegraResgate.LimiteAportes
                || dados.Linhas.Any(linha => linha?.Data is null || linha.Aportado is null || linha.Saldo is null))
                return false;
            PrimeiroAporte = dados.PrimeiroAporte;
            Quantidade = dados.Quantidade;
            ValorAporte = dados.ValorAporte;
            Rentabilidade = dados.Rentabilidade;
            Linhas.Clear();
            foreach (var linha in dados.Linhas)
                Linhas.Add(new() { Data = linha.Data, Aportado = linha.Aportado, Saldo = linha.Saldo });
            Mensagem = string.Empty;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private void Adicionar()
    {
        if (Linhas.Count >= RegraResgate.LimiteAportes)
            return;
        var valor = LeituraNumerica.TentarLer(ValorAporte, out var aporte) && aporte > 0m ? aporte.ToString("N2", Cultura) : "0,00";
        Linhas.Add(new() { Aportado = valor, Saldo = valor });
        Mensagem = string.Empty;
    }

    private static bool TentarData(string texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out data);

    private sealed record LinhaSalva(string Data, string Aportado, string Saldo);
    private sealed record DadosSalvos(string PrimeiroAporte, string Quantidade, string ValorAporte, string Rentabilidade, LinhaSalva[] Linhas);
}
