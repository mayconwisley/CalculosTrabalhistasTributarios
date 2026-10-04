namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>O aviso prévio da rescisão e a projeção que ele dá ao contrato.</summary>
/// <param name="Cumprimento">O aviso que vale para o motivo do desligamento.</param>
/// <param name="DiasProporcionais">30 dias mais 3 por ano completo, até 90 (Lei 12.506/2011).</param>
/// <param name="DiasIndenizados">Dias pagos; no acordo, a metade (CLT, art. 484-A, I, a).</param>
/// <param name="Desconto">O aviso não cumprido pelo empregado que pediu demissão (CLT, art. 487, § 2º).</param>
/// <param name="Projecao">Fim do contrato com os dias indenizados, que contam como tempo de serviço (CLT, art. 487, § 1º).</param>
public sealed record AvisoPrevioRescisao(
    CumprimentoAvisoPrevio Cumprimento,
    int DiasProporcionais,
    decimal DiasIndenizados,
    decimal Indenizado,
    decimal Desconto,
    DateOnly Projecao);
