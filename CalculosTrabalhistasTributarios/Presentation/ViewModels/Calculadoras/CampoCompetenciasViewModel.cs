using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Grade de competências gerada por um período. Gerar meses cria as competências de início a fim com os valores sugeridos
/// e preserva as linhas já digitadas; se alguma sair do período, o primeiro clique avisa e o segundo confirma.
/// Cada grade define as colunas, os valores sugeridos e a leitura das suas linhas.
/// </summary>
public abstract class CampoCompetenciasViewModel<TLinha> : CampoViewModel where TLinha : LinhaCompetenciaViewModel
{
    protected static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly RelayCommand _adicionarCommand;
    private string _inicio = DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura);
    private string _fim = DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura);
    private string _mensagem = string.Empty;
    private string? _periodoPendente;

    protected CampoCompetenciasViewModel(string rotulo, string dica, int limite) : base(rotulo, dica)
    {
        Limite = limite;
        GerarCommand = new RelayCommand(_ => Gerar());
        _adicionarCommand = new RelayCommand(_ => Adicionar(), _ => Linhas.Count < Limite);
        RemoverCommand = new RelayCommand(item => { if (item is TLinha linha) Linhas.Remove(linha); });
        Linhas.CollectionChanged += (_, _) => _adicionarCommand.RaiseCanExecuteChanged();
    }

    public int Limite { get; }
    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public string Mensagem { get => _mensagem; protected set => SetProperty(ref _mensagem, value); }
    public ObservableCollection<TLinha> Linhas { get; } = [];
    public ICommand GerarCommand { get; }
    public ICommand AdicionarCommand => _adicionarCommand;
    public ICommand RemoverCommand { get; }

    /// <summary>Confere os valores sugeridos antes de gerar; devolve a orientação a mostrar, ou nulo se estão válidos.</summary>
    protected abstract string? ValidarSugestoes();

    /// <summary>Uma linha nova com os valores sugeridos; a competência vem vazia em + Mês.</summary>
    protected abstract TLinha CriarLinha(string competencia);

    public bool Gerar()
    {
        if (!TentarCompetencia(Inicio, out var inicio) || !TentarCompetencia(Fim, out var fim) || inicio > fim)
        {
            Mensagem = "Informe início e fim válidos, no formato mm/aaaa, em ordem cronológica.";
            return false;
        }
        if (ValidarSugestoes() is { } orientacao)
        {
            Mensagem = orientacao;
            return false;
        }
        var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
        if (quantidade > Limite)
        {
            Mensagem = $"A simulação aceita até {Limite} competências.";
            return false;
        }

        var existentes = Linhas.GroupBy(linha => linha.Competencia.Trim()).ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var preservados = 0;
        var novas = Enumerable.Range(0, quantidade).Select(indice =>
        {
            var chave = inicio.AddMonths(indice).ToString("MM/yyyy", Cultura);
            if (existentes.TryGetValue(chave, out var existente))
            {
                preservados++;
                return existente;
            }
            return CriarLinha(chave);
        }).ToArray();
        var removidos = Linhas.Count - preservados;
        var periodo = $"{inicio:yyyyMM}-{fim:yyyyMM}";
        if (removidos > 0 && _periodoPendente != periodo)
        {
            _periodoPendente = periodo;
            Mensagem = $"{removidos} competência(s) ficarão fora do período. Clique em Gerar meses novamente para confirmar; os valores das demais serão preservados.";
            return false;
        }
        _periodoPendente = null;
        Linhas.Clear();
        foreach (var linha in novas)
            Linhas.Add(linha);
        Mensagem = $"{quantidade} competência(s): {preservados} preservada(s), {quantidade - preservados} nova(s)" + (removidos > 0 ? $", {removidos} removida(s)" : "") + ".";
        return true;
    }

    /// <summary>Lê as linhas com <paramref name="ler"/>, exigindo ao menos uma e competências únicas.</summary>
    protected Result<IReadOnlyList<T>> LerLinhas<T>(Func<TLinha, DateOnly, Result<T>> ler, string semLinhas)
    {
        if (Linhas.Count == 0)
            return Erro.Validacao(semLinhas);
        var lidas = new List<T>(Linhas.Count);
        var competencias = new HashSet<DateOnly>();
        foreach (var linha in Linhas)
        {
            if (!TentarCompetencia(linha.Competencia, out var competencia))
                return Erro.Validacao($"Revise a competência '{linha.Competencia}': use mm/aaaa.");
            if (!competencias.Add(competencia))
                return Erro.Validacao($"A competência {linha.Competencia} aparece mais de uma vez; some os valores do mês na mesma linha.");
            var lida = ler(linha, competencia);
            if (lida.Falhou)
                return lida.Erro;
            lidas.Add(lida.Valor);
        }
        return lidas;
    }

    /// <summary>Substitui o período e as linhas por dados do histórico já validados.</summary>
    protected void Restaurar(string inicio, string fim, IEnumerable<TLinha> linhas)
    {
        Inicio = inicio;
        Fim = fim;
        Linhas.Clear();
        foreach (var linha in linhas)
            Linhas.Add(linha);
        Mensagem = string.Empty;
        _periodoPendente = null;
    }

    private void Adicionar()
    {
        if (Linhas.Count >= Limite)
            return;
        Linhas.Add(CriarLinha(string.Empty));
        Mensagem = string.Empty;
    }

    /// <summary>Horas de relógio ("40:30") ou decimais ("40,5"), como nos demais campos de horas.</summary>
    protected static bool TentarHoras(string texto, out decimal horas)
    {
        var partes = texto.Trim().Split(':');
        if (partes.Length == 2 && int.TryParse(partes[0], out var inteiras) && int.TryParse(partes[1], out var minutos) && inteiras >= 0 && minutos is >= 0 and < 60)
        {
            horas = inteiras + minutos / 60m;
            return true;
        }
        return LeituraNumerica.TentarLer(texto, out horas) && horas >= 0m;
    }

    protected static string FormatarHoras(decimal horas)
    {
        var minutos = (long)Math.Round(horas * 60m, MidpointRounding.AwayFromZero);
        return $"{minutos / 60}:{minutos % 60:00}";
    }

    /// <summary>Valor monetário sugerido, como 3.300,00; inválido ou negativo vira 0,00 na linha nova.</summary>
    protected static string Moeda(string texto) => LeituraNumerica.TentarLer(texto, out var valor) && valor >= 0m ? valor.ToString("N2", Cultura) : "0,00";

    protected static bool TentarCompetencia(string texto, out DateOnly competencia)
    {
        var lido = DateTime.TryParseExact(texto.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data);
        competencia = lido ? new DateOnly(data.Year, data.Month, 1) : default;
        return lido;
    }
}
