using System.Collections.ObjectModel;
using System.Globalization;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>
/// Tela inicial e menu lateral, que compartilham a busca e as favoritas, como o menu lateral do desktop. A busca procura no
/// título e na descrição, sem diferenciar maiúsculas e acentos; com várias palavras, o item precisa ter todas. Como no desktop,
/// as calculadoras e as tabelas ficam no menu; a tela inicial mostra as boas-vindas (resumo, recentes e primeiros passos) e,
/// só durante a busca, os itens encontrados.
/// </summary>
public sealed partial class InicioViewModel : ObservableObject
{
    private static readonly CompareInfo Comparacao = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
    private readonly IPreferenciasMobile _preferencias;
    private readonly IPlataformaMobile _plataforma;
    private readonly INavegacaoMenu _menu;
    private readonly SituacaoAtualizacaoTabelas _situacao;
    private readonly IConsultaHistoricoCalculos _historico;
    private readonly ILogger<InicioViewModel> _logger;
    private readonly Dictionary<string, AtalhoMobile> _porChave;
    private bool _exibindoAviso;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbrirRecenteCommand))]
    private bool _ocupado;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensagemSemResultado))]
    private string _busca = string.Empty;
    [ObservableProperty] private bool _semResultado;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExibeBoasVindas))]
    private bool _emBusca;
    [ObservableProperty] private string _situacaoTabelas = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemRecentes))]
    private bool _carregandoRecentes = true;
    [ObservableProperty] private bool _temFavoritas;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoTabelas))]
    private string? _avisoTabelas;

    public InicioViewModel(INavegacaoCalculadoras navegacao, INavegacaoMenu menu, IPreferenciasMobile preferencias, IPlataformaMobile plataforma,
        SituacaoAtualizacaoTabelas situacao, IConsultaHistoricoCalculos historico, ILogger<InicioViewModel> logger)
    {
        _historico = historico;
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
        Recentes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SemRecentes));
        Atualizar();
    }

    public const string TextoAvisoSimulacao =
        "Os resultados deste aplicativo são simulações feitas com a legislação e as tabelas cadastradas.\n\n" +
        "Antes de usá-los num pagamento, numa rescisão ou num processo, confira a competência, as tabelas e a convenção coletiva " +
        "da categoria. Procure um profissional especializado no cálculo para apurar e validar os valores aplicáveis ao seu caso.\n\n" +
        "O manual do usuário abre pelo menu e por Ajuda, na tela de cada calculadora.";

    /// <summary>
    /// Uma vez só, como no desktop: os resultados são simulações, e quem usa precisa saber disso antes de pagar ou cobrar um valor.
    /// Fica marcado como lido depois que o usuário fecha o aviso.
    /// </summary>
    public async Task MostrarAvisoDeSimulacaoAsync()
    {
        if (_preferencias.AvisoSimulacaoLido || _exibindoAviso) return;
        _exibindoAviso = true;
        try
        {
            await _plataforma.MostrarAsync("Antes de começar", TextoAvisoSimulacao);
            _preferencias.AvisoSimulacaoLido = true;
        }
        finally { _exibindoAviso = false; }
    }

    public IReadOnlyList<GrupoAtalhosMobile> Grupos { get; }
    public IReadOnlyList<GrupoAtalhosMobile> GruposTabelas { get; }
    public GrupoAtalhosMobile Favoritas { get; }
    public int QuantidadeCalculadoras { get; }
    public int QuantidadeTabelas { get; }
    public string MensagemSemResultado => $"Nenhuma calculadora ou tabela tem “{Busca.Trim()}” no nome ou na descrição.";
    public bool TemAvisoTabelas => AvisoTabelas is not null;
    public bool ExibeBoasVindas => !EmBusca;

    /// <summary>Assinatura do rodapé da tela inicial do desktop, com a versão instalada.</summary>
    public string Assinatura => $"Cálculos Trabalhistas e Tributários {_plataforma.Versao} • Dados processados no aparelho • Desenvolvido por: Maycon Wisley";

    /// <summary>Quantos cálculos a seção "Continuar de onde parou" mostra, como no desktop.</summary>
    public const int QuantidadeRecentes = 5;
    public ObservableCollection<CalculoRecenteMobile> Recentes { get; } = [];
    public bool SemRecentes => !CarregandoRecentes && Recentes.Count == 0;

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

    /// <summary>Relê os recentes e a data da última atualização, ao voltar à tela inicial.</summary>
    [RelayCommand]
    private async Task AtualizarBoasVindas()
    {
        SituacaoTabelas = SituacaoAtualizacaoTabelas.DescreverUltimaAtualizacao(_preferencias.HorarioAtualizacaoTabelas, DateTime.Now);
        try
        {
            // O SQLite responde de forma síncrona; a leitura sai da thread da tela.
            var pagina = await _historico.ConsultarAsync(new(Limite: QuantidadeRecentes), CancellationToken.None);
            var hoje = DateTime.Today;
            Recentes.Clear();
            foreach (var salvo in pagina.Itens)
                Recentes.Add(CalculoRecenteMobile.Criar(salvo, hoje));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao ler os cálculos recentes.");
            Recentes.Clear();
        }
        finally { CarregandoRecentes = false; }
    }

    [RelayCommand(CanExecute = nameof(PodeAbrirRecente))]
    private async Task AbrirRecente(CalculoRecenteMobile? recente)
    {
        if (recente is null) return;
        Ocupado = true;
        try { await _menu.AbrirCalculoSalvoAsync(recente.Calculo); }
        finally { Ocupado = false; }
    }

    private bool PodeAbrirRecente(CalculoRecenteMobile? recente) => !Ocupado;

    [RelayCommand]
    private Task AbrirHistorico() => _menu.AbrirHistoricoAsync();

    [RelayCommand]
    private Task AbrirManual() => _menu.AbrirManualAsync();

    [RelayCommand]
    private Task AbrirProjeto() => _plataforma.AbrirEnderecoAsync(Configuracoes.ConfiguracoesMobileViewModel.Projeto);

    [RelayCommand]
    private void AbrirMenu() => _menu.AbrirMenu();

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
        EmBusca = emBusca;

        // Chaves gravadas de itens que não existem mais (por exemplo, de outra versão) são ignoradas.
        Favoritas.Atualizar(_preferencias.Favoritos.Where(_porChave.ContainsKey).Select(chave => _porChave[chave]).Where(Atende), emBusca);
        foreach (var grupo in Grupos.Concat(GruposTabelas))
            grupo.Atualizar(grupo.Todos.Where(atalho => !atalho.Favorito && Atende(atalho)), emBusca);
        TemFavoritas = Favoritas.Visivel;
        SemResultado = emBusca && !TemFavoritas && Grupos.Concat(GruposTabelas).All(grupo => !grupo.Visivel);
    }

    private static bool Contem(string texto, string termo) => Comparacao.IndexOf(texto, termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
