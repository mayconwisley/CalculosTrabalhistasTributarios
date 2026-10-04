using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Atualizacao;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Atualiza um valor por um índice mensal, do mês em que era devido até o anterior ao da atualização, como a Calculadora
/// do Cidadão do Banco Central, com juros simples e multa opcionais sobre o valor corrigido.
/// </summary>
public sealed class SimularCorrecaoValorUseCase(IIndicesEconomicos indices) : ISimularDemonstrativoUseCase<SimularCorrecaoValorRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularCorrecaoValorRequest r, CancellationToken cancellationToken)
    {
        if (r.Valor <= 0m)
            return Erro.Validacao("Informe o valor a corrigir.");
        if (r.MesFinal <= r.MesInicial)
            return Erro.Validacao("O mês da atualização deve ser posterior ao mês em que o valor era devido.");
        if (r.JurosMensais < 0m || r.Multa < 0m)
            return Erro.Validacao("Os juros e a multa não podem ser negativos.");

        var nome = NomeIndice(r.Indice);
        var ultimoMes = r.MesFinal.AddMonths(-1);
        var serie = await indices.ObterAsync(r.Indice, cancellationToken);
        var fatorAcumulado = SerieMensal.FatorComposto(serie, r.MesInicial, ultimoMes, nome);
        if (fatorAcumulado.Falhou)
            return fatorAcumulado.Erro;
        var fator = fatorAcumulado.Valor;
        var meses = SerieMensal.Quantidade(r.MesInicial, ultimoMes);

        var corrigido = CalculadoraTributacao.Arredondar(r.Valor * fator);
        var correcao = corrigido - r.Valor;
        var percentualJuros = r.JurosMensais * meses;
        var juros = CalculadoraTributacao.Arredondar(corrigido * percentualJuros / 100m);
        var multa = CalculadoraTributacao.Arredondar(corrigido * r.Multa / 100m);
        var total = corrigido + juros + multa;
        var variacao = (fator - 1m) * 100m;

        var formulas = new List<FormulaDto>
        {
            new("Período", $"{nome} de {Formato.Competencia(r.MesInicial)} a {Formato.Competencia(ultimoMes)}: {meses} {(meses == 1 ? "mês" : "meses")}"),
            new("Fator acumulado", $"Produto de (1 + variação do mês) em cada mês = {Math.Round(fator, 6).ToString("0.000000", Formato.Cultura)} ({Formato.Percentual(Math.Round(variacao, 4))})"),
            new("Valor corrigido", $"{Formato.Moeda(r.Valor)} x {Math.Round(fator, 6).ToString("0.000000", Formato.Cultura)} = {Formato.Moeda(corrigido)}")
        };
        if (r.JurosMensais > 0m)
            formulas.Add(new("Juros simples", $"{Formato.Percentual(r.JurosMensais)} x {meses} meses = {Formato.Percentual(percentualJuros)}; {Formato.Moeda(corrigido)} x {Formato.Percentual(percentualJuros)} = {Formato.Moeda(juros)}"));
        if (r.Multa > 0m)
            formulas.Add(new("Multa", $"{Formato.Moeda(corrigido)} x {Formato.Percentual(r.Multa)} = {Formato.Moeda(multa)}"));

        var proventos = new List<VerbaDto> { new("Valor original", Formato.Competencia(r.MesInicial), r.Valor), new($"Correção pelo {nome}", Formato.Percentual(Math.Round(variacao, 4)), correcao) };
        if (juros > 0m) proventos.Add(new("Juros simples", Formato.Percentual(percentualJuros), juros));
        if (multa > 0m) proventos.Add(new("Multa", Formato.Percentual(r.Multa), multa));

        var observacoes = new List<string>
        {
            $"A correção usa as variações do {nome} do mês em que o valor era devido até o mês anterior ao da atualização, como a Calculadora do Cidadão do Banco Central. Os índices precisam estar cadastrados na tabela {nome}; atualize-a pela internet se faltar algum mês.",
            "Os juros são simples, contados por mês cheio, e a multa incide sobre o valor corrigido. Para débitos de processos trabalhistas e cíveis, use a calculadora Débitos judiciais, que segue as fases definidas pelo STF e pela Lei 14.905/2024; para tributos federais, use Tributo em atraso."
        };
        if (fator < 1m)
            observacoes.Insert(0, $"O {nome} acumulado no período é negativo: houve deflação, e o valor corrigido ficou menor que o original.");

        return new DemonstrativoDto(
            "Correção de valores",
            $"{Formato.Competencia(r.MesInicial)} a {Formato.Competencia(r.MesFinal)} • {nome}",
            [
                new("Valor atualizado", Formato.Moeda(total), juros + multa > 0m ? "Com juros e multa" : "Só a correção"),
                new("Valor corrigido", Formato.Moeda(corrigido), $"{nome} de {Formato.Percentual(Math.Round(variacao, 4))}"),
                new("Juros", Formato.Moeda(juros), r.JurosMensais > 0m ? $"{Formato.Percentual(r.JurosMensais)} ao mês" : "Sem juros"),
                new("Multa", Formato.Moeda(multa), r.Multa > 0m ? Formato.Percentual(r.Multa) : "Sem multa")
            ],
            proventos,
            [],
            [],
            [new GrupoMemoriaDto("Atualização", $"Total: {Formato.Moeda(total)}", formulas)],
            observacoes,
            RotuloProventos: "Composição",
            RotuloResultado: "Valor atualizado");
    }

    public static string NomeIndice(IndiceEconomico indice) => indice switch
    {
        IndiceEconomico.Inpc => "INPC",
        IndiceEconomico.Ipca => "IPCA",
        IndiceEconomico.IpcaE => "IPCA-E",
        IndiceEconomico.Selic => "Selic",
        IndiceEconomico.Tr => "TR",
        _ => "taxa legal"
    };
}
