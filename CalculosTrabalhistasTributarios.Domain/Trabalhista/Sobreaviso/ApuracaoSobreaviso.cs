using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;

/// <summary>As horas de sobreaviso e prontidão de cada mês e os reflexos da soma, apurados pelo motor de reflexos.</summary>
public sealed record ApuracaoSobreaviso(EntradaSobreaviso Entrada, IReadOnlyList<SobreavisoDoMes> Meses, ApuracaoReflexos Reflexos)
{
    public decimal TotalSobreaviso => Meses.Sum(mes => mes.ValorSobreaviso);
    public decimal TotalProntidao => Meses.Sum(mes => mes.ValorProntidao);
    public decimal HorasSobreaviso => Meses.Sum(mes => mes.Mes.HorasSobreaviso);
    public decimal HorasProntidao => Meses.Sum(mes => mes.Mes.HorasProntidao);
}
