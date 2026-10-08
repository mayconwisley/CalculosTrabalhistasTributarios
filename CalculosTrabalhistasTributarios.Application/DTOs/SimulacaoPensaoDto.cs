using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ImpostoSemPensao">IRRF de quem paga se não houvesse a pensão, na modalidade de menor imposto; a diferença para o IRRF com a pensão é a economia que a dedução traz.</param>
/// <param name="SimplificadoDisponivel">Falso antes de 05/2023, quando o desconto simplificado mensal não existia.</param>
public sealed record SimulacaoPensaoDto(decimal ValorInss, ModalidadePensaoDto Normal, ModalidadePensaoDto Simplificada, string MensagemVantagem, decimal ImpostoSemPensao, bool SimplificadoDisponivel = true)
{
    /// <summary>A modalidade que a fonte pagadora aplica: a de menor IRRF; no empate, a normal.</summary>
    public ModalidadePensaoDto Aplicada => SimplificadoDisponivel && Simplificada.Imposto < Normal.Imposto ? Simplificada : Normal;

    /// <summary>As modalidades que existem na competência, para o comparativo.</summary>
    public IReadOnlyList<ModalidadePensaoDto> Modalidades => SimplificadoDisponivel ? [Normal, Simplificada] : [Normal];

    /// <summary>Quanto a dedução da pensão reduz o IRRF de quem paga; zero quando não reduz.</summary>
    public decimal EconomiaIrrf => Math.Max(0m, ImpostoSemPensao - Aplicada.Imposto);

    /// <summary>Contribuição de cada faixa do INSS; a última é a faixa em que a base termina. Vazia sem base de INSS.</summary>
    public IReadOnlyList<ResultadoFaixaTributaria> FaixasInss { get; init; } = [];

    /// <summary>Falso antes de 03/2020, quando a alíquota da faixa do INSS incidia sobre toda a base.</summary>
    public bool InssProgressivo { get; init; } = true;

    /// <summary>Alíquota da faixa do IRRF sem a pensão, na modalidade de <see cref="ImpostoSemPensao"/>.</summary>
    public decimal AliquotaIrrfSemPensao { get; init; }
}
