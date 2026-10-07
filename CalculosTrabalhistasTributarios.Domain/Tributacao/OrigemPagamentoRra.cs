namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Quem paga o RRA. A parte do ano do pagamento segue a regra do pagador: tabela mensal na fonte pagadora e nas Justiças
/// do Trabalho e Estadual, e 3% sem deduções no precatório ou RPV da Justiça Federal (IN RFB 1.500/2014, arts. 25, 26 e 44).
/// </summary>
public enum OrigemPagamentoRra { FontePagadora, JusticaFederal }
