namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>Indenizações e multas da rescisão, sem INSS, IRRF nem FGTS.</summary>
/// <param name="DiasRestantes">Dias que faltavam para o fim do contrato a prazo encerrado antes.</param>
/// <param name="Artigo479">Metade da remuneração desses dias, paga pelo empregador (CLT, art. 479).</param>
/// <param name="Limite480">O máximo que o empregado que saiu antes pode dever ao empregador (CLT, art. 480, § 1º).</param>
/// <param name="DataBase">A próxima data-base da categoria, quando informada na dispensa sem justa causa.</param>
/// <param name="Adicional">Um salário na dispensa nos 30 dias antes da data-base (Lei 7.238/1984, art. 9º).</param>
/// <param name="PrazoPagamento">Dez dias após o fim do contrato (CLT, art. 477, § 6º).</param>
/// <param name="MultaAtraso">Um salário quando as verbas são pagas depois do prazo (CLT, art. 477, § 8º).</param>
public sealed record IndenizacoesRescisao(
    int DiasRestantes,
    decimal Artigo479,
    decimal Limite480,
    DateOnly? DataBase,
    decimal Adicional,
    DateOnly PrazoPagamento,
    decimal MultaAtraso);
