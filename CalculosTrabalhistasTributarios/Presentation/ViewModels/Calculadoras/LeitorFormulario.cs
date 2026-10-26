using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Lê os campos do formulário no padrão brasileiro. Um campo em formato inválido não interrompe a leitura: o leitor
/// devolve zero, marca o campo com o que corrigir e guarda o primeiro erro, para o cálculo nem começar. Assim, todos os
/// campos inválidos aparecem destacados de uma vez.
/// </summary>
public sealed class LeitorFormulario
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O primeiro campo em formato inválido; nulo quando todos foram lidos.</summary>
    public Erro? Erro { get; private set; }

    public decimal Moeda(CampoTextoViewModel campo) =>
        LeituraNumerica.TentarLer(campo.Valor, out var valor) ? NaoNegativo(campo, valor) : Invalido(campo, "use somente números e vírgula nos centavos, por exemplo 3.500,00", "Use números e vírgula, como 3.500,00.", 0m);

    public decimal Numero(CampoTextoViewModel campo) =>
        LeituraNumerica.TentarLer(campo.Valor, out var valor) ? NaoNegativo(campo, valor) : Invalido(campo, "use somente números, com vírgula nas casas decimais", "Use números, com vírgula nas decimais.", 0m);

    public int Inteiro(CampoTextoViewModel campo) =>
        int.TryParse(campo.Valor.Trim(), NumberStyles.Integer, Cultura, out var valor) ? (int)NaoNegativo(campo, valor) : Invalido(campo, "use um número inteiro", "Use um número inteiro.", 0);

    public DateOnly Data(CampoTextoViewModel campo) =>
        DateOnly.TryParseExact(campo.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data) ? data : Invalido(campo, "use o formato dd/mm/aaaa", "Use uma data dd/mm/aaaa.", default(DateOnly));

    /// <summary>Nula quando o campo está vazio.</summary>
    public DateOnly? DataOpcional(CampoTextoViewModel campo) => string.IsNullOrWhiteSpace(campo.Valor) ? null : Data(campo);

    public DateOnly Competencia(CampoTextoViewModel campo) =>
        DateTime.TryParseExact(campo.Valor.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data) ? DateOnly.FromDateTime(data) : Invalido(campo, "use o formato mm/aaaa", "Use o mês e o ano, mm/aaaa.", default(DateOnly));

    /// <summary>Aceita horas de relógio ("10:30") ou em decimal ("10,5").</summary>
    public decimal Horas(CampoTextoViewModel campo)
    {
        var texto = campo.Valor.Trim();
        var partes = texto.Split(':');
        if (partes.Length == 2 && int.TryParse(partes[0], out var horas) && int.TryParse(partes[1], out var minutos) && horas >= 0 && minutos is >= 0 and < 60)
            return horas + minutos / 60m;
        return LeituraNumerica.TentarLer(texto, out var decimais) ? NaoNegativo(campo, decimais) : Invalido(campo, "use horas e minutos, como 10:30, ou horas decimais, como 10,5", "Use 10:30 ou 10,5 horas.", 0m);
    }

    /// <summary>Nula quando não há pensão.</summary>
    public RegraPensao? Pensao(CamposPensao pensao) => pensao.Forma.Selecionada.Valor switch
    {
        BasePensao.ValorFixo => new RegraPensao(BasePensao.ValorFixo, 0m, Moeda(pensao.Valor)),
        BasePensao forma => new RegraPensao(forma, Numero(pensao.Percentual), 0m),
        _ => null
    };

    /// <summary>
    /// Confere só o formato, ao sair do campo, com as mesmas mensagens do cálculo. Um campo vazio fica para o cálculo, que
    /// sabe se ele é opcional; as demais regras, como limites e datas fora do período, também.
    /// </summary>
    public static void ConferirFormato(CampoTextoViewModel campo)
    {
        if (string.IsNullOrWhiteSpace(campo.Valor))
            return;
        var leitor = new LeitorFormulario();
        switch (campo.Tipo)
        {
            case TipoCampo.Moeda: leitor.Moeda(campo); break;
            case TipoCampo.Numero: leitor.Numero(campo); break;
            case TipoCampo.Inteiro: leitor.Inteiro(campo); break;
            case TipoCampo.Data: leitor.Data(campo); break;
            case TipoCampo.Competencia: leitor.Competencia(campo); break;
            case TipoCampo.Horas: leitor.Horas(campo); break;
        }
    }

    // Nenhum campo do formulário aceita valor negativo. As regras de cálculo também recusam, mas com uma mensagem que
    // reúne vários campos; conferido aqui, o aviso aparece no campo digitado, como um formato inválido.
    private decimal NaoNegativo(CampoViewModel campo, decimal valor) =>
        valor >= 0m ? valor : Invalido(campo, "informe zero ou um valor positivo", "Informe zero ou um valor positivo.", 0m, "não aceita valor negativo");

    private T Invalido<T>(CampoViewModel campo, string orientacao, string orientacaoCurta, T padrao, string problema = "está em formato inválido")
    {
        Erro ??= Erro.Validacao($"O campo \"{campo.Rotulo}\" {problema}: {orientacao}.");
        campo.MensagemErro = orientacaoCurta;
        return padrao;
    }
}
