namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>
/// A natureza decide se o DSR da verba também reflete nas demais parcelas. Nas horas extras, só a partir de 20/03/2023
/// (TST, Tema Repetitivo 9, que alterou a OJ 394 da SBDI-1); nas demais, conforme a premissa informada.
/// </summary>
public enum NaturezaVerbaReflexa { HorasExtras, Outra }
