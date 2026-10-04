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
        var calculo = SaqueAniversario.Calcular(r.Saldo);
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
                new("Valor do saque", Formato.Moeda(p.Valor), $"Faixa {p.Faixa} da tabela"),
                new("Saldo depois do saque", Formato.Moeda(restante), "Continua na conta"),
                new("Período para sacar", $"{mes} a {ultimoMes}", "Do 1º dia útil do mês do aniversário"),
                new("Multa de 40% na dispensa", Formato.Moeda(multaEstimada), "Sobre o saldo atual; ela continua sendo paga")
            ],
            [new("Saque-aniversário", $"{Formato.PercentualCurto(p.Aliquota)} + {Formato.Moeda(p.ParcelaAdicional)}", p.Valor)],
            [],
            [],
            [new GrupoMemoriaDto("Saque-aniversário", $"Saque: {Formato.Moeda(p.Valor)}", [
                new("Faixa", faixa),
                new("Valor", $"{Formato.Moeda(r.Saldo)} x {Formato.PercentualCurto(p.Aliquota)} + {Formato.Moeda(p.ParcelaAdicional)} = {Formato.Moeda(p.Valor)}"),
                new("Saldo restante", $"{Formato.Moeda(r.Saldo)} - {Formato.Moeda(p.Valor)} = {Formato.Moeda(restante)}")])],
            [
                "O saque-aniversário é opcional: quem adere saca todo ano uma parte do saldo, pela tabela da Lei 8.036/1990 (art. 20-D), a partir do 1º dia útil do mês do aniversário e por três meses.",
                "Quem está no saque-aniversário e é dispensado sem justa causa recebe a multa de 40%, mas não saca o saldo da conta. Voltar ao saque-rescisão pode ser pedido a qualquer momento, mas só vale a partir do 1º dia do 25º mês depois do pedido.",
                "O saldo considerado é a soma de todas as contas do FGTS, ativas e inativas, como no aplicativo FGTS. Valores dados em garantia de empréstimo ficam bloqueados e não são sacados."
            ],
            RotuloProventos: "Saque",
            RotuloResultado: "Valor do saque");
    }
}
