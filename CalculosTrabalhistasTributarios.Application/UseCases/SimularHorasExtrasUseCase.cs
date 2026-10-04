using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Salário do mês com horas extras, adicional noturno e o reflexo das duas no descanso semanal remunerado (DSR),
/// com o INSS e o IRRF da remuneração total.
/// </summary>
public sealed class SimularHorasExtrasUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularHorasExtrasRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularHorasExtrasRequest request, CancellationToken cancellationToken)
    {
        if (request.Salario < 0m || request.AdicionaisSalariais < 0m || request.Dependentes < 0)
            return Erro.Validacao("Os valores, as horas e a quantidade de dependentes não podem ser negativos.");
        var informadas = new HorasInformadas(request.HorasFaixa1, request.PercentualFaixa1, request.HorasFaixa2, request.PercentualFaixa2,
            request.HorasNoturnas, request.PercentualNoturno, request.HorasExtrasNoturnas, request.Feriados, request.Rural);
        if (CalculadoraHoras.Validar(informadas, request.Divisor) is { Falhou: true } horasInvalidas)
            return horasInvalidas.Erro;

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var baseHora = request.Salario + request.AdicionaisSalariais;
        var calculoHoras = CalculadoraHoras.Calcular(request.Competencia, baseHora, request.Divisor, informadas);
        if (calculoHoras.Falhou)
            return calculoHoras.Erro;
        var horas = calculoHoras.Valor;
        var variaveis = horas.Variaveis;
        var dsr = horas.Dsr;
        var valorHora = horas.ValorHora;
        var remuneracao = baseHora + variaveis + dsr;

        var inss = tabelas.CalcularInss(remuneracao);
        var irrf = tabelas.CalcularIrrf(remuneracao, inss.Valor, request.Dependentes);
        var fgts = CalculadoraTributacao.Arredondar(remuneracao * .08m);
        var liquido = remuneracao - inss.Valor - irrf.Imposto;

        var proventos = new List<VerbaDto> { new("Salário", "", request.Salario) };
        if (request.AdicionaisSalariais > 0m) proventos.Add(new("Adicionais salariais (insalubridade, periculosidade)", "", request.AdicionaisSalariais));
        proventos.AddRange(DemonstrativoHoras.Proventos(horas));

        var valorHoraTexto = request.AdicionaisSalariais > 0m
            ? $"({Formato.Moeda(request.Salario)} + {Formato.Moeda(request.AdicionaisSalariais)} de adicionais) ÷ {Formato.Numero(request.Divisor)} horas = {valorHora.ToString("C4", Formato.Cultura)} por hora"
            : $"{Formato.Moeda(request.Salario)} ÷ {Formato.Numero(request.Divisor)} horas = {valorHora.ToString("C4", Formato.Cultura)} por hora";
        var formulas = new List<FormulaDto> { new("Valor da hora normal", valorHoraTexto) };
        formulas.AddRange(DemonstrativoHoras.Formulas(horas));
        formulas.Add(new("Remuneração do mês", $"{string.Join(" + ", proventos.Select(verba => Formato.Moeda(verba.Valor)))} = {Formato.Moeda(remuneracao)}"));

        return new DemonstrativoDto(
            "Horas extras e adicionais",
            $"Competência {Formato.Competencia(tabelas.Competencia)}",
            [
                new("Horas extras e adicionais", Formato.Moeda(variaveis + dsr), "Incluindo o reflexo no DSR"),
                new("Remuneração do mês", Formato.Moeda(remuneracao), request.AdicionaisSalariais > 0m ? $"Salário e adicionais de {Formato.Moeda(baseHora)}" : $"Salário de {Formato.Moeda(request.Salario)}"),
                new("Salário líquido", Formato.Moeda(liquido), "Remuneração menos INSS e IRRF"),
                new("Valor da hora", Formato.Moeda(valorHora), $"Divisor {Formato.Numero(request.Divisor)}")
            ],
            proventos,
            [
                new("INSS", "", inss.Valor),
                new(MemoriaTributaria.DescricaoIrrf("IRRF", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto)
            ],
            [new("FGTS", "8%", fgts)],
            [
                new GrupoMemoriaDto("Horas extras, adicional noturno e DSR", $"Remuneração: {Formato.Moeda(remuneracao)}", formulas),
                MemoriaTributaria.Inss("INSS", inss, "remuneração do mês"),
                MemoriaTributaria.Irrf("IRRF", irrf, "remuneração do mês")
            ],
            [
                "O divisor 220 corresponde a 44 horas semanais; use 200 para 40 horas, 180 para 36 e 150 para 30.",
                request.Rural
                    ? "No trabalho rural, a noite vai das 21h às 5h na lavoura e das 20h às 4h na pecuária, com adicional de pelo menos 25% e sem hora reduzida: as horas de relógio informadas são as pagas (Lei 5.889/1973, art. 7º)."
                    : "A hora noturna urbana, das 22h às 5h e na prorrogação depois das 5h (Súmula 60 do TST), tem 52 minutos e 30 segundos (CLT, art. 73); as horas de relógio informadas foram convertidas. No trabalho rural, escolha a opção rural: o adicional é de 25% e não há hora reduzida.",
                "O DSR considera os domingos do mês e os feriados informados. Convenções coletivas podem prever adicionais maiores."
            ]);
    }
}
