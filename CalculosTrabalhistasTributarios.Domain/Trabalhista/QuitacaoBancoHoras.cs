using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Parcelas apuradas no fechamento do banco, com os parâmetros usados para conferir o valor transferido.</summary>
public sealed record QuitacaoBancoHoras(DateOnly FimCiclo, SituacaoBancoHoras Situacao, decimal SalarioReferencia,
    decimal Divisor, IReadOnlyList<ParcelaQuitacaoBancoHoras> Parcelas)
{
    public decimal Total => Parcelas.Sum(parcela => parcela.Valor);

    public Result Validar(SituacaoBancoHoras situacaoEsperada, DateOnly dataDestino)
    {
        if (!Enum.IsDefined(Situacao) || Situacao == SituacaoBancoHoras.Acompanhamento)
            return Erro.Validacao("A situação da quitação transferida do banco de horas é inválida; recalcule o banco.");
        if (Situacao != situacaoEsperada)
            return Erro.Validacao($"A quitação do banco de horas foi apurada para {Situacao}; refaça o banco na situação {situacaoEsperada} antes de transferir.");
        if (situacaoEsperada == SituacaoBancoHoras.Fechamento
            ? FimCiclo.Year != dataDestino.Year || FimCiclo.Month != dataDestino.Month
            : FimCiclo != dataDestino)
            return Erro.Validacao("O fim do ciclo do banco de horas deve corresponder à competência do holerite ou à data de desligamento da rescisão.");
        if (SalarioReferencia <= 0m || SalarioReferencia > 1_000_000_000m || decimal.Round(SalarioReferencia, 2) != SalarioReferencia
            || Divisor < 1m || Divisor > 744m || Parcelas is null || Parcelas.Count is < 1 or > 500)
            return Erro.Validacao("A quitação transferida do banco de horas precisa de salário, divisor e de 1 a 500 parcelas válidas. Recalcule o banco.");
        var adicionais = new HashSet<decimal>();
        foreach (var parcela in Parcelas)
        {
            if (parcela is null || parcela.Adicional is < 50m or > 1000m || parcela.Minutos is < 1 or > 300_000
                || parcela.Valor <= 0m || decimal.Round(parcela.Valor, 2) != parcela.Valor
                || !adicionais.Add(parcela.Adicional))
                return Erro.Validacao("As parcelas transferidas do banco de horas devem ter adicional, minutos e valor válidos, sem adicional duplicado. Recalcule o banco.");
            var esperado = decimal.Round(SalarioReferencia * (1m + parcela.Adicional / 100m) * parcela.Minutos / (Divisor * 60m),
                2, MidpointRounding.AwayFromZero);
            if (parcela.Valor != esperado)
                return Erro.Validacao($"A parcela de {parcela.Adicional}% não confere com o salário, divisor e minutos do banco de horas. Recalcule o banco antes de transferir.");
        }
        if (Total > 1_000_000_000m)
            return Erro.Validacao("A quitação do banco de horas excede R$ 1.000.000.000,00; revise os lançamentos.");
        return Result.Ok();
    }
}
