using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Faltas">Faltas injustificadas no período aquisitivo, que definem os dias de direito.</param>
/// <param name="DiasGozo">Dias de descanso; zero usa todos os dias de direito que não forem vendidos.</param>
/// <param name="Pensao">Pensão alimentícia sobre as férias + 1/3; nula quando não há.</param>
/// <param name="PrevidenciaComplementar">Contribuição do trabalhador à previdência complementar sobre as férias, deduzida por inteiro do IRRF.</param>
public sealed record SimularFeriasRequest(
    DateOnly Competencia,
    decimal Salario,
    decimal Medias,
    int Faltas,
    int DiasGozo,
    bool VenderAbono,
    bool AdiantarDecimoTerceiro,
    int Dependentes,
    RegraPensao? Pensao = null,
    decimal PrevidenciaComplementar = 0m);
