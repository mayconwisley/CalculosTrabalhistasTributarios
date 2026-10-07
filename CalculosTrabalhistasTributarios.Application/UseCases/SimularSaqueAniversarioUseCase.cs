using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Valor do saque-aniversário do FGTS e o que a opção muda numa dispensa sem justa causa.</summary>
public sealed class SimularSaqueAniversarioUseCase : ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>
{
    // O cálculo não consulta o banco: a tarefa só cumpre o contrato assíncrono da interface.
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularSaqueAniversarioRequest r, CancellationToken cancellationToken) => Task.FromResult(Simular(r));

    private static Result<DemonstrativoDto> Simular(SimularSaqueAniversarioRequest r)
    {
        if (r.MesAniversario is < 1 or > 12)
            return Erro.Validacao("Informe o mês de aniversário, de 1 a 12.");
        var composicao = SaqueAniversario.ComporSaldo(r.Saldo, r.GarantiaBloqueada, r.MultaRescisoriaIncluida, r.SaldoIncluiGarantia);
        if (composicao.Falhou)
            return composicao.Erro;
        var saldo = composicao.Valor;
        var calculo = SaqueAniversario.Calcular(saldo, r.ParcelaComprometida);
        if (calculo.Falhou)
            return calculo.Erro;
        var p = calculo.Valor;
        var restante = saldo - p.Valor;
        var mes = Formato.Cultura.TextInfo.ToTitleCase(Formato.Cultura.DateTimeFormat.GetMonthName(r.MesAniversario));
        var ultimoMes = Formato.Cultura.DateTimeFormat.GetMonthName((r.MesAniversario + 1) % 12 + 1);
        var multaEstimada = CalculadoraTributacao.Arredondar(saldo * 0.4m);

        var faixa = p.Faixa == 1
            ? $"Saldo de até {Formato.Moeda(500m)}: {Formato.PercentualCurto(p.Aliquota)}"
            : $"Saldo acima de {Formato.Moeda(p.AcimaDe)}: {Formato.PercentualCurto(p.Aliquota)} mais {Formato.Moeda(p.ParcelaAdicional)}";

        var demonstrativo = new DemonstrativoDto(
            "Saque-aniversário do FGTS",
            $"Base do FGTS de {Formato.Moeda(saldo)} • aniversário em {mes.ToLower(Formato.Cultura)}",
            [
                new("Disponível estimado", Formato.Moeda(p.Disponivel), "Após o repasse ao banco"),
                new("Saldo depois do saque", Formato.Moeda(restante), "Após saque bruto; pode conter garantias futuras"),
                new("Período para sacar", $"{mes} a {ultimoMes}", "Do 1º dia útil do mês do aniversário"),
                new("Multa de 40% na dispensa", Formato.Moeda(multaEstimada), "Sobre o saldo atual; ela continua sendo paga")
            ],
            [new("Saque-aniversário", $"{Formato.PercentualCurto(p.Aliquota)} + {Formato.Moeda(p.ParcelaAdicional)}", p.Valor)],
            p.ParcelaComprometida > 0m ? [new("Antecipação de empréstimos anteriores", "Cessão deste saque", p.ParcelaComprometida)] : [],
            [],
            [new GrupoMemoriaDto("Saque-aniversário", $"Disponível estimado: {Formato.Moeda(p.Disponivel)}", [
                new("Faixa", faixa),
                new("Composição do saldo", $"Saldo do extrato {Formato.Moeda(r.Saldo)} + garantia a somar {Formato.Moeda(r.SaldoIncluiGarantia ? 0m : r.GarantiaBloqueada)} - multa incluída {Formato.Moeda(r.MultaRescisoriaIncluida)} = {Formato.Moeda(saldo)}" +
                    (r.SaldoIncluiGarantia ? $". A garantia de {Formato.Moeda(r.GarantiaBloqueada)} já está no saldo e não foi somada novamente." : "")),
                new("Valor", $"{Formato.Moeda(saldo)} x {Formato.PercentualCurto(p.Aliquota)} + {Formato.Moeda(p.ParcelaAdicional)} = {Formato.Moeda(p.Valor)}"),
                new("Repasse ao banco", $"Parcela do saque simulado já cedida em empréstimos: {Formato.Moeda(p.ParcelaComprometida)}"),
                new("Disponível estimado", $"{Formato.Moeda(p.Valor)} - {Formato.Moeda(p.ParcelaComprometida)} = {Formato.Moeda(p.Disponivel)}"),
                new("Saldo restante", $"{Formato.Moeda(saldo)} - {Formato.Moeda(p.Valor)} = {Formato.Moeda(restante)}")])],
            [
                "O saque-aniversário é opcional: quem adere saca todo ano uma parte do saldo, pela tabela da Lei 8.036/1990 (art. 20-D), a partir do 1º dia útil do mês do aniversário e por três meses.",
                "Na regra geral, quem está no saque-aniversário e é dispensado sem justa causa recebe a multa de 40%, mas não saca o saldo por rescisão. O retorno ao saque-rescisão exige ausência de antecipação contratada e só vale a partir do 1º dia do 25º mês depois do pedido.",
                "A base usa o saldo do extrato, soma a garantia somente quando ela ainda não estiver incluída e exclui a multa informada como incluída nesse saldo. Saques já debitados não devem ser descontados novamente.",
                "A parcela comprometida é o valor deste saque cedido ao banco conforme contrato/extrato. O total recebido nos empréstimos, com juros descontados e vários anos antecipados, e o saldo bloqueado não permitem determinar sozinhos essa parcela. Não inclua parcelas de outros anos ou contratos já quitados.",
                "O saldo restante desconta o saque bruto, incluindo o repasse ao banco; pode continuar bloqueado por antecipações futuras. A simulação não apura crédito para novo empréstimo, outros bloqueios ou liberações excepcionais. Confirme a disponibilidade no aplicativo FGTS."
            ],
            RotuloProventos: "Saque bruto",
            RotuloResultado: "Disponível estimado");

        if (r.AnaliseAntecipacao is null)
            return demonstrativo;
        var resultadoAnalise = AnalisadorAntecipacaoFgts.Analisar(saldo, r.GarantiaBloqueada, r.MesAniversario, r.AnaliseAntecipacao);
        if (resultadoAnalise.Falhou)
            return resultadoAnalise.Erro;
        var analise = resultadoAnalise.Valor;
        return demonstrativo with
        {
            Destaques = [
                new("Nova antecipação", analise.Situacao, $"Análise em {Formato.Data(r.AnaliseAntecipacao.DataConsulta)}"),
                new("Saldo fora da garantia", Formato.Moeda(analise.SaldoForaGarantia), "Não é limite de novo empréstimo"),
                new("Próximo aniversário", analise.ProximoAniversario.ToString("MM/yyyy"), "Confirme a situação dessa competência com o banco"),
                new("Saque anual após parcela informada", Formato.Moeda(p.Disponivel), "Zero no repasse não confirma parcela livre"),
                .. demonstrativo.Destaques.Skip(1).Take(2)
            ],
            Informativos = [new("Saldo fora da garantia informada", "Base menos bloqueio; não é crédito liberado", analise.SaldoForaGarantia)],
            Memoria = [.. demonstrativo.Memoria.Select(grupo => grupo with
                { Destaque = $"Saque anual após parcela informada: {Formato.Moeda(p.Disponivel)}", Formulas = grupo.Formulas.Select(formula =>
                    formula.Titulo == "Disponível estimado" ? formula with { Titulo = "Saque anual após parcela informada" } : formula).ToArray() }),
                new GrupoMemoriaDto("Análise de nova antecipação", analise.Situacao, [
                new("Saldo fora da garantia", $"{Formato.Moeda(saldo)} - {Formato.Moeda(r.GarantiaBloqueada)} = {Formato.Moeda(analise.SaldoForaGarantia)}; não é limite de empréstimo"),
                new("Janela de contratação", $"Em {Formato.Data(r.AnaliseAntecipacao.DataConsulta)}: até {analise.LimiteSaquesAnuais} saques anuais, R$ 100 a R$ 500 por saque; próximo aniversário de referência {analise.ProximoAniversario:MM/yyyy}"),
                .. analise.Motivos.Select((motivo, indice) => new FormulaDto($"Critério {indice + 1}", motivo))
            ])],
            Observacoes = [.. demonstrativo.Observacoes.Where(texto => !texto.StartsWith("O saldo restante", StringComparison.Ordinal)), .. analise.Motivos],
            RotuloResultado = "Saque anual após parcela informada"
        };
    }
}
