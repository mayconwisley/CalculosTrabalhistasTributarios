namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Natureza da parcela do RRA: o 13º salário conta como um mês na tabela acumulada (IN RFB 1.500/2014, art. 37, § 1º).</summary>
public enum TipoParcelaRra { Mensal, DecimoTerceiro }
