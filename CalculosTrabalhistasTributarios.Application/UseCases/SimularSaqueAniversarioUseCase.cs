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
        var calculo = SaqueAniversario.Calcular(r.Saldo, r.ParcelaComprometida);
        if (calculo.Falhou)
            return calculo.Erro;
        var p = calculo.Valor;
        var restante = r.Saldo - p.Valor;
        var mes = Formato.Cultura.TextInfo.ToTitleCase(Formato.Cultura.DateTimeFormat.GetMonthName(r.MesAniversario));
        var ultimoMes = Formato.Cultura.DateTimeFormat.GetMonthName((r.MesAniversario + 1) % 12 + 1);
        var multaEstimada = CalculadoraTributacao.Arredondar(r.Saldo * 0.4m);

        var faixa = p.Faixa == 1
            ? $"Saldo de até {Formato.Moeda(500m)}: {Formato.PercentualCurto(p.Aliquota)}"
            : $"Saldo acima de {Formato.Moeda(p.AcimaDe)}: {Formato.PercentualCurto(p.Aliquota)} mais {Formato.Moeda(p.ParcelaAdicional)}";

        return new DemonstrativoDto(
            "Saque-aniversário do FGTS",
            $"Saldo de {Formato.Moeda(r.Saldo)} • aniversário em {mes.ToLower(Formato.Cultura)}",
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
                new("Valor", $"{Formato.Moeda(r.Saldo)} x {Formato.PercentualCurto(p.Aliquota)} + {Formato.Moeda(p.ParcelaAdicional)} = {Formato.Moeda(p.Valor)}"),
                new("Repasse ao banco", $"Parcela do saque simulado já cedida em empréstimos: {Formato.Moeda(p.ParcelaComprometida)}"),
                new("Disponível estimado", $"{Formato.Moeda(p.Valor)} - {Formato.Moeda(p.ParcelaComprometida)} = {Formato.Moeda(p.Disponivel)}"),
                new("Saldo restante", $"{Formato.Moeda(r.Saldo)} - {Formato.Moeda(p.Valor)} = {Formato.Moeda(restante)}")])],
            [
                "O saque-aniversário é opcional: quem adere saca todo ano uma parte do saldo, pela tabela da Lei 8.036/1990 (art. 20-D), a partir do 1º dia útil do mês do aniversário e por três meses.",
                "Na regra geral, quem está no saque-aniversário e é dispensado sem justa causa recebe a multa de 40%, mas não saca o saldo por rescisão. O retorno ao saque-rescisão exige ausência de antecipação contratada e só vale a partir do 1º dia do 25º mês depois do pedido.",
                "Use o saldo antes do saque simulado, somando as contas ativas e inativas, incluindo a garantia bloqueada e excluindo a multa rescisória. Saques já debitados não devem ser descontados novamente.",
                "A parcela comprometida é o valor deste saque cedido ao banco conforme contrato/extrato. O total recebido nos empréstimos, com juros descontados e vários anos antecipados, e o saldo bloqueado não permitem determinar sozinhos essa parcela. Não inclua parcelas de outros anos ou contratos já quitados.",
                "O saldo restante desconta o saque bruto, incluindo o repasse ao banco; pode continuar bloqueado por antecipações futuras. A simulação não apura crédito para novo empréstimo, outros bloqueios ou liberações excepcionais. Confirme a disponibilidade no aplicativo FGTS."
            ],
            RotuloProventos: "Saque bruto",
            RotuloResultado: "Disponível estimado");
    }
}
