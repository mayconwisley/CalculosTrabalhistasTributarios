namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>O contrato encerrado e o que é preciso saber dele para apurar as verbas rescisórias.</summary>
/// <param name="Aviso">Como o aviso prévio foi cumprido; só vale na dispensa sem justa causa, no acordo e no pedido de demissão.</param>
/// <param name="PeriodosFeriasVencidas">Períodos aquisitivos completos sem férias gozadas, de 0 a 2.</param>
/// <param name="FaltasPeriodoAtual">Faltas injustificadas no período aquisitivo em curso, que definem os dias de férias.</param>
/// <param name="SaldoFgts">Saldo do extrato antes do mês do desligamento; zero para estimar pelo salário.</param>
/// <param name="FimPrevistoContrato">Fim do contrato a prazo, exigido na rescisão antecipada.</param>
/// <param name="MesDataBase">Mês da data-base da categoria, para a indenização adicional da Lei 7.238/1984.</param>
/// <param name="OutrosProventos">Horas extras, adicionais e comissões do mês do desligamento.</param>
/// <param name="FaltasNoMes">Faltas no mês do desligamento, descontadas do saldo de salário.</param>
public sealed record ContratoRescindido(
    DateOnly Admissao,
    DateOnly Desligamento,
    MotivoRescisao Motivo,
    CumprimentoAvisoPrevio Aviso,
    decimal Salario,
    decimal Medias,
    int PeriodosFeriasVencidas,
    int FaltasPeriodoAtual,
    decimal SaldoFgts,
    decimal AdiantamentoDecimoTerceiro,
    DateOnly? DataPagamento,
    DateOnly? FimPrevistoContrato,
    int? MesDataBase,
    decimal OutrosProventos,
    int FaltasNoMes)
{
    /// <summary>Salário e médias de variáveis, a base das verbas proporcionais e indenizadas.</summary>
    public decimal Remuneracao => Salario + Medias;

    public bool JustaCausa => Motivo == MotivoRescisao.DispensaPorJustaCausa;

    public bool Antecipada => Motivo is MotivoRescisao.RescisaoAntecipadaPeloEmpregador or MotivoRescisao.RescisaoAntecipadaPeloEmpregado;

    /// <summary>O mês do desligamento, que define o INSS e as tabelas da rescisão.</summary>
    public DateOnly CompetenciaDesligamento => new(Desligamento.Year, Desligamento.Month, 1);

    /// <summary>O aviso que vale para o motivo: nos demais motivos, não há aviso a pagar nem a descontar.</summary>
    public CumprimentoAvisoPrevio AvisoAplicavel =>
        Motivo is MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.Acordo or MotivoRescisao.PedidoDeDemissao ? Aviso : CumprimentoAvisoPrevio.TrabalhadoOuDispensado;
}
