namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <param name="Meses">Meses do ano com a verba, cada um com 1/12 do 13º.</param>
/// <param name="Base">Soma das bases reflexas do ano.</param>
public sealed record DecimoTerceiroReflexo(int Ano, int Meses, decimal Base, decimal Valor);
