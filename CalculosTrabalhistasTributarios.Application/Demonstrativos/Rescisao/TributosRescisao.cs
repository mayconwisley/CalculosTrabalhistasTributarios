using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao;

/// <summary>INSS, IRRF e pensão das verbas da rescisão que os têm: as do mês e o 13º, cada um calculado à parte.</summary>
/// <param name="CompetenciaIrrf">Mês do pagamento, que define a tabela do IRRF (regime de caixa).</param>
/// <param name="Pensao">A regra da pensão, nula quando não há.</param>
internal sealed record TributosRescisao(
    DateOnly CompetenciaIrrf,
    ApuracaoInss InssSaldo,
    ApuracaoInss Inss13,
    ApuracaoIrrf IrrfSaldo,
    ApuracaoIrrf Irrf13,
    RegraPensao? Pensao,
    PensaoApurada<ApuracaoIrrf>? PensaoSaldo,
    PensaoApurada<ApuracaoIrrf>? Pensao13);
