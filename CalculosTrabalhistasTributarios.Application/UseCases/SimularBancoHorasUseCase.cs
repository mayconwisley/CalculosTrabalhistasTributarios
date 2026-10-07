using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularBancoHorasUseCase : ISimularDemonstrativoUseCase<SimularBancoHorasRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularBancoHorasRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = CalculadoraBancoHoras.Calcular(request.Inicio, request.Fim, request.Regime, request.Situacao,
            request.Salario, request.Divisor, request.Adicional, request.Lancamentos);
        if (resultado.Falhou)
            return Task.FromResult<Result<DemonstrativoDto>>(resultado.Erro);
        var banco = resultado.Valor;
        var salarioInformado = request.Salario > 0m;
        var quitacaoApresentada = salarioInformado ? Formato.Moeda(banco.ValorQuitacao) : "Não calculada";
        var movimentos = banco.Movimentos.Select(item => new GrupoMemoriaDto(
            $"{item.Lancamento.Data:dd/MM/yyyy} — {(item.Lancamento.Tipo == TipoLancamentoBancoHoras.Credito ? "Crédito" : "Compensação")}",
            $"Saldo: {Horas(item.SaldoAposLancamento)}",
            [
                new("Movimento", Horas(item.Lancamento.Minutos)),
                new("Adicional", item.Lancamento.Tipo == TipoLancamentoBancoHoras.Credito
                    ? Formato.Percentual(item.Lancamento.Adicional ?? request.Adicional) : "Não se aplica à compensação"),
                new("Descrição", item.Lancamento.Descricao),
                new("Saldo após o movimento", Horas(item.SaldoAposLancamento))
            ])).ToArray();
        var observacoes = new List<string>
        {
            "O cálculo controla um único ciclo de compensação. O prazo máximo é o mesmo mês no ajuste individual tácito ou escrito, seis meses no acordo individual escrito e um ano em acordo ou convenção coletiva (CLT, art. 59).",
            "Os créditos lançados no mesmo dia são limitados a duas horas extras. Confira também a jornada total de até dez horas no dia, os descansos, a norma coletiva e as exceções aplicáveis; a grade registra apenas as horas destinadas ao banco.",
            "Saldo negativo representa horas a compensar, sem desconto salarial automático. Qualquer abatimento depende das regras do vínculo e do instrumento aplicável.",
            "A estimativa de quitação do saldo positivo usa a remuneração informada, o divisor e o adicional de horas extras. Não inclui DSR, reflexos, tributos ou outros adicionais. Na rescisão, confira a remuneração vigente nessa data (CLT, art. 59, § 3º).",
            "As compensações consomem os créditos mais antigos primeiro. A quitação soma cada grupo de créditos restantes pelo seu adicional e arredonda cada grupo aos centavos; confira o critério de compensação do instrumento aplicável."
        };
        if (request.Situacao == SituacaoBancoHoras.Acompanhamento)
            observacoes.Add("Modo acompanhamento: o valor de quitação é apenas uma referência e não entra no total a pagar.");
        if (request.Salario == 0m)
            observacoes.Add("Sem salário informado, o acompanhamento mostra apenas horas; a quitação estimada permanece zerada.");
        if (banco.SaldoDevedor > 0)
            observacoes.Add("As compensações superaram os créditos do ciclo. O demonstrativo não transforma esse saldo em desconto na folha ou na rescisão.");

        var demonstrativo = new DemonstrativoDto(
            "Banco de horas", $"Ciclo {request.Inicio:dd/MM/yyyy} a {request.Fim:dd/MM/yyyy}",
            [
                new("Saldo", Horas(banco.SaldoMinutos), banco.SaldoMinutos >= 0 ? "A favor do trabalhador" : "Horas a compensar"),
                new("Créditos", Horas(banco.MinutosCreditados), "Horas extras lançadas no banco"),
                new("Compensações", Horas(banco.MinutosCompensados), "Folgas ou reduções lançadas"),
                new("Quitação estimada", quitacaoApresentada, !salarioInformado ? "Informe o salário para estimar" :
                    request.Situacao == SituacaoBancoHoras.Acompanhamento ? "Referência, ainda não devida" : "Saldo positivo no fechamento")
            ],
            banco.ValorAPagar > 0m ? [new VerbaDto("Horas positivas não compensadas", $"{Horas(banco.SaldoCredor)} em {banco.ParcelasQuitacao.Count} faixa(s) de adicional", banco.ValorAPagar)] : [],
            [], [],
            [new("Conciliação e valor da hora", $"Saldo final: {Horas(banco.SaldoMinutos)}",
                [
                    new("Créditos - compensações", $"{Horas(banco.MinutosCreditados)} - {Horas(banco.MinutosCompensados)} = {Horas(banco.SaldoMinutos)}"),
                    new("Hora normal", salarioInformado
                        ? $"{Formato.Moeda(request.Salario)} ÷ {request.Divisor.ToString("N2", Formato.Cultura)} = {banco.ValorHora.ToString("C4", Formato.Cultura)}"
                        : "Não calculada: salário não informado"),
                    .. (salarioInformado ? banco.ParcelasQuitacao : []).Select(parcela => new FormulaDto($"Créditos restantes a {Formato.Percentual(parcela.Adicional)}",
                        $"{parcela.Minutos} minutos × {Formato.Moeda(request.Salario)} × (1 + {Formato.Percentual(parcela.Adicional)}) ÷ ({request.Divisor.ToString("N2", Formato.Cultura)} × 60) = {Formato.Moeda(parcela.Valor)}")),
                    new("Quitação estimada", salarioInformado
                        ? $"Soma das faixas arredondadas = {Formato.Moeda(banco.ValorQuitacao)}"
                        : "Não calculada: salário não informado")
                ]), .. movimentos],
            observacoes,
            RotuloProventos: "Quitação do saldo", RotuloResultado: "Total a pagar neste cenário",
            QuitacaoBancoHoras: request.Situacao != SituacaoBancoHoras.Acompanhamento && banco.ValorAPagar > 0m
                ? new QuitacaoBancoHoras(request.Fim, request.Situacao, request.Salario, request.Divisor, banco.ParcelasQuitacao)
                : null);
        return Task.FromResult<Result<DemonstrativoDto>>(demonstrativo);
    }

    private static string Horas(int minutos) => $"{(minutos < 0 ? "−" : "")}{Math.Abs(minutos) / 60}:{Math.Abs(minutos) % 60:00}";
}
