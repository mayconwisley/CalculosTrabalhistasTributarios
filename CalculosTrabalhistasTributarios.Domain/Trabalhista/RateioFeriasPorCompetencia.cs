using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public static class RateioFeriasPorCompetencia
{
    public static Result<IReadOnlyList<ParcelaFeriasCompetencia>> Calcular(DateOnly inicio, int dias, decimal ferias, decimal terco)
    {
        if (dias is < 1 or > 30 || inicio.DayNumber > DateOnly.MaxValue.DayNumber - dias + 1)
            return Erro.Validacao("Confira o início do gozo e os dias de descanso: o período deve ter até 30 dias e permanecer no calendário válido.");
        if (ferias < 0m || terco < 0m || ferias > 1_000_000_000m || terco > 1_000_000_000m)
            return Erro.Validacao("As férias e o terço a distribuir devem ser valores não negativos de até R$ 1 bilhão.");

        // O eSocial distribui as férias pelas competências em que os dias foram gozados.
        // A última competência recebe os centavos restantes para preservar o recibo.
        var grupos = Enumerable.Range(0, dias).Select(indice => inicio.AddDays(indice))
            .GroupBy(data => new DateOnly(data.Year, data.Month, 1)).ToArray();
        var parcelas = new List<ParcelaFeriasCompetencia>(grupos.Length);
        var feriasAlocadas = 0m;
        var tercoAlocado = 0m;
        var diasAlocados = 0;
        for (var indice = 0; indice < grupos.Length; indice++)
        {
            var diasNoMes = grupos[indice].Count();
            diasAlocados += diasNoMes;
            var ultimo = indice == grupos.Length - 1;
            var feriasAteMes = ultimo ? ferias : CalculadoraTributacao.Arredondar(ferias * diasAlocados / dias);
            var tercoAteMes = ultimo ? terco : CalculadoraTributacao.Arredondar(terco * diasAlocados / dias);
            var parteFerias = feriasAteMes - feriasAlocadas;
            var parteTerco = tercoAteMes - tercoAlocado;
            parcelas.Add(new(grupos[indice].Key, diasNoMes, parteFerias, parteTerco));
            feriasAlocadas += parteFerias;
            tercoAlocado += parteTerco;
        }
        return parcelas;
    }
}
