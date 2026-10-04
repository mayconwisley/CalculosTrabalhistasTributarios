namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Número com o formato de exibição do Excel; o separador decimal segue o idioma do Excel de quem abre.</summary>
internal sealed record Celula(decimal Valor, string Formato);
