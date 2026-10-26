using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>
/// Erros de preenchimento das janelas próprias, como a do débito judicial, no mesmo padrão da janela comum das calculadoras:
/// o campo com erro recebe o que corrigir logo abaixo dele, a grade recebe a mensagem abaixo do título e o aviso junto do
/// botão de cálculo resume o problema. A janela liga cada campo pelo nome da propriedade, como em <c>Erros[DataCalculo]</c>.
/// </summary>
public sealed class ErrosDoFormulario : ViewModelBase
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IReadOnlyDictionary<string, string> _rotulos;
    private readonly string? _grade;
    private readonly Dictionary<string, string> _mensagens = [];
    private string? _primeiraMensagem;
    private string _aviso = string.Empty;
    private bool _avisoDosCampos;

    /// <param name="rotulos">O rótulo de cada campo na tela, pelo nome da propriedade, para citar no aviso e reconhecer nas regras.</param>
    /// <param name="grade">A propriedade da grade de linhas, quando a janela tem uma.</param>
    public ErrosDoFormulario(IReadOnlyDictionary<string, string> rotulos, string? grade = null)
    {
        _rotulos = rotulos;
        _grade = grade;
    }

    /// <summary>O que corrigir no campo; nulo quando o campo está certo.</summary>
    public string? this[string campo] => _mensagens.GetValueOrDefault(campo);

    public string Aviso { get => _aviso; private set { SetProperty(ref _aviso, value); OnPropertyChanged(nameof(TemAviso)); } }
    public bool TemAviso => Aviso.Length > 0;

    /// <summary>Há campos a corrigir: a janela leva o foco ao primeiro deles.</summary>
    public event Action? CampoComErro;

    /// <summary>Antes de ler o formulário de novo.</summary>
    public void Limpar()
    {
        _mensagens.Clear();
        _primeiraMensagem = null;
        _avisoDosCampos = false;
        OnPropertyChanged("Item[]");
        Aviso = string.Empty;
    }

    /// <summary>Marca o campo com a orientação curta; a mensagem completa vai para o aviso quando é o único erro.</summary>
    public void Marcar(string campo, string orientacao, string mensagem)
    {
        if (!_mensagens.TryAdd(campo, orientacao))
            return;
        _primeiraMensagem ??= mensagem;
        OnPropertyChanged("Item[]");
    }

    /// <summary>Mostra o aviso dos campos marcados na leitura; falso quando a leitura não encontrou erros.</summary>
    public bool Apresentar()
    {
        if (_mensagens.Count == 0)
            return false;
        Aviso = _mensagens.Count > 1
            ? $"Corrija os {_mensagens.Count} campos destacados: {Lista(_mensagens.Keys.Select(Rotulo))}."
            : _primeiraMensagem!;
        _avisoDosCampos = true;
        CampoComErro?.Invoke();
        return true;
    }

    /// <summary>
    /// Uma regra do cálculo que falhou: os campos que ela cita ficam destacados, e o aviso traz a mensagem. Sem campo citado,
    /// o destaque vai para <paramref name="padrao"/>, como a grade quando a regra trata das marcações dos dias. Falso para
    /// uma falha que não é de preenchimento, como uma tabela ausente, que continua numa mensagem.
    /// </summary>
    public bool Regra(Erro erro, string? padrao = null)
    {
        if (erro.Tipo != TipoErro.Validacao)
            return false;
        var citados = CamposCitados.Encontrar(erro.Mensagem, _rotulos.Select(rotulo => (rotulo.Key, rotulo.Value)), _grade);
        if (citados.Count == 0 && padrao is not null)
            citados = [(padrao, erro.Mensagem)];
        foreach (var (campo, mensagem) in citados)
            _mensagens.TryAdd(campo, mensagem);
        OnPropertyChanged("Item[]");
        Aviso = erro.Mensagem;
        _avisoDosCampos = citados.Count > 0;
        if (citados.Count > 0)
            CampoComErro?.Invoke();
        return true;
    }

    /// <summary>O campo foi alterado: deixa de ser apontado. Corrigidos todos, o aviso deles sai; um aviso sem campo fica até o próximo cálculo.</summary>
    public void Corrigido(string campo)
    {
        if (!_mensagens.Remove(campo))
            return;
        OnPropertyChanged("Item[]");
        if (_mensagens.Count == 0 && _avisoDosCampos)
            Aviso = string.Empty;
    }

    public DateOnly Data(string campo, string texto) =>
        DateOnly.TryParseExact(texto.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data) ? data : Invalido(campo, "use o formato dd/mm/aaaa", "Use uma data dd/mm/aaaa.", default(DateOnly));

    /// <summary>Data que pode ficar em branco.</summary>
    public DateOnly? DataOpcional(string campo, string texto) => string.IsNullOrWhiteSpace(texto) ? null : Data(campo, texto);

    public DateOnly Competencia(string campo, string texto) =>
        DateTime.TryParseExact(texto.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data) ? DateOnly.FromDateTime(data) : Invalido(campo, "use o formato mm/aaaa", "Use o mês e o ano, mm/aaaa.", default(DateOnly));

    public decimal Moeda(string campo, string texto) =>
        LeituraNumerica.TentarLer(texto, out var valor) ? NaoNegativo(campo, valor) : Invalido(campo, "use somente números e vírgula nos centavos, por exemplo 3.500,00", "Use números e vírgula, como 3.500,00.", 0m);

    public decimal Numero(string campo, string texto) =>
        LeituraNumerica.TentarLer(texto, out var valor) ? NaoNegativo(campo, valor) : Invalido(campo, "use somente números, com vírgula nas casas decimais", "Use números, com vírgula nas decimais.", 0m);

    public int Inteiro(string campo, string texto) =>
        int.TryParse(texto.Trim(), NumberStyles.Integer, Cultura, out var valor) ? (int)NaoNegativo(campo, valor) : Invalido(campo, "use um número inteiro", "Use um número inteiro.", 0);

    /// <summary>Duração em horas de relógio, como 8:00, devolvida em minutos.</summary>
    public int Duracao(string campo, string texto) =>
        LeituraDeHoras.TentarLerDuracao(texto, out var minutos) ? minutos : Invalido(campo, "use horas e minutos, como 8:00", "Use horas e minutos, como 8:00.", 0);

    /// <summary>Horário de relógio, como 08:00, que pode ficar em branco.</summary>
    public void HorarioOpcional(string campo, string texto)
    {
        if (!string.IsNullOrWhiteSpace(texto) && !LeituraDeHoras.TentarLerHorario(texto, out _))
            Invalido(campo, "use um horário com horas e minutos, como 08:00", "Use um horário, como 08:00.", 0);
    }

    /// <summary>Uma linha da grade com erro: a mensagem, que identifica a linha, fica abaixo do título da grade.</summary>
    public void MarcarGrade(string mensagem) => Marcar(_grade ?? throw new InvalidOperationException("A janela não tem grade."), mensagem, mensagem);

    private decimal NaoNegativo(string campo, decimal valor) =>
        valor >= 0m ? valor : Invalido(campo, "informe zero ou um valor positivo", "Informe zero ou um valor positivo.", 0m, "não aceita valor negativo");

    private T Invalido<T>(string campo, string orientacao, string orientacaoCurta, T padrao, string problema = "está em formato inválido")
    {
        Marcar(campo, orientacaoCurta, $"O campo \"{Rotulo(campo)}\" {problema}: {orientacao}.");
        return padrao;
    }

    private string Rotulo(string campo) => _rotulos.GetValueOrDefault(campo, campo);

    private static string Lista(IEnumerable<string> itens)
    {
        var lista = itens.ToArray();
        return lista.Length == 1 ? lista[0] : $"{string.Join(", ", lista[..^1])} e {lista[^1]}";
    }
}
