using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Distribui o teto na ordem informada; a progressividade considera apenas os vínculos de empregado, doméstico e avulso.</summary>
public static class CalculadoraMultiplosVinculosInss
{
    public static Result<ApuracaoMultiplosVinculosInss> Calcular(TabelasDaCompetencia tabelas, IReadOnlyList<VinculoInss> vinculos)
    {
        if (tabelas.Competencia < new DateOnly(2020, 3, 1))
            return Erro.Validacao("A apuração de múltiplos vínculos está disponível a partir de 03/2020, quando começou a tabela progressiva do segurado empregado.");
        if (vinculos is null || vinculos.Count is < 2 or > 50)
            return Erro.Validacao("Informe de 2 a 50 vínculos da mesma competência, na ordem de desconto.");
        if (vinculos.Any(vinculo => vinculo is null || string.IsNullOrWhiteSpace(vinculo.Identificacao) || vinculo.Remuneracao <= 0m || !Enum.IsDefined(vinculo.Tipo)))
            return Erro.Validacao("Cada vínculo precisa de identificação, categoria válida e remuneração maior que zero.");
        if (vinculos.Any(vinculo => decimal.Round(vinculo.Remuneracao, 2) != vinculo.Remuneracao))
            return Erro.Validacao("Informe as remunerações em reais e centavos.");
        if (vinculos.Any(vinculo => vinculo.Remuneracao > 1_000_000_000m || vinculo.Identificacao.Length > 120))
            return Erro.Validacao("Cada remuneração deve ser de até R$ 1 bilhão e cada identificação deve ter no máximo 120 caracteres.");

        var resultado = new List<ContribuicaoVinculoInss>(vinculos.Count);
        var baseNoTeto = 0m;
        var baseProgressiva = 0m;
        foreach (var vinculo in vinculos)
        {
            var baseTributada = Math.Min(vinculo.Remuneracao, tabelas.TetoInss - baseNoTeto);
            var progressivo = vinculo.Tipo is TipoVinculoInss.Empregado or TipoVinculoInss.Domestico or TipoVinculoInss.Avulso;
            var faixas = progressivo && baseTributada > 0m
                ? tabelas.CalcularFaixasInssEntre(baseProgressiva, baseProgressiva + baseTributada)
                : [];
            var contribuicao = progressivo
                ? faixas.Sum(faixa => faixa.Imposto)
                : CalculadoraTributacao.Truncar(baseTributada * (vinculo.Tipo == TipoVinculoInss.ContribuinteIndividualEbas ? .20m : .11m));
            resultado.Add(new ContribuicaoVinculoInss(vinculo, baseNoTeto, baseProgressiva, baseTributada, contribuicao, faixas));
            baseNoTeto += baseTributada;
            if (progressivo) baseProgressiva += baseTributada;
        }

        return new ApuracaoMultiplosVinculosInss(resultado, tabelas.TetoInss);
    }
}
