namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <param name="Provento">Valor formatado na coluna de proventos; vazio nas linhas de desconto.</param>
public sealed record LinhaDemonstrativoViewModel(string Descricao, string Referencia, string Provento, string Desconto);
