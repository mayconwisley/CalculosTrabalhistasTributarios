using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>
/// Atalhos do aplicativo, mostrados pelo botão Atalhos ao lado do "?" e na tela de Configurações. Os atalhos das telas de
/// cálculo são ligados pela janela principal, que hospeda a tela aberta.
/// </summary>
public static class AtalhosDeTeclado
{
    /// <summary>Mostra a lista; registrado para todas as janelas, como o F1.</summary>
    public static RoutedUICommand Mostrar { get; } = new("Atalhos de teclado", nameof(Mostrar), typeof(AtalhosDeTeclado));

    public static IReadOnlyList<AtalhoDeTeclado> Lista { get; } =
    [
        new("Ctrl+Enter", "Calcular (Apurar, na jornada pelo ponto), sem sair do campo"),
        new("Ctrl+S", "Salvar o cálculo no histórico"),
        new("Ctrl+P", "Exportar o demonstrativo em PDF"),
        new("Ctrl+E", "Exportar o demonstrativo em Excel"),
        new("F1", "Abrir o manual na seção da tela aberta"),
        new("Ctrl+F", "Procurar uma calculadora ou tabela no menu"),
        new("Esc", "Fechar a tela aberta e voltar ao início"),
        new("Tab / Shift+Tab", "Ir ao próximo campo / ao campo anterior")
    ];

    public static string Texto => string.Join("\n", Lista.Select(atalho => $"{atalho.Teclas}: {atalho.Acao}"));
}
