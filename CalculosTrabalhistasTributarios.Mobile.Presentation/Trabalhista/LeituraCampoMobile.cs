using System.Globalization;
using CalculosTrabalhistasTributarios.Presentation;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Leitura dos campos do formulário e das células das grades; uma falha vira mensagem com o rótulo do campo.</summary>
public static class LeituraCampoMobile
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static int Opcao(CampoMobile campo)
    {
        if (campo.Indice < 0 || campo.Indice >= campo.Opcoes.Count)
            throw new EntradaInvalidaException($"{campo.Rotulo}: selecione uma opção válida.");
        return campo.Indice;
    }

    public static decimal Numero(CampoMobile campo)
    {
        if (!LeituraNumerica.TentarLer(campo.Valor, out var valor))
            throw new EntradaInvalidaException($"{campo.Rotulo}: informe um número, por exemplo 3.500,00.");
        return valor;
    }

    public static int Inteiro(CampoMobile campo)
    {
        if (!int.TryParse(campo.Valor.Trim(), NumberStyles.Integer, Cultura, out var valor))
            throw new EntradaInvalidaException($"{campo.Rotulo}: informe um número inteiro.");
        return valor;
    }

    public static decimal Horas(CampoMobile campo)
    {
        var texto = campo.Valor.Trim();
        var partes = texto.Split(':');
        if (partes.Length == 2 && int.TryParse(partes[0], out var horas) && int.TryParse(partes[1], out var minutos)
            && horas >= 0 && minutos is >= 0 and < 60)
            return horas + minutos / 60m;
        if (LeituraNumerica.TentarLer(texto, out var decimais)) return decimais;
        throw new EntradaInvalidaException($"{campo.Rotulo}: use horas de relógio como 10:30 ou horas decimais como 10,5.");
    }

    public static DateOnly Data(CampoMobile campo)
    {
        if (DateOnly.TryParseExact(campo.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data)) return data;
        throw new EntradaInvalidaException($"{campo.Rotulo}: informe uma data em dd/MM/yyyy.");
    }

    public static DateOnly? DataOpcional(CampoMobile campo) => campo.Valor.Trim().Length == 0 ? null : Data(campo);

    public static DateOnly Competencia(CampoMobile campo)
    {
        if (DateOnly.TryParseExact("01/" + campo.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data)) return data;
        throw new EntradaInvalidaException($"{campo.Rotulo}: informe mês e ano em MM/yyyy.");
    }
}
