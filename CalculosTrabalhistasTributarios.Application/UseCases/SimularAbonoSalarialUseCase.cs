using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Direito ao abono salarial do PIS/Pasep e o valor pelos meses trabalhados no ano-base.</summary>
public sealed class SimularAbonoSalarialUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularAbonoSalarialRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularAbonoSalarialRequest r, CancellationToken cancellationToken)
    {
        var anoPagamento = r.AnoBase + 2;
        if (r.AnoBase < 2017)
            return Erro.Validacao("Informe um ano-base a partir de 2017.");
        var limite = r.Limite > 0m ? r.Limite : AbonoSalarial.LimitePublicado(anoPagamento);
        if (limite is null)
            return Erro.Validacao($"O limite de renda do calendário de {anoPagamento} não está no aplicativo: informe-o no campo Limite de renda, como divulgado pelo Ministério do Trabalho.");

        // O valor usa o salário mínimo do ano do pagamento; se ele ainda não está cadastrado, vale o mais recente.
        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(new DateOnly(anoPagamento, 1, 1), cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var salarioMinimo = consultaTabelas.Valor.ObterSalarioMinimo();
        if (salarioMinimo.Falhou)
            return salarioMinimo.Erro;

        var calculo = AbonoSalarial.Calcular(r.Meses, r.RemuneracaoMedia, limite.Value, r.CadastradoHa5Anos, salarioMinimo.Valor);
        if (calculo.Falhou)
            return calculo.Erro;
        var a = calculo.Valor;

        var formulas = new List<FormulaDto>
        {
            new("Calendário", $"Ano-base {r.AnoBase}, pago em {anoPagamento}"),
            new("Limite de renda", $"Média mensal de até {Formato.Moeda(a.Limite)}{(r.Limite > 0m ? " (informado)" : "")}; informada: {Formato.Moeda(r.RemuneracaoMedia)}"),
            new("Valor", a.TemDireito
                ? $"{Formato.Moeda(a.SalarioMinimo)} ÷ 12 x {a.Meses} {(a.Meses == 1 ? "mês" : "meses")} = {Formato.Moeda(a.Valor)}"
                : $"Sem direito: {Formato.Lista(a.MotivosSemDireito)}.")
        };

        var observacoes = new List<string>
        {
            "Tem direito quem está cadastrado no PIS/Pasep há pelo menos 5 anos, trabalhou com carteira assinada pelo menos 30 dias no ano-base, teve a remuneração média até o limite e teve os dados informados pelo empregador no eSocial.",
            "Desde o calendário de 2026, o limite deixou de ser de dois salários mínimos e passou a ser corrigido só pela inflação (INPC), até chegar a um salário mínimo e meio (EC 135/2024). O de 2026 é de R$ 2.766,00; para outro ano, informe o limite divulgado.",
            "O PIS é pago pela Caixa, e o Pasep, pelo Banco do Brasil, conforme o calendário do Codefat pelo mês de nascimento. Consulte o valor na Carteira de Trabalho Digital."
        };
        if (anoPagamento > DateTime.Today.Year)
            observacoes.Insert(0, $"O salário mínimo de {anoPagamento} pode não estar cadastrado: o cálculo usa o mais recente da tabela, {Formato.Moeda(a.SalarioMinimo)}.");

        return new DemonstrativoDto(
            "Abono salarial (PIS/Pasep)",
            $"Ano-base {r.AnoBase} • pagamento em {anoPagamento}",
            [
                new("Valor do abono", Formato.Moeda(a.Valor), a.TemDireito ? $"{a.Meses}/12 do salário mínimo" : "Sem direito"),
                new("Direito", a.TemDireito ? "Sim" : "Não", a.TemDireito ? "Requisitos cumpridos" : Formato.Lista(a.MotivosSemDireito)),
                new("Limite de renda", Formato.Moeda(a.Limite), $"Calendário {anoPagamento}"),
                new("Salário mínimo", Formato.Moeda(a.SalarioMinimo), $"Base do valor em {anoPagamento}")
            ],
            a.TemDireito ? [new("Abono salarial", $"{a.Meses}/12", a.Valor)] : [],
            [],
            [],
            [new GrupoMemoriaDto("Abono salarial", a.TemDireito ? $"Valor: {Formato.Moeda(a.Valor)}" : "Sem direito", formulas)],
            observacoes,
            RotuloProventos: "Abono",
            RotuloResultado: "Valor do abono");
    }
}
