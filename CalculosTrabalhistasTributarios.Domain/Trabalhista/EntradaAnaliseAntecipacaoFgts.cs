namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record EntradaAnaliseAntecipacaoFgts(DateOnly DataConsulta,
    ConfirmacaoAntecipacaoFgts AdesaoHa90Dias,
    ConfirmacaoAntecipacaoFgts ContratoDesdeNovembro2025,
    ConfirmacaoAntecipacaoFgts ProximoSaqueComprometido);
