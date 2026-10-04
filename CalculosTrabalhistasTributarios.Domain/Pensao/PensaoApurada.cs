namespace CalculosTrabalhistasTributarios.Domain.Pensao;

/// <param name="Base">Valor sobre o qual o percentual incidiu; a própria verba no valor informado.</param>
/// <param name="Apuracao">Imposto da verba, com a pensão deduzida da base.</param>
public sealed record PensaoApurada<T>(decimal Pensao, decimal Base, T Apuracao);
