using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;

/// <summary>
/// Abertura do aplicativo e Atualizar agora: prepara o banco e, uma vez por dia, atualiza todas as tabelas pela internet,
/// mostrando o andamento. A atualização não impede o uso: Pular, uma falha ou o tempo esgotado só deixam para a próxima
/// abertura, e os dados locais são preservados. Mesmas regras de <c>AberturaViewModel</c> do desktop.
/// </summary>
public sealed partial class AtualizacaoTabelasViewModel : ObservableObject
{
    // Cada consulta tem o próprio limite; este evita que uma rede lenta prenda a abertura em todas as tabelas.
    private static readonly TimeSpan TempoMaximo = TimeSpan.FromMinutes(2);

    private readonly IAtualizarTodasTabelasUseCase _atualizarTabelas;
    private readonly IPreferenciasMobile _preferencias;
    private readonly SituacaoAtualizacaoTabelas _situacao;
    private readonly ILogger<AtualizacaoTabelasViewModel> _logger;
    private CancellationTokenSource? _interromper;

    [ObservableProperty] private string _mensagem = "Preparando o banco de dados...";
    [ObservableProperty] private string _detalhe = string.Empty;
    [ObservableProperty] private double _progresso;
    [ObservableProperty] private bool _indeterminado = true;
    [ObservableProperty] private bool _falhou;
    /// <summary>Nas configurações, o andamento só aparece depois do primeiro Atualizar agora.</summary>
    [ObservableProperty] private bool _iniciada;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InterromperCommand))]
    private bool _atualizando;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoInterromper))]
    private bool _pedidaPeloUsuario;

    public AtualizacaoTabelasViewModel(IAtualizarTodasTabelasUseCase atualizarTabelas, IPreferenciasMobile preferencias, SituacaoAtualizacaoTabelas situacao,
        IPlataformaMobile plataforma, ILogger<AtualizacaoTabelasViewModel> logger)
    {
        _atualizarTabelas = atualizarTabelas;
        _preferencias = preferencias;
        _situacao = situacao;
        _logger = logger;
        Total = atualizarTabelas.Total;
        Versao = $"Versão {plataforma.Versao}";
    }

    public int Total { get; }
    public string Versao { get; }

    /// <summary>Na abertura, Pular adia para a próxima abertura; em Atualizar agora, Cancelar só interrompe a consulta.</summary>
    public string TextoInterromper => PedidaPeloUsuario ? "Cancelar" : "Pular";

    /// <summary>Resultado de cada tabela, quando a atualização foi feita até o fim.</summary>
    public IReadOnlyList<AtualizacaoDeTabela> Resultados { get; private set; } = [];

    /// <summary>
    /// A atualização roda na primeira abertura do dia; outra abertura no mesmo dia usa as tabelas já conferidas. Uma data
    /// diferente de hoje, mesmo futura por um relógio acertado para trás, conta como dia novo.
    /// </summary>
    public static bool DeveAtualizar(bool atualizarAoAbrir, DateTime? ultimaAtualizacao, DateTime agora) =>
        atualizarAoAbrir && ultimaAtualizacao?.Date != agora.Date;

    /// <summary>Abertura: prepara o banco e atualiza as tabelas se for a primeira abertura do dia. Falso se o banco não pôde ser preparado.</summary>
    public async Task<bool> AbrirAsync(Func<CancellationToken, Task> prepararBanco, DateTime agora)
    {
        Falhou = false;
        Indeterminado = true;
        Mensagem = "Preparando o banco de dados...";
        try
        {
            await prepararBanco(CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Sem o banco, nenhuma calculadora funciona: a tela fica na abertura, com a opção de tentar de novo.
            _logger.LogError(exception, "Falha ao preparar o banco local.");
            Indeterminado = false;
            Falhou = true;
            Mensagem = "Não foi possível preparar o banco de dados do aplicativo. Feche e abra o aplicativo, ou tente de novo.";
            return false;
        }
        if (!DeveAtualizar(_preferencias.AtualizarTabelasAoAbrir, _preferencias.HorarioAtualizacaoTabelas, agora))
        {
            Concluir(_preferencias.AtualizarTabelasAoAbrir ? "As tabelas já foram atualizadas hoje." : "Abrindo o aplicativo...");
            return true;
        }
        await AtualizarAsync(agora);
        return true;
    }

    /// <summary>Atualização pedida nas configurações: roda mesmo se as tabelas já foram atualizadas hoje ou se a opção ao abrir está desligada.</summary>
    public Task AtualizarAgoraAsync(DateTime agora)
    {
        PedidaPeloUsuario = true;
        return AtualizarAsync(agora);
    }

    [RelayCommand(CanExecute = nameof(Atualizando))]
    private void Interromper() => _interromper?.Cancel();

    private async Task AtualizarAsync(DateTime agora)
    {
        Iniciada = true;
        Mensagem = "Atualizando as tabelas pela internet...";
        Detalhe = string.Empty;
        Progresso = 0;
        Indeterminado = false;
        Atualizando = true;
        using var interromper = new CancellationTokenSource(TempoMaximo);
        _interromper = interromper;
        // O progresso é criado aqui, na thread da tela, e a consulta roda fora dela.
        var progresso = new Progress<ProgressoAtualizacaoTabelas>(andamento =>
        {
            Progresso = andamento.Total == 0 ? 1 : (double)andamento.Concluidas / andamento.Total;
            Detalhe = andamento.TabelaAtual.Length == 0 ? string.Empty : $"{andamento.TabelaAtual} ({andamento.Concluidas + 1} de {andamento.Total})";
        });
        try
        {
            Resultados = await Task.Run(() => _atualizarTabelas.ExecutarAsync(progresso, interromper.Token));
            // Sem nenhuma fonte respondendo, como sem internet, a próxima abertura do dia tenta de novo.
            if (Resultados.Any(resultado => resultado.Resultado.Sucesso))
                _preferencias.HorarioAtualizacaoTabelas = agora;
            _situacao.Informar(Resultados);
            Concluir(Resultados.All(resultado => resultado.Resultado.Sucesso) ? "Tabelas atualizadas." : "Atualização concluída com pendências.");
            // Uma pausa curta para a barra cheia e o resultado serem vistos antes de a tela fechar.
            await Task.Delay(TimeSpan.FromMilliseconds(600));
        }
        catch (OperationCanceledException)
        {
            Resultados = [];
            Concluir(PedidaPeloUsuario ? "Atualização interrompida; os dados locais foram preservados." : "Atualização interrompida; ela será feita na próxima abertura.");
        }
        catch (Exception exception)
        {
            // Uma falha inesperada na atualização não pode impedir o uso das tabelas já cadastradas.
            _logger.LogError(exception, "Falha inesperada na atualização das tabelas.");
            Resultados = [];
            Concluir("Não foi possível atualizar as tabelas; os dados locais foram preservados.");
        }
        finally
        {
            _interromper = null;
        }
    }

    private void Concluir(string mensagem)
    {
        Atualizando = false;
        Indeterminado = false;
        Progresso = 1;
        Mensagem = mensagem;
        Detalhe = string.Empty;
    }
}
