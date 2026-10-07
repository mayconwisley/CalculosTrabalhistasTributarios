namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Como a receita de 12 meses (RBT12) e a folha (FS12) são obtidas no período de apuração.</summary>
public enum RegraReceitaSimples
{
    /// <summary>Soma dos 12 meses da janela.</summary>
    DozeMeses,
    /// <summary>Até 2026, 1º mês de atividade: a receita do próprio mês × 12 (Resolução CGSN 140/2018, art. 22, § 2º).</summary>
    PrimeiroMes,
    /// <summary>Início de atividade: média dos meses de atividade da janela × 12 (art. 22, § 3º; a partir de 2027, § 2º, II).</summary>
    MediaInicio,
    /// <summary>A partir de 2027, 1º e 2º meses de atividade: alíquota da 1ª faixa e fator r de 0,28 (art. 22, § 2º, I; art. 26, § 6º).</summary>
    PrimeiraFaixa
}
