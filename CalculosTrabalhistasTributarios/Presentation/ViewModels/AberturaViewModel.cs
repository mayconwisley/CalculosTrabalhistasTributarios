using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Globalization;
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
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IAtualizarTodasTabelasUseCase _atualizarTabelas;
    private readonly IRegistroDeErros _registroDeErros;
    private readonly CancellationTokenSource _pular = new();
    private readonly RelayCommand _pularCommand;
    private string _mensagem = "Preparando o banco de dados...";
    private string _detalhe = string.Empty;
    private int _concluidas;
    private bool _indeterminado = true;
    private bool _atualizando;
    private bool _pedidaPeloUsuario;

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

    /// <summary>Pedida pelo botão Atualizar agora, a interrupção cancela a consulta, e não adia para a próxima abertura.</summary>
    public bool PedidaPeloUsuario { get => _pedidaPeloUsuario; private set => SetProperty(ref _pedidaPeloUsuario, value); }

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
        await AtualizarAsync(DateTime.Now);
    }

    /// <summary>Atualização pedida na tela principal: roda mesmo se as tabelas já foram atualizadas hoje ou se a opção ao abrir está desligada.</summary>
    public Task AtualizarAgoraAsync(DateTime agora)
    {
        PedidaPeloUsuario = true;
        return AtualizarAsync(agora);
    }

    /// <summary>Texto de quando as tabelas foram atualizadas pela última vez, pela abertura ou pelo botão Atualizar agora.</summary>
    public static string DescreverUltimaAtualizacao(Configuracoes configuracoes, DateTime agora)
    {
        if (configuracoes.HorarioAtualizacaoTabelas is { } horario)
        {
            var dia = DateOnly.FromDateTime(horario);
            var hoje = DateOnly.FromDateTime(agora);
            var quando = dia == hoje ? "hoje" : dia == hoje.AddDays(-1) ? "ontem" : $"em {horario.ToString("dd/MM/yyyy", Cultura)}";
            return $"Tabelas atualizadas pela internet {quando} às {horario.ToString("HH:mm", Cultura)}.";
        }
        // Versões anteriores gravavam só o dia.
        if (configuracoes.UltimaAtualizacaoTabelas is { } ultima)
            return $"Tabelas atualizadas pela internet em {ultima.ToString("dd/MM/yyyy", Cultura)}.";
        return "As tabelas ainda não foram atualizadas pela internet neste computador.";
    }

    private async Task AtualizarAsync(DateTime agora)
    {
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
                ConfiguracoesUsuario.Alterar(configuracoes =>
                {
                    configuracoes.UltimaAtualizacaoTabelas = DateOnly.FromDateTime(agora);
                    configuracoes.HorarioAtualizacaoTabelas = agora;
                });
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
            _registroDeErros.Registrar(exception, "Atualização das tabelas");
            Resultados = [];
            Concluir("Não foi possível atualizar as tabelas; os dados locais foram preservados.");
        }
    }

    /// <summary>Texto do aviso da tela principal quando alguma tabela não foi atualizada; nulo quando todas foram.</summary>
    public static string? DescreverPendencias(IReadOnlyList<AtualizacaoDeTabela> resultados) => DescricaoAtualizacaoTabelas.DescreverPendencias(resultados);

    /// <summary>O motivo informado pela fonte de cada tabela não atualizada.</summary>
    public static string DetalharPendencias(IReadOnlyList<AtualizacaoDeTabela> resultados) => DescricaoAtualizacaoTabelas.DetalharPendencias(resultados);

    private void Concluir(string mensagem)
    {
        Atualizando = false;
        Indeterminado = false;
        Concluidas = Total;
        Mensagem = mensagem;
        Detalhe = string.Empty;
    }
}
