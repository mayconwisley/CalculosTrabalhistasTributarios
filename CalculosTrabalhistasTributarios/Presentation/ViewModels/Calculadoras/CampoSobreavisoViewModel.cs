using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Competências de sobreaviso e prontidão. Gerar meses cria as competências do período com os valores sugeridos e preserva
/// as linhas já digitadas; se alguma sair do período, o primeiro clique avisa e o segundo confirma.
/// </summary>
public sealed class CampoSobreavisoViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly RelayCommand _adicionarCommand;
    private string _inicio = DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura);
    private string _fim = DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura);
    private string _salarioMensal = "0,00";
    private string _sobreavisoMensal = "0:00";
    private string _prontidaoMensal = "0:00";
    private string _mensagem = string.Empty;
    private string? _periodoPendente;

    public CampoSobreavisoViewModel() : base("Competências de sobreaviso", "Salário, horas de sobreaviso e de prontidão e feriados de cada competência.")
    {
        GerarCommand = new RelayCommand(_ => Gerar());
        _adicionarCommand = new RelayCommand(_ => Adicionar(), _ => Linhas.Count < MotorReflexos.MaximoDeMeses);
        RemoverCommand = new RelayCommand(item => { if (item is LinhaSobreavisoViewModel linha) Linhas.Remove(linha); });
        Linhas.CollectionChanged += (_, _) => _adicionarCommand.RaiseCanExecuteChanged();
    }

    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public string SalarioMensal { get => _salarioMensal; set => SetProperty(ref _salarioMensal, value); }
    public string SobreavisoMensal { get => _sobreavisoMensal; set => SetProperty(ref _sobreavisoMensal, value); }
    public string ProntidaoMensal { get => _prontidaoMensal; set => SetProperty(ref _prontidaoMensal, value); }
    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public ObservableCollection<LinhaSobreavisoViewModel> Linhas { get; } = [];
    public ICommand GerarCommand { get; }
    public ICommand AdicionarCommand => _adicionarCommand;
    public ICommand RemoverCommand { get; }

    public bool Gerar()
    {
        if (!TentarCompetencia(Inicio, out var inicio) || !TentarCompetencia(Fim, out var fim) || inicio > fim)
        {
            Mensagem = "Informe início e fim válidos, no formato MM/AAAA, em ordem cronológica.";
            return false;
        }
        if (!LeituraNumerica.TentarLer(SalarioMensal, out var salario) || salario < 0m || !TentarHoras(SobreavisoMensal, out var sobreaviso) || !TentarHoras(ProntidaoMensal, out var prontidao))
        {
            Mensagem = "Informe o salário em reais, como 3.300,00, e as horas como 40:00 ou 40,5.";
            return false;
        }
        var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
        if (quantidade > MotorReflexos.MaximoDeMeses)
        {
            Mensagem = $"A simulação aceita até {MotorReflexos.MaximoDeMeses} competências.";
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
            return new LinhaSobreavisoViewModel { Competencia = chave, Salario = salario.ToString("N2", Cultura), Sobreaviso = FormatarHoras(sobreaviso), Prontidao = FormatarHoras(prontidao) };
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

    public Result<IReadOnlyList<MesSobreaviso>> Ler()
    {
        if (Linhas.Count == 0)
            return Erro.Validacao("Gere os meses ou adicione as competências de sobreaviso e prontidão.");
        var meses = new List<MesSobreaviso>(Linhas.Count);
        foreach (var linha in Linhas)
        {
            if (!TentarCompetencia(linha.Competencia, out var competencia))
                return Erro.Validacao($"Revise a competência '{linha.Competencia}': use MM/AAAA.");
            if (!LeituraNumerica.TentarLer(linha.Salario, out var salario))
                return Erro.Validacao($"Revise o salário de {linha}: use um valor monetário, por exemplo 3.300,00.");
            if (!TentarHoras(linha.Sobreaviso, out var sobreaviso) || !TentarHoras(linha.Prontidao, out var prontidao))
                return Erro.Validacao($"Revise as horas de {linha}: use horas e minutos, como 40:00, ou horas decimais, como 40,5.");
            if (!int.TryParse(linha.Feriados.Trim(), NumberStyles.Integer, Cultura, out var feriados) || feriados < 0)
                return Erro.Validacao($"Revise os feriados de {linha}: use um número inteiro, como 1.");
            meses.Add(new(competencia, salario, sobreaviso, prontidao, feriados));
        }
        if (meses.GroupBy(mes => mes.Competencia).Any(grupo => grupo.Count() > 1))
            return Erro.Validacao("Cada competência deve aparecer uma só vez; some as horas do mês na mesma linha.");
        return meses;
    }

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, SalarioMensal, SobreavisoMensal, ProntidaoMensal,
        Linhas.Select(linha => new LinhaSalva(linha.Competencia, linha.Salario, linha.Sobreaviso, linha.Prontidao, linha.Feriados)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.Inicio is null || dados.Fim is null || dados.SalarioMensal is null || dados.SobreavisoMensal is null || dados.ProntidaoMensal is null
                || dados.Linhas is null || dados.Linhas.Length > MotorReflexos.MaximoDeMeses
                || dados.Linhas.Any(linha => linha?.Competencia is null || linha.Salario is null || linha.Sobreaviso is null || linha.Prontidao is null || linha.Feriados is null))
                return false;
            Inicio = dados.Inicio;
            Fim = dados.Fim;
            SalarioMensal = dados.SalarioMensal;
            SobreavisoMensal = dados.SobreavisoMensal;
            ProntidaoMensal = dados.ProntidaoMensal;
            Linhas.Clear();
            foreach (var linha in dados.Linhas)
                Linhas.Add(new() { Competencia = linha.Competencia, Salario = linha.Salario, Sobreaviso = linha.Sobreaviso, Prontidao = linha.Prontidao, Feriados = linha.Feriados });
            Mensagem = string.Empty;
            _periodoPendente = null;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private void Adicionar()
    {
        if (Linhas.Count >= MotorReflexos.MaximoDeMeses)
            return;
        var salario = LeituraNumerica.TentarLer(SalarioMensal, out var valor) && valor >= 0m ? valor.ToString("N2", Cultura) : "0,00";
        Linhas.Add(new() { Salario = salario });
        Mensagem = string.Empty;
    }

    /// <summary>Horas de relógio ("40:30") ou decimais ("40,5"), como nos demais campos de horas.</summary>
    private static bool TentarHoras(string texto, out decimal horas)
    {
        var partes = texto.Trim().Split(':');
        if (partes.Length == 2 && int.TryParse(partes[0], out var inteiras) && int.TryParse(partes[1], out var minutos) && inteiras >= 0 && minutos is >= 0 and < 60)
        {
            horas = inteiras + minutos / 60m;
            return true;
        }
        return LeituraNumerica.TentarLer(texto, out horas) && horas >= 0m;
    }

    private static string FormatarHoras(decimal horas)
    {
        var minutos = (long)Math.Round(horas * 60m, MidpointRounding.AwayFromZero);
        return $"{minutos / 60}:{minutos % 60:00}";
    }

    private static bool TentarCompetencia(string texto, out DateOnly competencia)
    {
        var lido = DateTime.TryParseExact(texto.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data);
        competencia = lido ? new DateOnly(data.Year, data.Month, 1) : default;
        return lido;
    }

    private sealed record LinhaSalva(string Competencia, string Salario, string Sobreaviso, string Prontidao, string Feriados);
    private sealed record DadosSalvos(string Inicio, string Fim, string SalarioMensal, string SobreavisoMensal, string ProntidaoMensal, LinhaSalva[] Linhas);
}
