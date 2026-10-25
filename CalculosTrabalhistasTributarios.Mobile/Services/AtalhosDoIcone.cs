using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

/// <summary>
/// Publica os atalhos do ícone e abre a calculadora escolhida. Com o aplicativo fechado, o atalho chega antes de o banco ser
/// preparado: fica pendente até a abertura liberar a tela inicial, como se o usuário tocasse no cartão.
/// </summary>
public sealed class AtalhosDoIcone(ILogger<AtalhosDoIcone> logger)
{
    private readonly object _trava = new();
    private NavegadorCalculadoras? _navegador;
    private TipoCalculadoraMobile? _pendente;
    private (string Id, DateTime Horario)? _ultimo;

    public async Task PublicarAsync(IReadOnlyList<string> favoritos)
    {
        try
        {
            if (!AppActions.Current.IsSupported) return;
            var acoes = AtalhosDoIconeMobile.Escolher(favoritos)
                .Select(cartao => new AppAction(AtalhosDoIconeMobile.Id(cartao.Tipo), cartao.Titulo, cartao.TextoBotao))
                .ToList();
            await AppActions.Current.SetAsync(acoes);
        }
        catch (Exception erro) when (erro is FeatureNotSupportedException or InvalidOperationException or Java.Lang.Exception)
        {
            // O lançador pode recusar atalhos (limite do fabricante, perfil de trabalho); o aplicativo segue sem eles.
            logger.LogWarning(erro, "Não foi possível publicar os atalhos do ícone.");
        }
    }

    /// <summary>Chamado pelo Android ao tocar num atalho.</summary>
    public void Receber(AppAction acao)
    {
        if (AtalhosDoIconeMobile.Interpretar(acao.Id) is not { } tipo) return;
        NavegadorCalculadoras? navegador;
        lock (_trava)
        {
            // Retomar a atividade e receber o intent podem entregar o mesmo toque duas vezes.
            var agora = DateTime.UtcNow;
            if (_ultimo is { } ultimo && ultimo.Id == acao.Id && agora - ultimo.Horario < TimeSpan.FromSeconds(2)) return;
            _ultimo = (acao.Id, agora);
            navegador = _navegador;
            if (navegador is null) _pendente = tipo;
        }
        if (navegador is not null) Abrir(navegador, tipo);
    }

    /// <summary>A janela terminou a abertura: atalhos seguintes navegam direto, e o pendente é aberto agora.</summary>
    public void Registrar(NavegadorCalculadoras navegador)
    {
        TipoCalculadoraMobile? pendente;
        lock (_trava)
        {
            _navegador = navegador;
            pendente = _pendente;
            _pendente = null;
        }
        if (pendente is { } tipo) Abrir(navegador, tipo);
    }

    public void Esquecer(NavegadorCalculadoras navegador)
    {
        lock (_trava)
            if (ReferenceEquals(_navegador, navegador)) _navegador = null;
    }

    private void Abrir(NavegadorCalculadoras navegador, TipoCalculadoraMobile tipo) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        try { await navegador.AbrirAsync(tipo); }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao abrir a calculadora {Tipo} pelo atalho do ícone.", tipo);
        }
    });
}
