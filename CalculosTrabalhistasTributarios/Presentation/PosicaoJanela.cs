namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>Posição e tamanho de uma janela em unidades WPF; maximizada guarda o tamanho de antes de maximizar.</summary>
public sealed record PosicaoJanela(double Left, double Top, double Width, double Height, bool Maximizada);
