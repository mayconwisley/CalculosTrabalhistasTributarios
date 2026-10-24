namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Insere separadores somente em uma sequência completa de dígitos; não corrige datas inválidas.</summary>
public static class DigitacaoPeriodoMobile
{
    public static string Formatar(string texto, TipoCampoMobile tipo)
    {
        if (!texto.All(char.IsAsciiDigit)) return texto;
        return tipo switch
        {
            TipoCampoMobile.Data when texto.Length == 8 => $"{texto[..2]}/{texto[2..4]}/{texto[4..]}",
            TipoCampoMobile.Competencia when texto.Length == 6 => $"{texto[..2]}/{texto[2..]}",
            _ => texto
        };
    }
}
