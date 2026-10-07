using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoQuitacaoBancoHorasViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private QuitacaoBancoHoras? _quitacao;
    private bool _confirmado;

    public CampoQuitacaoBancoHorasViewModel() : base("Quitação do banco de horas", "Parcelas transferidas do banco de horas, sujeitas a INSS, IRRF e FGTS.")
    {
        Visivel = false;
        DescartarCommand = new RelayCommand(_ => Descartar());
    }

    public bool Confirmado { get => _confirmado; set => SetProperty(ref _confirmado, value); }
    public string FimCiclo => _quitacao?.FimCiclo.ToString("dd/MM/yyyy", Cultura) ?? string.Empty;
    public string SalarioReferencia => _quitacao?.SalarioReferencia.ToString("C2", Cultura) ?? string.Empty;
    public string Divisor => _quitacao?.Divisor.ToString("N2", Cultura) ?? string.Empty;
    public string Total => _quitacao?.Total.ToString("C2", Cultura) ?? string.Empty;
    public string TotalTexto => $"Total transferido: {Total}";
    public IReadOnlyList<LinhaQuitacaoBancoHorasViewModel> Parcelas => _quitacao?.Parcelas.Select(parcela => new LinhaQuitacaoBancoHorasViewModel(parcela)).ToArray() ?? [];
    public ICommand DescartarCommand { get; }

    public Result<QuitacaoBancoHoras?> Ler()
    {
        if (_quitacao is null)
            return Result.Ok<QuitacaoBancoHoras?>(null);
        return Confirmado ? Result.Ok<QuitacaoBancoHoras?>(_quitacao)
            : Erro.Validacao("Confira as incidências de INSS, IRRF e FGTS da quitação do banco de horas e marque a confirmação, ou descarte a transferência.");
    }

    public string Exportar() => _quitacao is null ? string.Empty : JsonSerializer.Serialize(new DadosSalvos(_quitacao, Confirmado));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.Quitacao is not { } quitacao || quitacao.Validar(quitacao.Situacao, quitacao.FimCiclo).Falhou)
                return false;
            _quitacao = quitacao;
            Confirmado = dados.Confirmado;
            Visivel = true;
            OnPropertyChanged(nameof(FimCiclo));
            OnPropertyChanged(nameof(SalarioReferencia));
            OnPropertyChanged(nameof(Divisor));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(TotalTexto));
            OnPropertyChanged(nameof(Parcelas));
            return true;
        }
        catch (JsonException) { return false; }
    }

    public static string CriarImportacao(QuitacaoBancoHoras quitacao) => JsonSerializer.Serialize(new DadosSalvos(quitacao, false));

    /// <summary>
    /// Campos que o banco de horas preenche no destino: o fechamento vai para o holerite da competência do fim do ciclo e
    /// a rescisão para o desligamento nessa data. As chaves são os rótulos dos campos do holerite e da rescisão.
    /// </summary>
    public static (TipoCalculadora Destino, IReadOnlyDictionary<string, string> Valores) Transferencia(QuitacaoBancoHoras quitacao)
    {
        var valores = new Dictionary<string, string>
        {
            ["Quitação do banco de horas"] = CriarImportacao(quitacao),
            ["Salário"] = quitacao.SalarioReferencia.ToString("N2", Cultura)
        };
        if (quitacao.Situacao == SituacaoBancoHoras.Rescisao)
        {
            valores["Data de desligamento"] = quitacao.FimCiclo.ToString("dd/MM/yyyy", Cultura);
            return (TipoCalculadora.Rescisao, valores);
        }
        valores["Competência"] = quitacao.FimCiclo.ToString("MM/yyyy", Cultura);
        valores["Divisor de horas"] = quitacao.Divisor.ToString("N2", Cultura);
        return (TipoCalculadora.Holerite, valores);
    }

    private void Descartar()
    {
        _quitacao = null;
        Confirmado = false;
        Visivel = false;
        OnPropertyChanged(nameof(Parcelas));
    }

    private sealed record DadosSalvos(QuitacaoBancoHoras Quitacao, bool Confirmado);
}
