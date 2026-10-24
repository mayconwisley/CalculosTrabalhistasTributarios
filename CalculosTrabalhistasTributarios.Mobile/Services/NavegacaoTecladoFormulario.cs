using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

/// <summary>Ordem visual atual dos campos, incluindo grades e seções criadas sob demanda.</summary>
internal static class NavegacaoTecladoFormulario
{
    internal static void Configurar(Page pagina, ILogger logger)
    {
        var elementos = new HashSet<VisualElement>();
        var entradas = new HashSet<Entry>();
        var visivel = false;
        var agendada = false;
        bool Disponivel(Entry entrada)
        {
            for (Element? atual = entrada; atual is not null; atual = atual.Parent)
                if (atual is VisualElement visual && (!visual.IsVisible || !visual.IsEnabled || visual.InputTransparent)) return false;
            return !entrada.IsReadOnly;
        }
        Entry[] OrdemAtual() => ApoioFormularioMobile.Descendentes(pagina).OfType<Entry>().Where(Disponivel).ToArray();
        void Limpar()
        {
            foreach (var entrada in entradas) entrada.Completed -= AoConcluir;
            foreach (var elemento in elementos) elemento.PropertyChanged -= AoAlterar;
            entradas.Clear(); elementos.Clear();
        }
        void Atualizar()
        {
            Limpar();
            if (!visivel) return;
            foreach (var elemento in ApoioFormularioMobile.Descendentes(pagina).OfType<VisualElement>())
            {
                elementos.Add(elemento); elemento.PropertyChanged += AoAlterar;
                if (elemento is Entry entrada) { entradas.Add(entrada); entrada.Completed += AoConcluir; }
            }
            var ordem = OrdemAtual();
            for (var i = 0; i < ordem.Length; i++) ordem[i].ReturnType = i == ordem.Length - 1 ? ReturnType.Done : ReturnType.Next;
        }
        void Agendar()
        {
            if (!visivel || agendada) return;
            agendada = true;
            pagina.Dispatcher.Dispatch(() => { agendada = false; Atualizar(); });
        }
        void AoAlterar(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(VisualElement.IsVisible) or nameof(VisualElement.IsEnabled)
                or nameof(VisualElement.InputTransparent) or nameof(Entry.IsReadOnly)) Agendar();
        }
        async void AoConcluir(object? sender, EventArgs args)
        {
            if (sender is not Entry entrada) return;
            try
            {
                var ordem = OrdemAtual();
                var indice = Array.IndexOf(ordem, entrada);
                if (indice >= 0 && indice + 1 < ordem.Length)
                {
                    var proxima = ordem[indice + 1];
                    for (Element? pai = proxima.Parent; pai is not null; pai = pai.Parent)
                        if (pai is ScrollView rolagem) { await rolagem.ScrollToAsync(proxima, ScrollToPosition.MakeVisible, false); break; }
                    proxima.Focus();
                }
                else { entrada.Unfocus(); await entrada.HideSoftInputAsync(CancellationToken.None); }
            }
            catch (Exception erro) { logger.LogError(erro, "Falha ao avançar foco no formulário móvel."); }
        }
        pagina.DescendantAdded += (_, _) => Agendar();
        pagina.DescendantRemoved += (_, _) => Agendar();
        pagina.Appearing += (_, _) => { visivel = true; Atualizar(); };
        pagina.Disappearing += (_, _) => { visivel = false; Limpar(); };
    }
}
