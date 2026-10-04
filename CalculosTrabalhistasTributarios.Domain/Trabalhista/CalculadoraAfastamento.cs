using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Divide o afastamento entre a empresa e a Previdência. Na doença e no acidente, a empresa paga os 15 primeiros dias, e o
/// INSS paga 91% do salário de benefício a partir do 16º (Lei 8.213/1991, arts. 60 e 61). A licença-maternidade dura 120
/// dias, com o salário-maternidade igual à remuneração (art. 72), e a Empresa Cidadã a prorroga por 60 dias (Lei 11.770/2008).
/// </summary>
public static class CalculadoraAfastamento
{
    public const int DiasPagosPelaEmpresa = 15;
    public const decimal PercentualAuxilio = 91m;
    public const int DiasMaternidade = 120;
    public const int ProrrogacaoMaternidade = 60;
    public const int ProrrogacaoPaternidade = 15;

    /// <param name="dias">Dias de afastamento na doença e no acidente; nas licenças, a duração vem da lei.</param>
    /// <param name="media">Média dos salários de contribuição, base do auxílio; na falta dela, a remuneração.</param>
    public static Result<Afastamento> Calcular(TipoAfastamento tipo, DateOnly inicio, int dias, decimal remuneracao, decimal media, bool empresaCidada, decimal salarioMinimo, decimal tetoInss)
    {
        if (remuneracao <= 0m)
            return Erro.Validacao("Informe a remuneração mensal.");
        if (tipo is TipoAfastamento.Doenca or TipoAfastamento.AcidenteDeTrabalho && dias < 1)
            return Erro.Validacao("Informe quantos dias dura o afastamento.");
        if (media < 0m)
            return Erro.Validacao("A média dos salários não pode ser negativa.");

        var diaria = remuneracao / 30m;
        return tipo switch
        {
            TipoAfastamento.Maternidade => Maternidade(inicio, remuneracao, diaria, empresaCidada),
            TipoAfastamento.Paternidade => Paternidade(inicio, diaria, empresaCidada),
            _ => Incapacidade(tipo, inicio, dias, remuneracao, media > 0m ? media : remuneracao, diaria, salarioMinimo, tetoInss)
        };
    }

    /// <summary>Licença-paternidade pela LC 229/2026: 5 dias até 2026, 10 em 2027, 15 em 2028 e 20 a partir de 2029.</summary>
    public static int DiasPaternidade(int ano) => ano switch { <= 2026 => 5, 2027 => 10, 2028 => 15, _ => 20 };

    private static Afastamento Incapacidade(TipoAfastamento tipo, DateOnly inicio, int dias, decimal remuneracao, decimal media, decimal diaria, decimal salarioMinimo, decimal tetoInss)
    {
        var diasEmpresa = Math.Min(DiasPagosPelaEmpresa, dias);
        var diasInss = dias - diasEmpresa;
        var pagoPelaEmpresa = Arredondar(diaria * diasEmpresa);
        // 91% do salário de benefício, nunca abaixo do mínimo nem acima do teto; a média informada estima o salário de benefício.
        var beneficio = Arredondar(Math.Clamp(media * PercentualAuxilio / 100m, salarioMinimo, Math.Max(salarioMinimo, tetoInss)));
        var pagoPeloInss = Arredondar(beneficio / 30m * diasInss);
        // O FGTS continua no afastamento por acidente de trabalho (Lei 8.036/1990, art. 15, § 5º); na doença comum, para no 16º dia.
        var baseFgts = tipo == TipoAfastamento.AcidenteDeTrabalho ? Arredondar(diaria * dias) : pagoPelaEmpresa;
        var fim = inicio.AddDays(dias - 1);
        // Depois do auxílio por acidente de trabalho, 12 meses de estabilidade a partir do retorno (Lei 8.213/1991, art. 118).
        DateOnly? estabilidade = tipo == TipoAfastamento.AcidenteDeTrabalho && diasInss > 0 ? fim.AddDays(1).AddMonths(12).AddDays(-1) : null;
        return new Afastamento(tipo, inicio, fim, dias, diasEmpresa, diasInss, pagoPelaEmpresa, beneficio, pagoPeloInss, Arredondar(baseFgts * .08m), estabilidade);
    }

    private static Afastamento Maternidade(DateOnly inicio, decimal remuneracao, decimal diaria, bool empresaCidada)
    {
        var prorrogacao = empresaCidada ? ProrrogacaoMaternidade : 0;
        var dias = DiasMaternidade + prorrogacao;
        var salarioMaternidade = Arredondar(diaria * DiasMaternidade);
        var pagoPelaEmpresa = Arredondar(diaria * prorrogacao);
        var fim = inicio.AddDays(dias - 1);
        // Estabilidade da gestante até 5 meses após o parto (ADCT, art. 10, II, b); a licença começa até 28 dias antes dele.
        return new Afastamento(TipoAfastamento.Maternidade, inicio, fim, dias, prorrogacao, DiasMaternidade, pagoPelaEmpresa, Arredondar(remuneracao),
            salarioMaternidade, Arredondar((salarioMaternidade + pagoPelaEmpresa) * .08m), inicio.AddMonths(5));
    }

    private static Afastamento Paternidade(DateOnly inicio, decimal diaria, bool empresaCidada)
    {
        var dias = DiasPaternidade(inicio.Year) + (empresaCidada ? ProrrogacaoPaternidade : 0);
        var pago = Arredondar(diaria * dias);
        var fim = inicio.AddDays(dias - 1);
        // Garantia de emprego até um mês depois do fim da licença (LC 229/2026).
        return new Afastamento(TipoAfastamento.Paternidade, inicio, fim, dias, dias, 0, pago, 0m, 0m, Arredondar(pago * .08m), fim.AddMonths(1));
    }

    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
}
