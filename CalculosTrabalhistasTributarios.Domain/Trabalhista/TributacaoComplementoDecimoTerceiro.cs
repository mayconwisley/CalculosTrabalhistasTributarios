namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// IRRF do complemento do 13º pago no ano seguinte. O eSocial informa a diferença de 13º de ano anterior como RRA de um
/// mês (Manual de Orientação do eSocial; IN RFB 1.500/2014, art. 37, § 1º). A IN RFB 1.500/2014, art. 13, § 3º, manda
/// recalcular o 13º total pela tabela do mês de quitação e deduzir o imposto já retido.
/// </summary>
public enum TributacaoComplementoDecimoTerceiro { Rra, Recalculo }
