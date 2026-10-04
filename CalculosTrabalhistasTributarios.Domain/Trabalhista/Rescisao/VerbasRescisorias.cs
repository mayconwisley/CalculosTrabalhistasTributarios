namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>As verbas rescisórias do contrato, antes dos impostos e da pensão.</summary>
/// <param name="AnosCompletos">Anos completos de serviço, que definem o aviso proporcional.</param>
public sealed record VerbasRescisorias(
    ContratoRescindido Contrato,
    int AnosCompletos,
    SaldoDeSalario Saldo,
    AvisoPrevioRescisao Aviso,
    IndenizacoesRescisao Indenizacoes,
    DecimoTerceiroRescisao DecimoTerceiro,
    FeriasRescisao Ferias,
    FgtsRescisao Fgts);
