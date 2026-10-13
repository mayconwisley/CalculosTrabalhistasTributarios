namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Aposentadoria;

/// <summary>Todas as regras avaliadas, com a primeira aposentadoria possível e a de maior benefício estimado.</summary>
public sealed record ApuracaoAposentadoria(EntradaAposentadoria Entrada, int IdadeAtualMeses, IReadOnlyList<ResultadoRegraAposentadoria> Regras)
{
    public ResultadoRegraAposentadoria? Primeira => Regras.Where(regra => regra.Alcancada).OrderBy(regra => regra.Data).ThenByDescending(regra => regra.Valor ?? 0m).FirstOrDefault();

    public ResultadoRegraAposentadoria? MaiorValor => Regras.Where(regra => regra.Alcancada && regra.Valor is not null).OrderByDescending(regra => regra.Valor).ThenBy(regra => regra.Data).FirstOrDefault();
}
