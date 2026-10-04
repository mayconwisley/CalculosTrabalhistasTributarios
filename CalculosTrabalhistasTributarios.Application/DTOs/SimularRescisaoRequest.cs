using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Desligamento">Último dia de trabalho; com aviso trabalhado, o fim do aviso.</param>
/// <param name="FaltasPeriodoAtual">Faltas injustificadas no período aquisitivo em curso, que reduzem as férias proporcionais.</param>
/// <param name="SaldoFgts">Saldo da conta do FGTS para fins rescisórios; zero estima pelo salário atual.</param>
/// <param name="AdiantamentoDecimoTerceiro">1ª parcela do 13º já paga no ano, descontada na rescisão.</param>
/// <param name="Pensao">Pensão alimentícia sobre as verbas salariais da rescisão; nula quando não há.</param>
/// <param name="DataPagamento">Data do pagamento das verbas, que define a tabela do IRRF e, depois de 10 dias do fim do contrato, a multa do art. 477, § 8º; nula quando é paga no prazo, no mês do desligamento.</param>
/// <param name="FimPrevistoContrato">Fim previsto do contrato a prazo, nas rescisões antecipadas.</param>
/// <param name="MesDataBase">Mês da data-base da categoria (1 a 12), para a indenização adicional da Lei 7.238/1984; nulo quando não informado.</param>
/// <param name="OutrosProventos">Horas extras, adicionais e comissões do mês do desligamento, com INSS, IRRF e FGTS.</param>
/// <param name="FaltasNoMes">Faltas injustificadas no mês do desligamento, descontadas do saldo de salário.</param>
public sealed record SimularRescisaoRequest(
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
    int Dependentes,
    RegraPensao? Pensao = null,
    DateOnly? DataPagamento = null,
    DateOnly? FimPrevistoContrato = null,
    int? MesDataBase = null,
    decimal OutrosProventos = 0m,
    int FaltasNoMes = 0);
