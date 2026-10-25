using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Configuracoes;

/// <summary>Preferências do aplicativo, gravadas no aparelho, e a atualização das tabelas na hora.</summary>
public sealed partial class ConfiguracoesMobileViewModel : ObservableObject
{
    internal static readonly Uri Projeto = new("https://github.com/mayconwisley/CalculosTrabalhistasTributarios");
    private readonly IPreferenciasMobile _preferencias;
    private readonly IPlataformaMobile _plataforma;
    private readonly IRegistroDeErros _registroDeErros;
    private readonly ICopiaSegurancaHistoricoUseCase _copiaSeguranca;

    [ObservableProperty] private string _situacaoTabelas;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPendencias))]
    private string? _pendencias;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FazerCopiaSegurancaCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestaurarCopiaSegurancaCommand))]
    private bool _processandoCopia;

    public ConfiguracoesMobileViewModel(IPreferenciasMobile preferencias, IPlataformaMobile plataforma, AtualizacaoTabelasViewModel atualizacao,
        SituacaoAtualizacaoTabelas situacao, IRegistroDeErros registroDeErros, ICopiaSegurancaHistoricoUseCase copiaSeguranca)
    {
        _copiaSeguranca = copiaSeguranca;
        _registroDeErros = registroDeErros;
        _preferencias = preferencias;
        _plataforma = plataforma;
        Atualizacao = atualizacao;
        Situacao = situacao;
        _situacaoTabelas = SituacaoAtualizacaoTabelas.DescreverUltimaAtualizacao(preferencias.HorarioAtualizacaoTabelas, DateTime.Now);
        _pendencias = DescricaoAtualizacaoTabelas.DescreverPendencias(situacao.Resultados);
        Atualizacao.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AtualizacaoTabelasViewModel.Atualizando)) AtualizarAgoraCommand.NotifyCanExecuteChanged();
        };
    }

    public AtualizacaoTabelasViewModel Atualizacao { get; }
    public SituacaoAtualizacaoTabelas Situacao { get; }
    public IReadOnlyList<string> Temas { get; } = ["Automático (tema do aparelho)", "Claro", "Escuro"];
    public string Versao => $"Versão {_plataforma.Versao} • Dados processados no aparelho • Desenvolvido por Maycon Wisley";
    public bool TemPendencias => Pendencias is not null;

    public int IndiceTema
    {
        get => (int)_preferencias.Tema;
        set
        {
            if (value < 0 || value == IndiceTema) return;
            _preferencias.Tema = (TemaMobile)value;
            _plataforma.AplicarTema(_preferencias.Tema);
            OnPropertyChanged();
        }
    }

    /// <summary>Desligada, as tabelas são atualizadas só por Atualizar agora ou pela tela de cada uma.</summary>
    public bool AtualizarTabelasAoAbrir
    {
        get => _preferencias.AtualizarTabelasAoAbrir;
        set
        {
            if (value == AtualizarTabelasAoAbrir) return;
            _preferencias.AtualizarTabelasAoAbrir = value;
            OnPropertyChanged();
        }
    }

    private bool PodeAtualizar() => !Atualizacao.Atualizando;

    [RelayCommand(CanExecute = nameof(PodeAtualizar))]
    private async Task AtualizarAgora()
    {
        await Atualizacao.AtualizarAgoraAsync(DateTime.Now);
        SituacaoTabelas = SituacaoAtualizacaoTabelas.DescreverUltimaAtualizacao(_preferencias.HorarioAtualizacaoTabelas, DateTime.Now);
        // Interrompida, a atualização não muda o aviso das pendências anteriores.
        if (Atualizacao.Resultados.Count > 0)
            Pendencias = DescricaoAtualizacaoTabelas.DescreverPendencias(Atualizacao.Resultados);
    }

    [RelayCommand]
    private Task VerDetalhes() => _plataforma.MostrarAsync("Tabelas não atualizadas", DescricaoAtualizacaoTabelas.DetalharPendencias(Situacao.Resultados));

    /// <summary>Envia o registro do mês pelo aplicativo que o usuário escolher; nada é enviado automaticamente.</summary>
    [RelayCommand]
    private async Task EnviarRegistroDeErros()
    {
        var arquivo = _registroDeErros.Arquivo;
        if (!await Task.Run(() => File.Exists(arquivo)))
        {
            await _plataforma.MostrarAsync("Registro de erros", "Nenhum erro inesperado foi registrado neste mês.");
            return;
        }
        await _plataforma.CompartilharArquivoAsync("Registro de erros", arquivo);
    }

    /// <summary>Identifica as cópias deste aplicativo: os formulários do desktop têm outro formato e não reabrem no celular.</summary>
    public const string OrigemCopiaSeguranca = "Android";

    private bool PodeProcessarCopia() => !ProcessandoCopia;

    /// <summary>Gera o arquivo com todo o histórico e abre o compartilhamento; o usuário escolhe onde guardá-lo.</summary>
    [RelayCommand(CanExecute = nameof(PodeProcessarCopia))]
    private async Task FazerCopiaSeguranca()
    {
        ProcessandoCopia = true;
        try
        {
            var copia = await _copiaSeguranca.ExportarAsync(OrigemCopiaSeguranca, DateTime.Now, CancellationToken.None);
            if (copia.Falhou)
            {
                await _plataforma.MostrarAsync("Cópia de segurança", copia.Erro.Mensagem);
                return;
            }
            await _plataforma.CompartilharConteudoAsync($"Cópia de segurança: {copia.Valor.Quantidade} cálculo(s)", copia.Valor.NomeArquivo,
                copia.Valor.Conteudo, "application/json");
        }
        finally { ProcessandoCopia = false; }
    }

    /// <summary>Acrescenta ao histórico os cálculos de uma cópia; nada do histórico atual é apagado ou substituído.</summary>
    [RelayCommand(CanExecute = nameof(PodeProcessarCopia))]
    private async Task RestaurarCopiaSeguranca()
    {
        ProcessandoCopia = true;
        try
        {
            if (await _plataforma.EscolherArquivoTextoAsync("Escolha a cópia de segurança do histórico", CopiaSegurancaHistoricoUseCase.MaximoCaracteres) is not { } arquivo)
                return;
            if (arquivo.Falhou)
            {
                await _plataforma.MostrarAsync("Restaurar cópia", arquivo.Erro.Mensagem);
                return;
            }
            var restauracao = await _copiaSeguranca.RestaurarAsync(arquivo.Valor, OrigemCopiaSeguranca, CancellationToken.None);
            await _plataforma.MostrarAsync("Restaurar cópia", restauracao.Falhou ? restauracao.Erro.Mensagem : DescreverRestauracao(restauracao.Valor));
        }
        finally { ProcessandoCopia = false; }
    }

    internal static string DescreverRestauracao(ResumoRestauracaoHistoricoDto resumo) => (resumo.Incluidos, resumo.JaExistentes) switch
    {
        (0, _) => "Todos os cálculos da cópia já estavam no histórico. Nada foi alterado.",
        (_, 0) => $"{resumo.Incluidos} cálculo(s) restaurado(s) no histórico.",
        _ => $"{resumo.Incluidos} cálculo(s) restaurado(s) no histórico. {resumo.JaExistentes} já estavam salvos e não foram repetidos."
    };

    [RelayCommand]
    private Task AbrirProjeto() => _plataforma.AbrirEnderecoAsync(Projeto);
}
