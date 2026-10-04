namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Trabalho noturno urbano ou rural: a opção ajusta o adicional sugerido, de 20% ou de 25%.</summary>
internal static class CamposNoturnos
{
    public static CampoTextoViewModel Percentual() =>
        new("Adicional noturno (%)", TipoCampo.Numero, "20", "Pelo menos 20% no trabalho urbano (CLT, art. 73) e 25% no rural (Lei 5.889/1973, art. 7º).");

    public static CampoOpcaoViewModel Trabalho(CampoTextoViewModel percentual)
    {
        var campo = new CampoOpcaoViewModel("Trabalho noturno",
            [
                new("Urbano (22h às 5h)", false),
                new("Rural", true)
            ], "Urbano: das 22h às 5h, com a hora reduzida de 52 minutos e 30 segundos. Rural: das 21h às 5h na lavoura e das 20h às 4h na pecuária, sem hora reduzida.");
        // Troca o adicional sugerido só quando ele está no mínimo da outra opção, para não apagar um percentual da convenção.
        campo.AoAlterar = () =>
        {
            var rural = campo.Valor<bool>();
            if (rural && percentual.Valor.Trim() == "20")
                percentual.Valor = "25";
            else if (!rural && percentual.Valor.Trim() == "25")
                percentual.Valor = "20";
        };
        return campo;
    }
}
