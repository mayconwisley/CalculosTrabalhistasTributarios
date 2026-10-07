namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

/// <summary>Conferência dos depósitos do FGTS por competência, com os totais mensais e rescisórios separados.</summary>
public sealed record ApuracaoConferenciaFgts(CategoriaFgts Categoria, decimal Aliquota, IReadOnlyList<LinhaConferenciaFgts> Linhas, DateOnly? Desligamento)
{
    public IEnumerable<LinhaConferenciaFgts> Mensais => Linhas.Where(linha => linha.Lancamento.Tipo == TipoCompetenciaFgts.Mensal);
    public IEnumerable<LinhaConferenciaFgts> Rescisorias => Linhas.Where(linha => linha.Lancamento.Tipo == TipoCompetenciaFgts.Rescisoria);

    public decimal Devido(IEnumerable<LinhaConferenciaFgts> linhas) => linhas.Sum(linha => linha.Devido);
    public decimal Informado(IEnumerable<LinhaConferenciaFgts> linhas) => linhas.Sum(linha => linha.Lancamento.DepositoInformado);

    /// <summary>Soma do que falta depositar, sem abater os depósitos a maior de outras competências.</summary>
    public decimal Falta(IEnumerable<LinhaConferenciaFgts> linhas) => linhas.Where(linha => linha.Diferenca > 0m).Sum(linha => linha.Diferenca);

    /// <summary>Soma dos depósitos acima do devido.</summary>
    public decimal Excesso(IEnumerable<LinhaConferenciaFgts> linhas) => linhas.Where(linha => linha.Diferenca < 0m).Sum(linha => -linha.Diferenca);

    public decimal Compensatoria => Linhas.Sum(linha => linha.Compensatoria);
}
