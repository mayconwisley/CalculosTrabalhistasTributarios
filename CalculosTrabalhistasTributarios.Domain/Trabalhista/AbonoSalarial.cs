using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Abono salarial do PIS/Pasep (CF, art. 239, § 3º, e Lei 7.998/1990, art. 9º): um doze avos do salário mínimo vigente
/// no pagamento por mês trabalhado no ano-base, para quem teve remuneração média até o limite do calendário.
/// </summary>
public static class AbonoSalarial
{
    /// <summary>
    /// Limite da remuneração média de cada calendário de pagamento: dois salários mínimos até o de 2025; a partir do de 2026,
    /// R$ 2.640,00 corrigidos pelo INPC, até chegar a um salário mínimo e meio (EC 135/2024). Os anos seguintes ainda não foram publicados.
    /// </summary>
    public static decimal? LimitePublicado(int anoPagamento) => anoPagamento switch
    {
        2025 => 2_640m,
        2026 => 2_766m,
        _ => null
    };

    /// <param name="meses">Meses trabalhados no ano-base; a fração de 15 dias ou mais conta como mês.</param>
    /// <param name="limite">Limite da remuneração média do calendário.</param>
    public static Result<ApuracaoAbono> Calcular(int meses, decimal remuneracaoMedia, decimal limite, bool cadastradoHa5Anos, decimal salarioMinimo)
    {
        if (meses is < 0 or > 12)
            return Erro.Validacao("Informe de 0 a 12 meses trabalhados no ano-base.");
        if (remuneracaoMedia < 0m || limite <= 0m)
            return Erro.Validacao("Informe a remuneração média e o limite do calendário.");

        var motivos = new List<string>();
        if (!cadastradoHa5Anos)
            motivos.Add("é preciso estar cadastrado no PIS/Pasep há pelo menos 5 anos");
        if (meses < 1)
            motivos.Add("é preciso ter trabalhado pelo menos 30 dias no ano-base");
        if (remuneracaoMedia > limite)
            motivos.Add("a remuneração média passou do limite do calendário");
        var valor = motivos.Count == 0 ? CalculadoraTributacao.Arredondar(salarioMinimo * meses / 12m) : 0m;
        return new ApuracaoAbono(meses, salarioMinimo, limite, motivos, valor);
    }
}
