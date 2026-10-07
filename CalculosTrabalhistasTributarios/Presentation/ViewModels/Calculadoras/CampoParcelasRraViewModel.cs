using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using RegraRra = CalculosTrabalhistasTributarios.Domain.Tributacao.CalculadoraRra;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Competências a que o pagamento acumulado se refere. O ano de cada linha separa os anos anteriores do ano do
/// pagamento, e as linhas mensais e de 13º definem a quantidade de meses da tabela acumulada.
/// </summary>
public sealed class CampoParcelasRraViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly RelayCommand _adicionarMensalCommand;
    private readonly RelayCommand _adicionarDecimoTerceiroCommand;
    private string _inicio = DateTime.Today.AddYears(-1).ToString("01/yyyy", Cultura);
    private string _fim = DateTime.Today.AddYears(-1).ToString("12/yyyy", Cultura);
    private string _valorMensal = "0,00";
    private string _mensagem = string.Empty;
    private string? _periodoPendente;

    public CampoParcelasRraViewModel() : base("Parcelas do RRA", "Uma linha por competência a que o pagamento se refere, com o valor tributável recebido.")
    {
        GerarCommand = new RelayCommand(_ => Gerar());
        _adicionarMensalCommand = new RelayCommand(_ => Adicionar(TipoParcelaRra.Mensal), _ => Parcelas.Count < RegraRra.LimiteParcelas);
        _adicionarDecimoTerceiroCommand = new RelayCommand(_ => Adicionar(TipoParcelaRra.DecimoTerceiro), _ => Parcelas.Count < RegraRra.LimiteParcelas);
        RemoverCommand = new RelayCommand(item => { if (item is LinhaParcelaRraViewModel linha) Parcelas.Remove(linha); });
        Parcelas.CollectionChanged += (_, _) =>
        {
            _adicionarMensalCommand.RaiseCanExecuteChanged();
            _adicionarDecimoTerceiroCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(Resumo));
        };
    }

    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public string ValorMensal { get => _valorMensal; set => SetProperty(ref _valorMensal, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public string Resumo => Parcelas.Count == 0 ? "Nenhuma parcela." : $"{Parcelas.Count} linha(s) de {RegraRra.LimiteParcelas}.";
    public ObservableCollection<LinhaParcelaRraViewModel> Parcelas { get; } = [];
    public ICommand GerarCommand { get; }
    public ICommand AdicionarMensalCommand => _adicionarMensalCommand;
    public ICommand AdicionarDecimoTerceiroCommand => _adicionarDecimoTerceiroCommand;
    public ICommand RemoverCommand { get; }

    /// <summary>
    /// Gera as competências mensais do período com o valor mensal informado. Linhas mensais já existentes no período
    /// mantêm o valor, e as de 13º são preservadas; linhas mensais fora do período exigem um segundo clique para sair.
    /// </summary>
    public bool Gerar()
    {
        if (!TentarCompetencia(Inicio, out var inicio) || !TentarCompetencia(Fim, out var fim) || inicio > fim)
        {
            Mensagem = "Informe início e fim válidos, no formato MM/AAAA, em ordem cronológica.";
            return false;
        }
        if (!LeituraNumerica.TentarLer(ValorMensal, out var valor) || valor < 0m)
        {
            Mensagem = "Informe um valor mensal positivo ou zero, por exemplo 1.250,00.";
            return false;
        }
        var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
        var decimos = Parcelas.Where(linha => (TipoParcelaRra)linha.Tipo.Valor == TipoParcelaRra.DecimoTerceiro).ToArray();
        if (quantidade + decimos.Length > RegraRra.LimiteParcelas)
        {
            Mensagem = $"O cálculo aceita até {RegraRra.LimiteParcelas} linhas. Reduza o período.";
            return false;
        }

        var mensais = Parcelas.Where(linha => (TipoParcelaRra)linha.Tipo.Valor == TipoParcelaRra.Mensal)
            .GroupBy(linha => linha.Competencia.Trim()).ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var gerados = new List<LinhaParcelaRraViewModel>(quantidade);
        var preservados = 0;
        for (var indice = 0; indice < quantidade; indice++)
        {
            var chave = inicio.AddMonths(indice).ToString("MM/yyyy", Cultura);
            if (mensais.TryGetValue(chave, out var existente))
            {
                gerados.Add(existente);
                preservados++;
            }
            else
                gerados.Add(new() { Competencia = chave, Valor = valor.ToString("N2", Cultura) });
        }
        var removidos = mensais.Count - preservados;
        var periodo = $"{inicio:yyyyMM}-{fim:yyyyMM}";
        if (removidos > 0 && _periodoPendente != periodo)
        {
            _periodoPendente = periodo;
            Mensagem = $"{removidos} competência(s) mensal(is) ficarão fora do período. Clique em Gerar meses novamente para confirmar; os valores das demais e as linhas de 13º serão preservados.";
            return false;
        }
        _periodoPendente = null;
        Parcelas.Clear();
        foreach (var linha in gerados.Concat(decimos).OrderBy(Ordem))
            Parcelas.Add(linha);
        Mensagem = $"{quantidade} competência(s): {preservados} preservada(s), {quantidade - preservados} nova(s)"
            + (removidos > 0 ? $", {removidos} removida(s)" : "") + ". Acrescente uma linha de 13º salário para cada ano em que ele foi pago.";
        return true;
    }

    public Result<IReadOnlyList<ParcelaRra>> Ler()
    {
        if (Parcelas.Count == 0)
            return Erro.Validacao("Gere os meses ou adicione as parcelas a que o pagamento se refere.");
        if (Parcelas.Count > RegraRra.LimiteParcelas)
            return Erro.Validacao($"O cálculo aceita até {RegraRra.LimiteParcelas} parcelas.");
        var parcelas = new List<ParcelaRra>(Parcelas.Count);
        foreach (var linha in Parcelas)
        {
            if (!TentarCompetencia(linha.Competencia, out var competencia))
                return Erro.Validacao($"Revise a competência '{linha.Competencia}' das parcelas: use MM/AAAA; no 13º salário, vale o ano (por exemplo 12/2024).");
            if (!LeituraNumerica.TentarLer(linha.Valor, out var valor))
                return Erro.Validacao($"Revise o valor da parcela {linha}: use valor monetário, por exemplo 1.250,00.");
            parcelas.Add(new(competencia, (TipoParcelaRra)linha.Tipo.Valor, valor));
        }
        var repetida = parcelas.GroupBy(item => (item.Tipo, Chave: item.Tipo == TipoParcelaRra.Mensal ? item.Competencia : new DateOnly(item.Competencia.Year, 1, 1)))
            .FirstOrDefault(grupo => grupo.Count() > 1);
        if (repetida is not null)
            return Erro.Validacao(repetida.Key.Tipo == TipoParcelaRra.Mensal
                ? $"A competência {repetida.Key.Chave:MM/yyyy} aparece mais de uma vez: some os valores do mês em uma única linha."
                : $"Há mais de uma linha de 13º salário de {repetida.Key.Chave.Year}: some os valores em uma única linha.");
        return parcelas;
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, ValorMensal,
        Parcelas.Select(linha => new LinhaSalva(linha.Competencia, (TipoParcelaRra)linha.Tipo.Valor, linha.Valor)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.Inicio is null || dados.Fim is null || dados.ValorMensal is null || dados.Parcelas is null
                || dados.Parcelas.Length > RegraRra.LimiteParcelas
                || dados.Parcelas.Any(linha => linha?.Competencia is null || linha.Valor is null || !Enum.IsDefined(linha.Tipo)))
                return false;
            Inicio = dados.Inicio;
            Fim = dados.Fim;
            ValorMensal = dados.ValorMensal;
            Parcelas.Clear();
            foreach (var linha in dados.Parcelas)
                Parcelas.Add(new() { Competencia = linha.Competencia, Tipo = LinhaParcelaRraViewModel.Tipos.First(opcao => (TipoParcelaRra)opcao.Valor == linha.Tipo), Valor = linha.Valor });
            Mensagem = string.Empty;
            _periodoPendente = null;
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Linhas vindas de outra calculadora, no formato salvo no histórico, para abrir o RRA já preenchido.</summary>
    public static string CriarImportacao(IReadOnlyList<ParcelaRra> parcelas)
    {
        var mensais = parcelas.Where(item => item.Tipo == TipoParcelaRra.Mensal).Select(item => item.Competencia).DefaultIfEmpty(parcelas[0].Competencia).ToArray();
        return JsonSerializer.Serialize(new DadosSalvos(mensais.Min().ToString("MM/yyyy", Cultura), mensais.Max().ToString("MM/yyyy", Cultura), "0,00",
            parcelas.Select(item => new LinhaSalva(item.Competencia.ToString("MM/yyyy", Cultura), item.Tipo, item.Valor.ToString("N2", Cultura))).ToArray()));
    }

    private void Adicionar(TipoParcelaRra tipo)
    {
        if (Parcelas.Count >= RegraRra.LimiteParcelas)
        {
            Mensagem = $"O cálculo aceita até {RegraRra.LimiteParcelas} parcelas.";
            return;
        }
        var ano = TentarCompetencia(Fim, out var fim) ? fim.Year : DateTime.Today.Year - 1;
        Parcelas.Add(new()
        {
            Competencia = tipo == TipoParcelaRra.DecimoTerceiro ? $"12/{ano}" : string.Empty,
            Tipo = LinhaParcelaRraViewModel.Tipos.First(opcao => (TipoParcelaRra)opcao.Valor == tipo)
        });
        Mensagem = tipo == TipoParcelaRra.DecimoTerceiro ? "Informe o ano do 13º salário na competência, por exemplo 12/2024." : string.Empty;
    }

    private static (int, int, int) Ordem(LinhaParcelaRraViewModel linha) =>
        TentarCompetencia(linha.Competencia, out var competencia)
            ? (competencia.Year, (TipoParcelaRra)linha.Tipo.Valor == TipoParcelaRra.DecimoTerceiro ? 13 : competencia.Month, 0)
            : (int.MaxValue, 0, 0);

    private static bool TentarCompetencia(string texto, out DateOnly competencia)
    {
        var lido = DateTime.TryParseExact(texto.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data);
        competencia = lido ? new DateOnly(data.Year, data.Month, 1) : default;
        return lido;
    }

    private sealed record LinhaSalva(string Competencia, TipoParcelaRra Tipo, string Valor);
    private sealed record DadosSalvos(string Inicio, string Fim, string ValorMensal, LinhaSalva[] Parcelas);
}
