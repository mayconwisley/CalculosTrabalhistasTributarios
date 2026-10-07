namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Atividade que define o anexo (Resolução CGSN 140/2018, art. 25, § 1º). Nas atividades sujeitas ao fator r, o anexo é
/// o III quando a folha dos 12 meses alcança 28% da receita e o V quando fica abaixo (LC 123/2006, art. 18, §§ 5º-J e 5º-M).
/// </summary>
public enum AtividadeSimples { Comercio, Industria, ServicosAnexoIII, ServicosAnexoIV, ServicosFatorR }
