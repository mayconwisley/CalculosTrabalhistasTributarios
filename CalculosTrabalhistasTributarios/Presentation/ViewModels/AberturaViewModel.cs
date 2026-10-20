using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>
/// Tela de abertura: prepara o banco e, uma vez por dia, atualiza todas as tabelas pela internet, mostrando o andamento.
/// A atualização não impede o uso: Pular, uma falha ou o tempo esgotado só deixam para a próxima abertura.
/// </summary>
public sealed class AberturaViewModel : ViewModelBase
{
    // Cada consulta tem o próprio limite; este evita que uma rede lenta prenda a abertura em todas as tabelas.
    private static readonly TimeSpan TempoMaximo = TimeSpan.FromMinutes(2);

    private readonly IAtualizarTodasTabelasUseCase _atualizarTabelas;
    private readonly IRegistroDeErros _registroDeErros;
    private readonly CancellationTokenSource _pular = new();
    private readonly RelayCommand _pularCommand;
    private string _mensagem = "Preparando o banco de dados...";
    private string _detalhe = string.Empty;
    private int _concluidas;
    private bool _indeterminado = true;
    private bool _atualizando;

    public AberturaViewModel(IAtualizarTodasTabelasUseCase atualizarTabelas, IRegistroDeErros registroDeErros)
    {
        _atualizarTabelas = atualizarTabelas;
        _registroDeErros = registroDeErros;
        Total = atualizarTabelas.Total;
        _pularCommand = new RelayCommand(_ => _pular.Cancel(), _ => Atualizando);
    }

    public string Mensagem { get => _mensagem; private set => SetProperty(ref _mensagem, value); }
    public string Detalhe { get => _detalhe; private set => SetProperty(ref _detalhe, value); }
    public int Concluidas { get => _concluidas; private set => SetProperty(ref _concluidas, value); }
    public int Total { get; }
    public string Versao { get; } = MainWindowViewModel.ObterVersao();

    /// <summary>Enquanto o banco é preparado não há etapas para contar; a barra só mostra que há trabalho em andamento.</summary>
    public bool Indeterminado { get => _indeterminado; private set => SetProperty(ref _indeterminado, value); }

    public bool Atualizando
    {
        get => _atualizando;
        private set
        {
            if (SetProperty(ref _atualizando, value))
                _pularCommand.RaiseCanExecuteChanged();
        }
    }

    public ICommand PularCommand => _pularCommand;

    /// <summary>Resultado de cada tabela, quando a atualização foi feita até o fim nesta abertura.</summary>
    public IReadOnlyList<AtualizacaoDeTabela> Resultados { get; private set; } = [];

    /// <summary>
    /// A atualização roda na primeira abertura do dia; outra abertura no mesmo dia usa as tabelas já conferidas. Uma data
    /// diferente de hoje, mesmo futura por um relógio acertado para trás, conta como dia novo.
    /// </summary>
    public static bool DeveAtualizar(Configuracoes configuracoes, DateOnly hoje) =>
        configuracoes.AtualizarTabelasAoAbrir && configuracoes.UltimaAtualizacaoTabelas != hoje;

    public async Task ExecutarAsync(Func<Task> prepararBanco, DateOnly hoje)
    {
        await prepararBanco();
        if (!DeveAtualizar(ConfiguracoesUsuario.Atuais, hoje))
        {
            Concluir(ConfiguracoesUsuario.Atuais.AtualizarTabelasAoAbrir ? "As tabelas já foram atualizadas hoje." : "Abrindo o aplicativo...");
            return;
        }

        Mensagem = "Atualizando as tabelas pela internet...";
        Indeterminado = false;
        Atualizando = true;
        _pular.CancelAfter(TempoMaximo);
        // O progresso é criado aqui, na thread da tela, e a consulta roda fora dela: a leitura das páginas e a gravação no
        // banco não travam a barra.
        var progresso = new Progress<ProgressoAtualizacaoTabelas>(andamento =>
        {
            Concluidas = andamento.Concluidas;
            Detalhe = andamento.TabelaAtual.Length == 0 ? string.Empty : $"{andamento.TabelaAtual} ({andamento.Concluidas + 1} de {andamento.Total})";
        });
        try
        {
            Resultados = await Task.Run(() => _atualizarTabelas.ExecutarAsync(progresso, _pular.Token));
            // Sem nenhuma fonte respondendo, como sem internet, a próxima abertura do dia tenta de novo.
            if (Resultados.Any(resultado => resultado.Resultado.Sucesso))
                ConfiguracoesUsuario.Alterar(configuracoes => configuracoes.UltimaAtualizacaoTabelas = hoje);
            Concluir(Resultados.All(resultado => resultado.Resultado.Sucesso) ? "Tabelas atualizadas." : "Atualização concluída com pendências.");
            // Uma pausa curta para a barra cheia e o resultado serem vistos antes de a tela fechar.
            await Task.Delay(TimeSpan.FromMilliseconds(600));
        }
        catch (OperationCanceledException)
        {
            Resultados = [];
            Concluir("Atualização interrompida; ela será feita na próxima abertura.");
        }
        catch (Exception exception)
        {
            // Uma falha inesperada na atualização não pode impedir o uso das tabelas já cadastradas.
            _registroDeErros.Registrar(exception, "Atualização das tabelas ao abrir");
            Resultados = [];
            Concluir("Não foi possível atualizar as tabelas; os dados locais foram preservados.");
        }
    }

    /// <summary>Texto do aviso da tela principal quando alguma tabela não foi atualizada; nulo quando todas foram.</summary>
    public static string? DescreverPendencias(IReadOnlyList<AtualizacaoDeTabela> resultados)
    {
        var falhas = resultados.Where(resultado => resultado.Resultado.Falhou).ToList();
        if (falhas.Count == 0)
            return null;
        if (falhas.Count == resultados.Count)
            return "As tabelas não foram atualizadas ao abrir: nenhuma fonte respondeu. Confira a conexão com a internet; os dados locais foram preservados e a atualização será tentada de novo na próxima abertura.";
        return $"Ao abrir, {falhas.Count} de {resultados.Count} tabelas não foram atualizadas ({string.Join(", ", falhas.Select(falha => falha.Nome))}). " +
            "Os dados locais foram preservados; atualize-as pela tela de cada tabela.";
    }

    /// <summary>O motivo informado pela fonte de cada tabela não atualizada.</summary>
    public static string DetalharPendencias(IReadOnlyList<AtualizacaoDeTabela> resultados) => string.Join("\n\n",
        resultados.Where(resultado => resultado.Resultado.Falhou).Select(falha => $"{falha.Nome}: {falha.Resultado.Erro.Mensagem}"));

    private void Concluir(string mensagem)
    {
        Atualizando = false;
        Indeterminado = false;
        Concluidas = Total;
        Mensagem = mensagem;
        Detalhe = string.Empty;
    }
}
