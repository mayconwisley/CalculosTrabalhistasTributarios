using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="MesInicial">Mês em que o valor era devido: a correção começa nele.</param>
/// <param name="MesFinal">Mês da atualização: a correção vai até o mês anterior, como na Calculadora do Cidadão do Banco Central.</param>
/// <param name="JurosMensais">Juros simples ao mês, em %, sobre o valor corrigido; zero para só corrigir.</param>
/// <param name="Multa">Multa, em %, sobre o valor corrigido; zero quando não há.</param>
public sealed record SimularCorrecaoValorRequest(decimal Valor, DateOnly MesInicial, DateOnly MesFinal, IndiceEconomico Indice, decimal JurosMensais, decimal Multa);
