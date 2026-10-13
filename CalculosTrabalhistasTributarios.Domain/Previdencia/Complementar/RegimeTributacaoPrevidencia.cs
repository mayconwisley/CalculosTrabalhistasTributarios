namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

/// <summary>
/// Regressivo: IR definitivo de 35% a 10%, pelo prazo de cada aporte (Lei 11.053/2004, art. 1º). Progressivo: IR de 15%
/// retido no resgate como antecipação, ajustado na declaração anual pela tabela progressiva (art. 3º).
/// </summary>
public enum RegimeTributacaoPrevidencia { Regressivo, Progressivo }
