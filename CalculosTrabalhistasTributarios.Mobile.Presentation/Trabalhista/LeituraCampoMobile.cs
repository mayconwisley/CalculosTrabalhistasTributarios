using System.Globalization;
using CalculosTrabalhistasTributarios.Presentation;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Leitura dos campos do formulário e das células das grades, com as mensagens do aplicativo Windows: o campo recebe a
/// orientação curta e o aviso, a mensagem completa com o rótulo. Nenhum campo aceita valor negativo.
/// </summary>
public static class LeituraCampoMobile
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static int Opcao(CampoMobile campo)
    {
        if (campo.Indice < 0 || campo.Indice >= campo.Opcoes.Count)
            throw Invalido(campo, "selecione uma opção da lista", "Selecione uma opção da lista.", "está sem opção selecionada");
        return campo.Indice;
    }

    public static decimal Numero(CampoMobile campo)
    {
        if (LeituraNumerica.TentarLer(campo.Valor, out var valor)) return NaoNegativo(campo, valor);
        throw campo.Tipo == TipoCampoMobile.Moeda
            ? Invalido(campo, "use somente números e vírgula nos centavos, por exemplo 3.500,00", "Use números e vírgula, como 3.500,00.")
            : Invalido(campo, "use somente números, com vírgula nas casas decimais", "Use números, com vírgula nas decimais.");
    }

    public static int Inteiro(CampoMobile campo)
    {
        if (int.TryParse(campo.Valor.Trim(), NumberStyles.Integer, Cultura, out var valor)) return (int)NaoNegativo(campo, valor);
        throw Invalido(campo, "use um número inteiro", "Use um número inteiro.");
    }

    public static decimal Horas(CampoMobile campo)
    {
        var texto = campo.Valor.Trim();
        var partes = texto.Split(':');
        if (partes.Length == 2 && int.TryParse(partes[0], out var horas) && int.TryParse(partes[1], out var minutos)
            && horas >= 0 && minutos is >= 0 and < 60)
            return horas + minutos / 60m;
        if (LeituraNumerica.TentarLer(texto, out var decimais)) return NaoNegativo(campo, decimais);
        throw Invalido(campo, "use horas e minutos, como 10:30, ou horas decimais, como 10,5", "Use 10:30 ou 10,5 horas.");
    }

    public static DateOnly Data(CampoMobile campo)
    {
        if (DateOnly.TryParseExact(campo.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data)) return data;
        throw Invalido(campo, "use o formato dd/mm/aaaa", "Use uma data dd/mm/aaaa.");
    }

    public static DateOnly? DataOpcional(CampoMobile campo) => campo.Valor.Trim().Length == 0 ? null : Data(campo);

    public static DateOnly Competencia(CampoMobile campo)
    {
        if (DateOnly.TryParseExact("01/" + campo.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data)) return data;
        throw Invalido(campo, "use o formato mm/aaaa", "Use o mês e o ano, mm/aaaa.");
    }

    /// <summary>
    /// Confere só o formato do que foi digitado, antes do cálculo, para apontar de uma vez todos os campos a corrigir. Um campo
    /// vazio fica para o cálculo, que sabe se ele é opcional; as demais regras, como limites e períodos, também.
    /// </summary>
    internal static EntradaInvalidaException? Conferir(CampoMobile campo)
    {
        if (campo.Tipo is not TipoCampoMobile.Opcao && string.IsNullOrWhiteSpace(campo.Valor)) return null;
        try
        {
            _ = campo.Tipo switch
            {
                TipoCampoMobile.Moeda or TipoCampoMobile.Numero => Numero(campo),
                TipoCampoMobile.Inteiro => Inteiro(campo),
                TipoCampoMobile.Horas => Horas(campo),
                TipoCampoMobile.Data => (object)Data(campo),
                TipoCampoMobile.Competencia => Competencia(campo),
                TipoCampoMobile.Opcao when campo.Opcoes.Count > 0 => Opcao(campo),
                _ => 0
            };
            return null;
        }
        catch (EntradaInvalidaException erro) { return erro; }
    }

    private static decimal NaoNegativo(CampoMobile campo, decimal valor) =>
        valor >= 0m ? valor : throw Invalido(campo, "informe zero ou um valor positivo", "Informe zero ou um valor positivo.", "não aceita valor negativo");

    private static EntradaInvalidaException Invalido(CampoMobile campo, string orientacao, string orientacaoCurta, string problema = "está em formato inválido") =>
        new($"O campo \"{campo.Rotulo}\" {problema}: {orientacao}.", campo, orientacaoCurta);
}
