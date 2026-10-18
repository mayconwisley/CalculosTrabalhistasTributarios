namespace CalculosTrabalhistasTributarios.Domain.Pensao;

/// <summary>Limites operacionais da simulação, sem significado de limite jurídico.</summary>
public static class LimitesPensao
{
    public const decimal MaximoMonetario = 999_999_999.99m;
    public static bool MoedaValida(decimal valor) => valor >= 0m && valor <= MaximoMonetario && decimal.Round(valor, 2) == valor;
}
