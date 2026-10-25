namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>
/// Atalhos do ícone do aplicativo (toque longo no ícone do Android): as calculadoras favoritas, na ordem em que foram marcadas,
/// completadas pelas mais usadas quando há menos de quatro favoritas. Tabelas favoritas não entram, porque abrem outra tela.
/// </summary>
public static class AtalhosDoIconeMobile
{
    /// <summary>O Android mostra até quatro atalhos dinâmicos na maioria dos lançadores.</summary>
    public const int Limite = 4;

    private const string Prefixo = "Calculadora.";

    public static IReadOnlyList<TipoCalculadoraMobile> Padrao { get; } =
        [TipoCalculadoraMobile.SimulacaoTributaria, TipoCalculadoraMobile.Rescisao, TipoCalculadoraMobile.Ferias, TipoCalculadoraMobile.DecimoTerceiro];

    public static IReadOnlyList<CartaoCalculadoraMobile> Escolher(IReadOnlyList<string> favoritos)
    {
        var cartoes = CatalogoCalculadorasMobile.Cartoes.ToDictionary(cartao => cartao.Tipo);
        return favoritos.Select(Interpretar).OfType<TipoCalculadoraMobile>()
            .Concat(Padrao)
            .Distinct()
            .Where(cartoes.ContainsKey)
            .Take(Limite)
            .Select(tipo => cartoes[tipo])
            .ToList();
    }

    /// <summary>Mesma chave dos favoritos e do histórico, para o atalho continuar válido entre versões.</summary>
    public static string Id(TipoCalculadoraMobile tipo) => Prefixo + tipo;

    public static TipoCalculadoraMobile? Interpretar(string? id) =>
        id is not null && id.StartsWith(Prefixo, StringComparison.Ordinal)
            && Enum.TryParse<TipoCalculadoraMobile>(id[Prefixo.Length..], out var tipo) && Enum.IsDefined(tipo)
            ? tipo : null;
}
