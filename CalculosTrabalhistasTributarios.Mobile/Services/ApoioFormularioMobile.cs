using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

/// <summary>Adapta ciclo de vida e navegação visual; a sessão mantém a política de rascunhos fora da página.</summary>
public sealed class ApoioFormularioMobile(IRascunhosFormularios armazenamento, ILogger<ApoioFormularioMobile> logger)
{
    public static readonly BindableProperty RecebeuDadosProperty = BindableProperty.CreateAttached("RecebeuDados", typeof(bool), typeof(ApoioFormularioMobile), false);

    public void Configurar(Page pagina, TipoCalculadoraMobile tipo)
    {
        if (pagina.BindingContext is not IFormularioEditavelMobile formulario) return;
        NavegacaoTecladoFormulario.Configurar(pagina, logger);
        var controle = new ControleRascunhoFormulario(armazenamento, tipo.ToString(), formulario);
        var iniciais = formulario.ExportarDados();
        var timer = pagina.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        var preparando = false;
        var gravando = false;
        var operacao = new SemaphoreSlim(1, 1);
        var visivel = false;
        Window? janela = null;
        var status = new Label { Text = "Rascunho automático neste aparelho. Use Salvar para guardar no histórico.", FontSize = 12 };
        var conteudo = Descendentes(pagina).OfType<ScrollView>().FirstOrDefault()?.Content as VerticalStackLayout;
        conteudo?.Children.Insert(0, status);

        async Task PersistirAsync(bool imediato)
        {
            if (!controle.Preparada) return;
            await operacao.WaitAsync();
            gravando = true;
            try
            {
                if (await controle.PersistirAsync(imediato, CancellationToken.None))
                    status.Text = "Rascunho salvo neste aparelho. O histórico é salvo pelo botão Salvar.";
            }
            catch (Exception erro)
            {
                logger.LogError(erro, "Falha ao guardar rascunho móvel {Tipo}.", tipo);
                status.Text = "Não foi possível guardar o rascunho. Use Salvar para proteger seus dados.";
            }
            finally { gravando = false; operacao.Release(); }
        }

        async void AoParar(object? sender, EventArgs args) => await PersistirAsync(imediato: true);
        timer.Tick += async (_, _) =>
        {
            if (!gravando && controle.TemEdicoes) await PersistirAsync(imediato: false);
            if (!controle.TemEdicoes) timer.Stop();
        };
        formulario.FormularioAlterado += (_, _) =>
        {
            if (visivel && controle.Preparada) timer.Start();
        };
        pagina.Appearing += async (_, _) =>
        {
            visivel = true;
            if (preparando) return;
            preparando = true;
            try
            {
                var recuperou = false;
                var resultado = await controle.PrepararAsync((bool)pagina.GetValue(RecebeuDadosProperty), async _ =>
                {
                    recuperou = await pagina.DisplayAlertAsync("Recuperar rascunho?", "Há um preenchimento anterior desta calculadora neste aparelho.", "Recuperar", "Descartar");
                    return recuperou;
                }, CancellationToken.None);
                if (resultado.Falhou) await pagina.DisplayAlertAsync("Rascunho indisponível", resultado.Erro.Mensagem, "Entendi");
                else if (recuperou) status.Text = "Rascunho recuperado. Calcule novamente para conferir os valores.";
            }
            catch (Exception erro)
            {
                logger.LogError(erro, "Falha ao recuperar rascunho móvel {Tipo}.", tipo);
                status.Text = "Rascunho indisponível. Você pode descartá-lo pelo menu desta tela.";
            }
            finally { preparando = false; }
            if (!visivel) return;
            janela = pagina.Window;
            janela.Stopped -= AoParar;
            janela.Stopped += AoParar;
            if (controle.TemEdicoes) timer.Start();
        };
        pagina.Disappearing += async (_, _) =>
        {
            visivel = false;
            timer.Stop();
            if (janela is not null) janela.Stopped -= AoParar;
            await PersistirAsync(imediato: true);
        };
        pagina.ToolbarItems.Add(new ToolbarItem
        {
            Text = "Descartar rascunho", Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () =>
            {
                if (gravando || !controle.Preparada || preparando) return;
                if (!await pagina.DisplayAlertAsync("Descartar preenchimento?", "O formulário voltará aos valores iniciais e a cópia automática será apagada. Históricos salvos serão preservados.", "Descartar", "Cancelar")) return;
                await operacao.WaitAsync();
                gravando = true;
                try
                {
                    var resultado = await controle.DescartarAsync(iniciais, CancellationToken.None);
                    if (resultado.Falhou) { await pagina.DisplayAlertAsync("Rascunho", resultado.Erro.Mensagem, "Entendi"); return; }
                    status.Text = "Rascunho descartado. Novas edições serão guardadas automaticamente.";
                }
                catch (Exception erro)
                {
                    logger.LogError(erro, "Falha ao descartar rascunho móvel {Tipo}.", tipo);
                    await pagina.DisplayAlertAsync("Rascunho", "Não foi possível descartar a cópia automática. Tente novamente.", "Entendi");
                }
                finally { gravando = false; operacao.Release(); }
            })
        });
        pagina.ToolbarItems.Add(new ToolbarItem
        {
            Text = "Ir ao resultado", Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () =>
            {
                var resultado = Descendentes(pagina).OfType<ResultadoSimulacaoView>().FirstOrDefault();
                var rolagem = Descendentes(pagina).OfType<ScrollView>().FirstOrDefault();
                if (resultado is { TemResultado: true } && rolagem is not null)
                    await rolagem.ScrollToAsync(resultado, ScrollToPosition.Start, false);
                else await pagina.DisplayAlertAsync("Resultado", "Preencha os dados e toque em Calcular para obter o resultado.", "Entendi");
            })
        });
    }

    internal static IEnumerable<Element> Descendentes(Element raiz)
    {
        if (raiz is not IElementController controller) yield break;
        foreach (var filho in controller.LogicalChildren)
        {
            yield return filho;
            foreach (var descendente in Descendentes(filho)) yield return descendente;
        }
    }
}
