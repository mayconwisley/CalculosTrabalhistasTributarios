using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Grade de linhas editáveis (meses, vínculos, parcelas) com as mesmas entradas dos campos comuns. Qualquer alteração
/// invalida o resultado pela calculadora dona da grade.
/// </summary>
public sealed partial class GradeMobile : ObservableObject
{
    private readonly IReadOnlyList<ColunaGradeMobile> _colunas;
    private readonly Action<LinhaGradeMobile>? _aoAlterarLinha;
    [ObservableProperty] private bool _visivel = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    private string? _aviso;

    public GradeMobile(string chave, string titulo, string descricao, string nomeLinha, int limite,
        IReadOnlyList<ColunaGradeMobile> colunas, Action<LinhaGradeMobile>? aoAlterarLinha = null)
    {
        Chave = chave;
        Titulo = titulo;
        Descricao = descricao;
        NomeLinha = nomeLinha;
        Limite = limite;
        _colunas = colunas;
        _aoAlterarLinha = aoAlterarLinha;
        Linhas.CollectionChanged += (_, args) =>
        {
            if (args.OldItems is not null)
                foreach (LinhaGradeMobile linha in args.OldItems)
                    foreach (var campo in linha.Campos) campo.PropertyChanged -= CampoAlterado;
            if (args.NewItems is not null)
                foreach (LinhaGradeMobile linha in args.NewItems)
                    foreach (var campo in linha.Campos) campo.PropertyChanged += CampoAlterado;
            for (var i = 0; i < Linhas.Count; i++) Linhas[i].Titulo = $"{NomeLinha} {i + 1}";
            AdicionarCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(TemLinhas));
            Alterada?.Invoke(this, EventArgs.Empty);
        };
        Pagina = new PaginaLinhas<LinhaGradeMobile>(Linhas);
    }

    public string Chave { get; }
    public string Titulo { get; }
    public string Descricao { get; }
    public string NomeLinha { get; }
    public int Limite { get; }
    public string RotuloAdicionar => $"+ {NomeLinha}";
    public string RotuloRemover => $"Remover {NomeLinha.ToLowerInvariant()}";
    public ObservableCollection<LinhaGradeMobile> Linhas { get; } = [];
    /// <summary>Linhas mostradas na tela; o cálculo lê <see cref="Linhas"/> inteira.</summary>
    public PaginaLinhas<LinhaGradeMobile> Pagina { get; }
    public bool TemLinhas => Linhas.Count > 0;
    public bool TemAviso => !string.IsNullOrWhiteSpace(Aviso);
    /// <summary>Ação opcional que preenche a grade a partir do formulário, como gerar os meses de um período.</summary>
    public IRelayCommand? GerarCommand { get; set; }
    public string RotuloGerar { get; set; } = "Gerar meses";
    public bool TemGerar => GerarCommand is not null;

    public event EventHandler? Alterada;

    private bool PodeAdicionar() => Linhas.Count < Limite;

    [RelayCommand(CanExecute = nameof(PodeAdicionar))]
    private void Adicionar()
    {
        if (AdicionarLinha() is { } linha) Pagina.Exibir(linha);
    }

    [RelayCommand]
    private void Remover(LinhaGradeMobile? linha)
    {
        if (linha is not null) Linhas.Remove(linha);
    }

    /// <summary>Acrescenta uma linha com os valores iniciais das colunas; nulo quando o limite foi atingido.</summary>
    public LinhaGradeMobile? AdicionarLinha(IReadOnlyDictionary<string, string>? valores = null)
    {
        if (Linhas.Count >= Limite) return null;
        var linha = new LinhaGradeMobile(_colunas);
        if (valores is not null)
            foreach (var (chave, valor) in valores) linha.Campo(chave).Valor = valor;
        _aoAlterarLinha?.Invoke(linha);
        Linhas.Add(linha);
        return linha;
    }

    /// <summary>
    /// Garante uma linha para cada chave (por exemplo, a competência) sem apagar nada: linhas existentes mantêm os valores e
    /// as que ficaram fora da lista continuam na grade, para o usuário remover se quiser.
    /// </summary>
    public void Gerar(IReadOnlyList<string> chaves, string colunaChave)
    {
        if (chaves.Count > Limite)
        {
            Aviso = $"O período gera {chaves.Count} linhas; o limite é {Limite}.";
            return;
        }
        var existentes = Linhas.GroupBy(linha => linha.Campo(colunaChave).Valor.Trim())
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var foraDoPeriodo = Linhas.Where(linha => !chaves.Contains(linha.Campo(colunaChave).Valor.Trim())).ToList();
        if (chaves.Count + foraDoPeriodo.Count > Limite)
        {
            Aviso = $"Remova as linhas fora do período antes de gerar: a grade aceita até {Limite} linhas.";
            return;
        }
        var ordenadas = new List<LinhaGradeMobile>(chaves.Count + foraDoPeriodo.Count);
        var novas = 0;
        foreach (var chave in chaves)
        {
            if (existentes.TryGetValue(chave, out var linha)) ordenadas.Add(linha);
            else
            {
                var nova = new LinhaGradeMobile(_colunas);
                nova.Campo(colunaChave).Valor = chave;
                _aoAlterarLinha?.Invoke(nova);
                ordenadas.Add(nova);
                novas++;
            }
        }
        ordenadas.AddRange(foraDoPeriodo);
        // Remoção linha a linha: o Reset de Clear não informa as linhas antigas e elas ficariam inscritas duas vezes.
        while (Linhas.Count > 0) Linhas.RemoveAt(Linhas.Count - 1);
        foreach (var linha in ordenadas) Linhas.Add(linha);
        Aviso = foraDoPeriodo.Count == 0
            ? $"{novas} linha(s) criada(s); os valores já digitados foram preservados."
            : $"{novas} linha(s) criada(s); {foraDoPeriodo.Count} linha(s) fora do período foram mantidas no fim da grade. Remova-as se não fizerem parte do cálculo.";
    }

    /// <summary>Lê cada linha; a mensagem de erro identifica a linha com o nome da grade.</summary>
    public IReadOnlyList<T> Ler<T>(Func<LinhaGradeMobile, T> ler)
    {
        if (Linhas.Count > Limite)
            throw new EntradaInvalidaException($"{Titulo}: informe no máximo {Limite} linhas.");
        var valores = new List<T>(Linhas.Count);
        foreach (var linha in Linhas)
        {
            try { valores.Add(ler(linha)); }
            catch (EntradaInvalidaException erro)
            {
                Pagina.Exibir(linha);
                throw new EntradaInvalidaException($"{linha.Titulo}: {erro.Message}");
            }
        }
        return valores;
    }

    private void CampoAlterado(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(CampoMobile.Valor) or nameof(CampoMobile.Indice) or nameof(CampoMobile.Marcado))) return;
        if (sender is CampoMobile campo && _aoAlterarLinha is not null)
            if (Linhas.FirstOrDefault(linha => linha.Campos.Contains(campo)) is { } linha) _aoAlterarLinha(linha);
        Aviso = null;
        Alterada?.Invoke(this, EventArgs.Empty);
    }
}
