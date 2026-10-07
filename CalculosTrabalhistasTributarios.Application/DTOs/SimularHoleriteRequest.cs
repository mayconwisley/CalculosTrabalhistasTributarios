using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Holerite do mês: salário, adicionais, horas, faltas, descontos e benefícios em um só demonstrativo.</summary>
/// <param name="OutrosProventos">Outros proventos com INSS, IRRF e FGTS; comissões informadas aqui já devem incluir o DSR.</param>
/// <param name="Comissoes">Comissões apuradas no mês, separadas dos outros proventos para calcular o DSR automaticamente.</param>
/// <param name="Faltas">Dias de falta injustificada, descontados pelo salário-dia.</param>
/// <param name="DescansosPerdidos">Domingos e feriados perdidos pelas faltas: o da semana com falta injustificada (Lei 605/1949, art. 6º).</param>
/// <param name="HorasAtraso">Atrasos e saídas antecipadas, descontados pelo valor da hora.</param>
/// <param name="Dependentes">Dependentes para a dedução do IRRF.</param>
/// <param name="Filhos">Filhos ou equiparados para o salário-família.</param>
/// <param name="CustoValeTransporte">Custo das passagens do mês; o empregado paga até 6% do salário-base.</param>
/// <param name="Adiantamento">Adiantamento salarial (vale) pago no mês.</param>
/// <param name="OutrosDescontos">Consignado, plano de saúde e outros descontos que não reduzem o INSS, o IRRF nem o FGTS.</param>
/// <param name="Premios">Prêmios por desempenho (CLT, art. 457, § 4º): têm IRRF, mas não têm INSS nem FGTS.</param>
/// <param name="ProventosNaoTributaveis">Ajuda de custo, diárias de viagem e reembolsos: sem INSS, IRRF nem FGTS, só somam no líquido.</param>
/// <param name="PrevidenciaComplementar">Contribuição do trabalhador à previdência complementar ou ao Fapi, deduzida da base do IRRF.</param>
public sealed record SimularHoleriteRequest(
    DateOnly Competencia,
    decimal Salario,
    GrauInsalubridade Insalubridade,
    bool Periculosidade,
    decimal OutrosProventos,
    decimal Divisor,
    decimal HorasFaixa1,
    decimal PercentualFaixa1,
    decimal HorasFaixa2,
    decimal PercentualFaixa2,
    decimal HorasNoturnas,
    decimal PercentualNoturno,
    int Feriados,
    int Faltas,
    int DescansosPerdidos,
    decimal HorasAtraso,
    int Dependentes,
    int Filhos,
    RegraPensao? Pensao,
    decimal CustoValeTransporte,
    decimal Adiantamento,
    decimal OutrosDescontos,
    bool Rural = false,
    decimal HorasExtrasNoturnas = 0m,
    decimal Premios = 0m,
    decimal ProventosNaoTributaveis = 0m,
    decimal PrevidenciaComplementar = 0m,
    decimal Comissoes = 0m,
    bool ComissoesIncluemDsr = false,
    int? DiasUteisComissoes = null,
    int? DiasDescansoComissoes = null,
    decimal PisoGarantidoComissoes = 0m,
    decimal HorasIntervaloIntrajornada = 0m,
    decimal HorasIntervaloInterjornada = 0m);
