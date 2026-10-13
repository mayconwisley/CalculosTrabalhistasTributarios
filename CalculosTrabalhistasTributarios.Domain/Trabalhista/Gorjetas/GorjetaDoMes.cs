namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;

/// <summary>A gorjeta de um mês: o que a empresa reteve para os encargos e o que foi repassado ao empregado.</summary>
public sealed record GorjetaDoMes(MesGorjeta Mes, decimal Retencao, decimal Repasse);
