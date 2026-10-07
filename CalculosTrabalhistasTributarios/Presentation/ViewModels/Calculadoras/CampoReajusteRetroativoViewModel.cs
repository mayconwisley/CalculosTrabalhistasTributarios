using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoReajusteRetroativoViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private string _inicio = DateTime.Today.AddMonths(-3).ToString("MM/yyyy", Cultura);
    private string _fim = DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura);
    private string _salarioAnterior = "0,00";
    private string _percentual = "0,00";
    private string _mensagem = string.Empty;
    private string? _periodoPendente;

    public CampoReajusteRetroativoViewModel() : base("Competências e reflexos", "Gere os meses e ajuste cada linha conforme o valor efetivamente pago e o valor devido.")
    {
        GerarCommand = new RelayCommand(_ => Gerar());
        AdicionarDecimoTerceiroCommand = new RelayCommand(_ => AdicionarReflexo(TipoParcelaReajuste.DecimoTerceiro));
        AdicionarFeriasCommand = new RelayCommand(_ => AdicionarReflexo(TipoParcelaReajuste.FeriasGozadas));
        RemoverReflexoCommand = new RelayCommand(item => { if (item is ParcelaReajusteLinhaViewModel linha) Reflexos.Remove(linha); });
    }

    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public string SalarioAnterior { get => _salarioAnterior; set => SetProperty(ref _salarioAnterior, value); }
    public string Percentual { get => _percentual; set => SetProperty(ref _percentual, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public ObservableCollection<ParcelaReajusteLinhaViewModel> Meses { get; } = [];
    public ObservableCollection<ParcelaReajusteLinhaViewModel> Reflexos { get; } = [];
    public ICommand GerarCommand { get; }
    public ICommand AdicionarDecimoTerceiroCommand { get; }
    public ICommand AdicionarFeriasCommand { get; }
    public ICommand RemoverReflexoCommand { get; }

    public bool Gerar()
    {
        if (!TentarCompetencia(Inicio, out var inicio) || !TentarCompetencia(Fim, out var fim) || inicio > fim)
        {
            Mensagem = "Informe início e fim válidos, no formato MM/AAAA, em ordem cronológica.";
            return false;
        }
        var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
        if (quantidade > 120)
        {
            Mensagem = "Selecione no máximo 120 competências (10 anos).";
            return false;
        }
        if (!LeituraNumerica.TentarLer(SalarioAnterior, out var salario) || salario <= 0m || salario > 1_000_000_000m || !Centavos(salario)
            || !LeituraNumerica.TentarLer(Percentual, out var percentual) || percentual <= 0m || percentual > 1000m)
        {
            Mensagem = "Informe um salário anterior positivo de até R$ 1 bilhão e um reajuste maior que zero e até 1.000%.";
            return false;
        }
        decimal devido;
        try { devido = decimal.Round(salario * (1m + percentual / 100m), 2, MidpointRounding.AwayFromZero); }
        catch (OverflowException)
        {
            Mensagem = "O salário reajustado excede o valor monetário permitido.";
            return false;
        }
        if (devido > 1_000_000_000m)
        {
            Mensagem = "O salário reajustado excede o limite de R$ 1 bilhão por mês.";
            return false;
        }
        var anteriores = Meses.GroupBy(linha => linha.Competencia).ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var preservados = 0;
        var gerados = new List<ParcelaReajusteLinhaViewModel>(quantidade);
        for (var indice = 0; indice < quantidade; indice++)
        {
            var competencia = inicio.AddMonths(indice);
            var chave = competencia.ToString("MM/yyyy", Cultura);
            if (anteriores.TryGetValue(chave, out var existente))
            {
                gerados.Add(existente);
                preservados++;
                continue;
            }
            gerados.Add(new(TipoParcelaReajuste.Salario)
            {
                Competencia = chave,
                BasePaga = salario.ToString("N2", Cultura),
                BaseDevida = devido.ToString("N2", Cultura),
                Quantidade = "1"
            });
        }
        var removidos = Meses.Count - preservados;
        var periodo = $"{inicio:yyyyMM}-{fim:yyyyMM}";
        if (removidos > 0 && _periodoPendente != periodo)
        {
            _periodoPendente = periodo;
            Mensagem = $"{removidos} competência(s) ficarão fora do novo período. Clique em Gerar meses novamente para confirmar a remoção; as demais serão preservadas.";
            return false;
        }
        _periodoPendente = null;
        Meses.Clear();
        foreach (var linha in gerados) Meses.Add(linha);
        Mensagem = $"{quantidade} competência(s): {preservados} preservada(s), {quantidade - preservados} nova(s)"
            + (removidos > 0 ? $", {removidos} fora do período removida(s)" : "") + ". Revise os valores de cada mês.";
        return true;
    }

    public Result<IReadOnlyList<ParcelaReajusteRetroativo>> Ler()
    {
        if (Meses.Count == 0)
            return Erro.Validacao("Gere as competências antes de calcular.");
        var parcelas = new List<ParcelaReajusteRetroativo>(Meses.Count + Reflexos.Count);
        foreach (var linha in Meses.Concat(Reflexos))
        {
            if (!TentarCompetencia(linha.Competencia, out var competencia)
                || !LeituraNumerica.TentarLer(linha.BasePaga, out var pago) || pago < 0m || !Centavos(pago)
                || !LeituraNumerica.TentarLer(linha.BaseDevida, out var devido) || devido < 0m || !Centavos(devido)
                || !int.TryParse(linha.Quantidade, NumberStyles.Integer, Cultura, out var quantidade))
                return Erro.Validacao($"Revise competência, valores e quantidade do lançamento {linha.TipoTexto} {linha.Competencia}.");
            parcelas.Add(new(competencia, linha.Tipo, pago, devido, quantidade));
        }
        return parcelas;
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, SalarioAnterior, Percentual,
        Meses.Select(Salvar).ToArray(), Reflexos.Select(Salvar).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados is null || dados.Inicio is null || dados.Fim is null || dados.SalarioAnterior is null || dados.Percentual is null
                || dados.Meses is null || dados.Reflexos is null || dados.Meses.Length > 120 || dados.Reflexos.Length > 10
                || dados.Meses.Any(item => item is null || item.Tipo != TipoParcelaReajuste.Salario || !LinhaCompleta(item))
                || dados.Reflexos.Any(item => item is null || item.Tipo is not (TipoParcelaReajuste.DecimoTerceiro or TipoParcelaReajuste.FeriasGozadas) || !LinhaCompleta(item)))
                return false;
            Inicio = dados.Inicio;
            Fim = dados.Fim;
            SalarioAnterior = dados.SalarioAnterior;
            Percentual = dados.Percentual;
            Meses.Clear();
            Reflexos.Clear();
            foreach (var item in dados.Meses) Meses.Add(Restaurar(item));
            foreach (var item in dados.Reflexos) Reflexos.Add(Restaurar(item));
            Mensagem = string.Empty;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private void AdicionarReflexo(TipoParcelaReajuste tipo)
    {
        if (Reflexos.Count >= 10)
        {
            Mensagem = "Limite de 10 lançamentos de 13º ou férias.";
            return;
        }
        Reflexos.Add(new(tipo) { Competencia = Fim, Quantidade = tipo == TipoParcelaReajuste.DecimoTerceiro ? "12" : "30" });
        Mensagem = string.Empty;
    }

    private static LinhaSalva Salvar(ParcelaReajusteLinhaViewModel linha) =>
        new(linha.Tipo, linha.Competencia, linha.BasePaga, linha.BaseDevida, linha.Quantidade);

    private static ParcelaReajusteLinhaViewModel Restaurar(LinhaSalva linha) => new(linha.Tipo)
    {
        Competencia = linha.Competencia,
        BasePaga = linha.BasePaga,
        BaseDevida = linha.BaseDevida,
        Quantidade = linha.Quantidade
    };

    private static bool TentarCompetencia(string? texto, out DateOnly competencia)
    {
        var valido = DateTime.TryParseExact(texto?.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data);
        competencia = valido ? new DateOnly(data.Year, data.Month, 1) : default;
        return valido;
    }

    private static bool Centavos(decimal valor) => decimal.Round(valor, 2) == valor;

    private static bool LinhaCompleta(LinhaSalva linha) => linha.Competencia is not null && linha.BasePaga is not null
        && linha.BaseDevida is not null && linha.Quantidade is not null;

    private sealed record LinhaSalva(TipoParcelaReajuste Tipo, string Competencia, string BasePaga, string BaseDevida, string Quantidade);
    private sealed record DadosSalvos(string Inicio, string Fim, string SalarioAnterior, string Percentual, LinhaSalva[] Meses, LinhaSalva[] Reflexos);
}
