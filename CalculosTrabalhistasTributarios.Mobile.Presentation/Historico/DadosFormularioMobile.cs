using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

/// <summary>Valores digitados, inclusive a ordem das linhas. O resultado é sempre apurado novamente.</summary>
public sealed record DadosFormularioMobile(
    int Versao,
    IReadOnlyDictionary<string, ValorCampoMobile> Campos,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>> Grades,
    IReadOnlyList<DepositoFgtsSalvoMobile>? DepositosFgts = null,
    IReadOnlyList<BeneficioSalvoMobile>? Beneficios = null,
    IReadOnlyList<LancamentoBancoSalvoMobile>? LancamentosBanco = null,
    QuitacaoBancoHoras? QuitacaoBancoHoras = null,
    bool QuitacaoConfirmada = false);
