namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Tipo">Janela do cálculo, como "Pensao" ou "Calculadora.Rescisao", que sabe ler os <paramref name="Dados"/>.</param>
/// <param name="Calculadora">Nome da calculadora exibido no histórico, como "Rescisão".</param>
/// <param name="Nome">Nome dado pelo usuário, por exemplo o do empregado ou o número do processo.</param>
/// <param name="Dados">Valores do formulário em JSON, como estavam ao salvar.</param>
public sealed record CalculoSalvoDto(long Id, string Tipo, string Calculadora, string Nome, string Dados, DateTime CriadoEm, DateTime AlteradoEm);
