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
    private static readonly Uri Projeto = new("https://github.com/mayconwisley/CalculosTrabalhistasTributarios");
    private readonly IPreferenciasMobile _preferencias;
    private readonly IPlataformaMobile _plataforma;
    private readonly IRegistroDeErros _registroDeErros;

    [ObservableProperty] private string _situacaoTabelas;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPendencias))]
    private string? _pendencias;

    public ConfiguracoesMobileViewModel(IPreferenciasMobile preferencias, IPlataformaMobile plataforma, AtualizacaoTabelasViewModel atualizacao,
        SituacaoAtualizacaoTabelas situacao, IRegistroDeErros registroDeErros)
    {
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

    [RelayCommand]
    private Task AbrirProjeto() => _plataforma.AbrirEnderecoAsync(Projeto);
}
