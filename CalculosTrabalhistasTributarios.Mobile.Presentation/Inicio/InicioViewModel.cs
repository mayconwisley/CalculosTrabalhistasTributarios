using System.Globalization;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>
/// Tela inicial e menu lateral, que compartilham a busca e as favoritas, como o menu lateral do desktop. A busca procura no
/// título e na descrição, sem diferenciar maiúsculas e acentos; com várias palavras, o item precisa ter todas.
/// </summary>
public sealed partial class InicioViewModel : ObservableObject
{
    private static readonly CompareInfo Comparacao = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
    private readonly IPreferenciasMobile _preferencias;
    private readonly IPlataformaMobile _plataforma;
    private readonly INavegacaoMenu _menu;
    private readonly SituacaoAtualizacaoTabelas _situacao;
    private readonly ILogger<InicioViewModel> _logger;
    private readonly Dictionary<string, AtalhoMobile> _porChave;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirCommand))]
    private bool _ocupado;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensagemSemResultado))]
    private string _busca = string.Empty;
    [ObservableProperty] private bool _semResultado;
    [ObservableProperty] private bool _temFavoritas;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoTabelas))]
    private string? _avisoTabelas;

    public InicioViewModel(INavegacaoCalculadoras navegacao, INavegacaoMenu menu, IPreferenciasMobile preferencias, IPlataformaMobile plataforma,
        SituacaoAtualizacaoTabelas situacao, ILogger<InicioViewModel> logger)
    {
        _menu = menu;
        _preferencias = preferencias;
        _plataforma = plataforma;
        _situacao = situacao;
        _logger = logger;
        Grupos = [.. CatalogoCalculadorasMobile.Grupos.Select(grupo => new GrupoAtalhosMobile(grupo.Titulo,
            [.. grupo.Cartoes.Select(cartao => new AtalhoMobile($"Calculadora.{cartao.Tipo}", cartao.Titulo, cartao.Descricao, cartao.TextoBotao,
                () => navegacao.AbrirAsync(cartao.Tipo)))]))];
        GruposTabelas = [.. CatalogoTabelasMobile.Grupos.Select(grupo => new GrupoAtalhosMobile(grupo.Titulo,
            [.. grupo.Cartoes.Select(cartao => new AtalhoMobile($"Tabela.{cartao.Tipo}", cartao.Titulo, cartao.Descricao, "Abrir tabela",
                () => menu.AbrirTabelaAsync(cartao.Tipo)))]))];
        _porChave = Grupos.Concat(GruposTabelas).SelectMany(grupo => grupo.Todos).ToDictionary(atalho => atalho.Chave);
        foreach (var atalho in _porChave.Values)
            atalho.Favorito = preferencias.Favoritos.Contains(atalho.Chave);
        Favoritas = new GrupoAtalhosMobile("Favoritas", []) { Expandido = true };
        QuantidadeCalculadoras = Grupos.Sum(grupo => grupo.Todos.Count);
        QuantidadeTabelas = GruposTabelas.Sum(grupo => grupo.Todos.Count);
        DescreverAvisoTabelas();
        situacao.Alterada += (_, _) => DescreverAvisoTabelas();
        Atualizar();
    }

    public IReadOnlyList<GrupoAtalhosMobile> Grupos { get; }
    public IReadOnlyList<GrupoAtalhosMobile> GruposTabelas { get; }
    public GrupoAtalhosMobile Favoritas { get; }
    public int QuantidadeCalculadoras { get; }
    public int QuantidadeTabelas { get; }
    public string Resumo => $"{QuantidadeCalculadoras} calculadoras e {QuantidadeTabelas} tabelas, com os cálculos feitos no aparelho.";
    public string MensagemSemResultado => $"Nenhuma calculadora ou tabela tem “{Busca.Trim()}” no nome ou na descrição.";
    public bool TemAvisoTabelas => AvisoTabelas is not null;

    partial void OnBuscaChanged(string value) => Atualizar();

    private bool PodeAbrir(AtalhoMobile? atalho) => !Ocupado;

    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private async Task Abrir(AtalhoMobile? atalho)
    {
        if (atalho is null) return;
        Ocupado = true;
        Mensagem = null;
        try { await atalho.AbrirAsync(); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir {Atalho}.", atalho.Chave);
            Mensagem = "Não foi possível abrir a tela. Tente novamente.";
        }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private void AlternarFavorito(AtalhoMobile? atalho)
    {
        if (atalho is null) return;
        atalho.Favorito = !atalho.Favorito;
        _preferencias.DefinirFavorito(atalho.Chave, atalho.Favorito);
        Atualizar();
    }

    [RelayCommand]
    private void LimparBusca() => Busca = string.Empty;

    [RelayCommand]
    private Task AbrirConfiguracoes() => _menu.AbrirConfiguracoesAsync();

    [RelayCommand]
    private Task VoltarAoInicio() => _menu.VoltarAoInicioAsync();

    [RelayCommand]
    private Task VerPendencias() => _plataforma.MostrarAsync("Tabelas não atualizadas", DescricaoAtualizacaoTabelas.DetalharPendencias(_situacao.Resultados));

    [RelayCommand]
    private void DispensarAvisoTabelas() => AvisoTabelas = null;

    private void DescreverAvisoTabelas() => AvisoTabelas = DescricaoAtualizacaoTabelas.DescreverPendencias(_situacao.Resultados);

    private void Atualizar()
    {
        var termos = Busca.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool Atende(AtalhoMobile atalho) => termos.All(termo => Contem(atalho.Titulo, termo) || Contem(atalho.Descricao, termo));
        var emBusca = termos.Length > 0;

        // Chaves gravadas de itens que não existem mais (por exemplo, de outra versão) são ignoradas.
        Favoritas.Atualizar(_preferencias.Favoritos.Where(_porChave.ContainsKey).Select(chave => _porChave[chave]).Where(Atende), emBusca);
        foreach (var grupo in Grupos.Concat(GruposTabelas))
            grupo.Atualizar(grupo.Todos.Where(atalho => !atalho.Favorito && Atende(atalho)), emBusca);
        TemFavoritas = Favoritas.Visivel;
        SemResultado = emBusca && !TemFavoritas && Grupos.Concat(GruposTabelas).All(grupo => !grupo.Visivel);
    }

    private static bool Contem(string texto, string termo) => Comparacao.IndexOf(texto, termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
