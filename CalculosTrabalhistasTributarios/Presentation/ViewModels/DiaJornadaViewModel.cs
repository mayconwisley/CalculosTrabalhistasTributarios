using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Um dia do cartão de ponto: o tipo, a jornada prevista e as marcações são editáveis; o resto vem da apuração.</summary>
public sealed class DiaJornadaViewModel : ViewModelBase
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly Action _aoAlterar;
    private OpcaoCampo _tipoSelecionado;
    private string _previsto;
    private string _entrada1;
    private string _saida1;
    private string _entrada2;
    private string _saida2;
    private DiaJornadaDto? _resultado;

    public DiaJornadaViewModel(DateOnly data, TipoDia tipo, string previsto, string entrada1, string saida1, string entrada2, string saida2, Action aoAlterar)
    {
        Data = data;
        _tipoSelecionado = Tipos.First(opcao => (TipoDia)opcao.Valor == tipo);
        _previsto = previsto;
        _entrada1 = entrada1;
        _saida1 = saida1;
        _entrada2 = entrada2;
        _saida2 = saida2;
        _aoAlterar = aoAlterar;
    }

    public static IReadOnlyList<OpcaoCampo> Tipos { get; } =
    [
        new("Útil", TipoDia.Util),
        new("Descanso", TipoDia.Descanso),
        new("Feriado", TipoDia.Feriado)
    ];

    public DateOnly Data { get; }
    public string Dia => Data.ToString("dd/MM ddd", Cultura);
    public OpcaoCampo TipoSelecionado { get => _tipoSelecionado; set => Alterar(ref _tipoSelecionado, value); }
    public string Previsto { get => _previsto; set => Alterar(ref _previsto, value); }
    public string Entrada1 { get => _entrada1; set => Alterar(ref _entrada1, value); }
    public string Saida1 { get => _saida1; set => Alterar(ref _saida1, value); }
    public string Entrada2 { get => _entrada2; set => Alterar(ref _entrada2, value); }
    public string Saida2 { get => _saida2; set => Alterar(ref _saida2, value); }

    public string Trabalhadas => _resultado is { Trabalhado: true } dia ? Horas(dia.MinutosTrabalhados) : "";
    public string Extras => _resultado is { MinutosExtras: > 0 } dia ? Horas(dia.MinutosExtras) + (dia.ExtraComAdicionalDeDescanso ? " (100%)" : "") : "";
    public string Noturnas => _resultado is { MinutosNoturnos: > 0 } dia ? Horas(dia.MinutosNoturnos) : "";
    public string Faltas => _resultado switch { { Falta: true } => "Falta", { MinutosFaltantes: > 0 } dia => Horas(dia.MinutosFaltantes), _ => "" };
    public string Intervalos => _resultado is { } dia && dia.IntervaloSuprimido + dia.InterjornadaSuprimida > 0
        ? string.Join(" • ", new[] { dia.IntervaloSuprimido > 0 ? $"{Horas(dia.IntervaloSuprimido)} intrajornada" : null, dia.InterjornadaSuprimida > 0 ? $"{Horas(dia.InterjornadaSuprimida)} entre jornadas" : null }.OfType<string>())
        : "";
    public bool Destaque => _resultado is { } dia && (dia.Falta || dia.IntervaloSuprimido + dia.InterjornadaSuprimida > 0);

    /// <summary>Lê o dia; falso com a mensagem do que corrigir.</summary>
    public bool TentarLer(out MarcacaoDia dia, out string erro)
    {
        dia = default!;
        erro = string.Empty;
        if (!LeituraDeHoras.TentarLerDuracao(Previsto, out var previsto))
        {
            erro = $"Informe a jornada prevista de {Dia} em horas, como 8:00.";
            return false;
        }
        var periodos = new List<PeriodoTrabalhado>();
        foreach (var (entrada, saida) in new[] { (Entrada1, Saida1), (Entrada2, Saida2) })
        {
            var semEntrada = string.IsNullOrWhiteSpace(entrada);
            var semSaida = string.IsNullOrWhiteSpace(saida);
            if (semEntrada && semSaida)
                continue;
            if (semEntrada || semSaida || !LeituraDeHoras.TentarLerHorario(entrada, out var horaEntrada) || !LeituraDeHoras.TentarLerHorario(saida, out var horaSaida))
            {
                erro = $"Em {Dia}, informe a entrada e a saída como horários, por exemplo 08:00 e 12:00.";
                return false;
            }
            periodos.Add(new PeriodoTrabalhado(horaEntrada, horaSaida));
        }
        dia = new MarcacaoDia(Data, (TipoDia)TipoSelecionado.Valor, previsto, periodos);
        return true;
    }

    public void Apresentar(DiaJornadaDto? resultado)
    {
        _resultado = resultado;
        foreach (var propriedade in new[] { nameof(Trabalhadas), nameof(Extras), nameof(Noturnas), nameof(Faltas), nameof(Intervalos), nameof(Destaque) })
            OnPropertyChanged(propriedade);
    }

    public static string Horas(int minutos) => $"{minutos / 60}:{minutos % 60:00}";

    /// <summary>Nome da linha da grade para leitores de tela.</summary>
    public override string ToString() => Dia;

    private void Alterar<T>(ref T campo, T valor)
    {
        if (SetProperty(ref campo, valor))
            _aoAlterar();
    }
}
