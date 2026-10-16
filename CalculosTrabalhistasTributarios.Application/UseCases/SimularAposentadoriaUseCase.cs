using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Atualizacao;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Aposentadoria;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Simulação de aposentadoria do RGPS: quando o segurado cumpre cada regra da EC 103/2019 e o benefício estimado, com o
/// salário mínimo e o teto da data da simulação. A média vem pronta ou é calculada dos salários de contribuição, corrigidos
/// pelo INPC da tabela de índices. O resultado é um comparativo das regras, sem proventos.
/// </summary>
public sealed class SimularAposentadoriaUseCase(ITributacaoConsulta tributacaoConsulta, IIndicesEconomicos indices) : ISimularDemonstrativoUseCase<SimularAposentadoriaRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularAposentadoriaRequest r, CancellationToken cancellationToken)
    {
        var tabelas = await tributacaoConsulta.ObterTabelasAsync(new DateOnly(r.DataSimulacao.Year, r.DataSimulacao.Month, 1), cancellationToken);
        if (tabelas.Falhou)
            return tabelas.Erro;
        var minimo = tabelas.Valor.ObterSalarioMinimo();
        if (minimo.Falhou)
            return minimo.Erro;

        ApuracaoMediaSalarios? media = null;
        if (r.Salarios is not null)
        {
            var calculada = CalculadoraMediaSalarios.Calcular(r.Salarios, r.DataSimulacao, await indices.ObterAsync(IndiceEconomico.Inpc, cancellationToken));
            if (calculada.Falhou)
                return calculada.Erro;
            if (calculada.Valor.Quantidade > r.MesesContribuicao)
                return Erro.Validacao($"Os períodos somam {Tempo(calculada.Valor.Quantidade)} de salários, mais que o tempo de contribuição informado ({Tempo(r.MesesContribuicao)}): revise o tempo ou os períodos.");
            media = calculada.Valor;
        }

        var resultado = SimuladorAposentadoria.Simular(new EntradaAposentadoria(r.Sexo, r.Nascimento, r.DataSimulacao, r.MesesContribuicao, r.MesesNaReforma,
            r.ContinuaContribuindo, media?.Media ?? r.Media, minimo.Valor, tabelas.Valor.TetoInss, r.ExpectativaSobrevida));
        if (resultado.Falhou)
            return resultado.Erro;
        return Montar(resultado.Valor, media);
    }

    private static DemonstrativoDto Montar(ApuracaoAposentadoria a, ApuracaoMediaSalarios? media)
    {
        var e = a.Entrada;
        var primeira = a.Primeira;
        var maior = a.MaiorValor;

        var destaques = new List<DestaqueDto>
        {
            primeira is { Data: { } data }
                ? new("Primeira aposentadoria", data <= e.DataSimulacao ? "Já cumpre" : Formato.Competencia(data), $"{Nome(primeira.Regra)}, aos {Tempo(primeira.IdadeMeses)}")
                : new("Primeira aposentadoria", "Não alcançada", e.ContinuaContribuindo ? $"Em até {SimuladorAposentadoria.HorizonteMeses / 12} anos de projeção" : "Sem novas contribuições"),
            new("Benefício na primeira", primeira?.Valor is { } valor ? Formato.Moeda(valor) : "Não calculado", primeira is null ? "Nenhuma regra cumprida" : Base(primeira)),
            new("Maior benefício", maior?.Valor is { } maiorValor ? Formato.Moeda(maiorValor) : "Não calculado", maior is { Data: { } dataMaior } ? $"{Nome(maior.Regra)}, em {Formato.Competencia(dataMaior)}" : "Nenhuma regra cumprida"),
            new("Tempo de contribuição hoje", Tempo(e.MesesContribuicao), $"Idade: {Tempo(a.IdadeAtualMeses)}")
        };

        var linhas = a.Regras.Select(regra => new LinhaComparativaDto(Nome(regra.Regra), regra switch
        {
            { Aplicavel: false } => ["Não se aplica", "", "", ""],
            { Data: { } data } => [data <= e.DataSimulacao ? "Já cumpre" : Formato.Competencia(data), Tempo(regra.IdadeMeses), Tempo(regra.TempoMeses), regra.Valor is { } v ? Formato.Moeda(v) : "Não calculado"],
            _ => ["Não alcança", "", "", ""]
        }, regra == primeira)).ToArray();

        var memoria = a.Regras.Select(regra =>
        {
            var formulas = new List<FormulaDto> { new("Requisitos", regra.Requisitos) };
            if (regra.Motivo is { } motivo)
                formulas.Add(new("Situação", motivo));
            else if (regra.Data is not { } data)
                formulas.Add(new("Situação", e.ContinuaContribuindo ? $"Requisitos não cumpridos em {SimuladorAposentadoria.HorizonteMeses / 12} anos de projeção ou até os 100 anos." : "Requisitos não cumpridos sem novas contribuições."));
            else
            {
                formulas.Add(new("Data", $"{Formato.Competencia(data)}: {Tempo(regra.IdadeMeses)} de idade e {Tempo(regra.TempoMeses)} de contribuição"));
                formulas.Add(new("Benefício", Calculo(regra, e)));
            }
            return new GrupoMemoriaDto(Nome(regra.Regra), regra.Valor is { } valor ? Formato.Moeda(valor) : regra.Aplicavel ? (regra.Data is null ? "Não alcança" : "Não calculado") : "Não se aplica", formulas);
        }).ToArray();

        var observacoes = new List<string>
        {
            "Projeção mês a mês a partir da data da simulação, " + (e.ContinuaContribuindo ? "supondo que o segurado continue contribuindo todos os meses." : "sem novas contribuições: só a idade avança.") + " Os valores estão em moeda de hoje, com o salário mínimo e o teto atuais; não há correção da média nem dos limites no futuro.",
            media is null
                ? "Média informada: todos os salários de contribuição desde 07/1994, corrigidos (EC 103/2019, art. 26). Confira a média e o tempo de contribuição no CNIS e no simulador do Meu INSS, que considera períodos e vínculos que esta simulação não vê."
                : $"Média calculada com {media.Quantidade} salários de contribuição desde {Formato.Competencia(media.Meses[0].Competencia)}, cada um corrigido pelo INPC{(media.CorrigidoAte is { } ate ? $" até {Formato.Competencia(ate)}" : "")} (EC 103/2019, art. 26; Lei 8.213/1991, art. 29-B). Os salários entram como informados, já limitados ao teto da época, como no CNIS; desde 11/2019, a competência abaixo do salário mínimo só conta se for complementada (EC 103/2019, art. 29). A exclusão das contribuições que reduzem a média (art. 26, § 6º) não é aplicada. Confira os salários e o tempo no CNIS e no simulador do Meu INSS.",
            "Pontos e idade mínima progressiva (arts. 15 e 16) e transição por idade (art. 18): 60% da média mais 2 pontos por ano completo acima de 20 anos de contribuição (homem) ou 15 anos (mulher). Pedágio de 100% (art. 20): 100% da média. Pedágio de 50% (art. 17): média × fator previdenciário, que depende da expectativa de sobrevida da tábua do IBGE do ano da aposentadoria.",
            "O benefício fica entre o salário mínimo e o teto do INSS. A carência de 180 contribuições, o tempo especial, a aposentadoria do professor, da pessoa com deficiência e por incapacidade não são simulados."
        };
        if (media?.FaltaInpcDesde is { } desde)
            observacoes.Insert(1, $"O INPC de {Formato.Competencia(desde)} em diante ainda não está na tabela de índices: os salários foram corrigidos só até o último mês publicado. Atualize a tabela INPC quando o IBGE divulgar o índice.");
        if (e.ExpectativaSobrevida is null && a.Regras.Any(regra => regra.Regra == RegraAposentadoria.Pedagio50 && regra.Alcancada))
            observacoes.Add("Informe a expectativa de sobrevida na idade da aposentadoria para calcular o fator previdenciário e o benefício do pedágio de 50%.");

        return new DemonstrativoDto("Simulação de aposentadoria", $"Simulação em {Formato.Data(e.DataSimulacao)} • {(e.Sexo == SexoSegurado.Feminino ? "mulher" : "homem")} • média de {Formato.Moeda(e.Media)}",
            destaques, [], [], [], media is null ? memoria : [MemoriaMedia(media), .. memoria], observacoes,
            Comparativo: new TabelaComparativaDto("Regras de aposentadoria", ["Cumpre em", "Idade", "Tempo de contribuição", "Benefício estimado"], linhas));
    }

    private static GrupoMemoriaDto MemoriaMedia(ApuracaoMediaSalarios m)
    {
        var formulas = m.Periodos.OrderBy(periodo => periodo.Inicio).Select(periodo =>
        {
            var meses = m.Meses.Where(mes => mes.Competencia >= SerieMensal.MesDe(periodo.Inicio) && mes.Competencia <= SerieMensal.MesDe(periodo.Fim)).ToArray();
            var nome = meses.Length == 1 ? Formato.Competencia(meses[0].Competencia) : $"{Formato.Competencia(meses[0].Competencia)} a {Formato.Competencia(meses[^1].Competencia)}";
            var fatores = meses[0].Fator == meses[^1].Fator ? $"fator {Fator(meses[0].Fator)}" : $"fatores de {Fator(meses[0].Fator)} a {Fator(meses[^1].Fator)}";
            return new FormulaDto(nome, $"{meses.Length} {(meses.Length == 1 ? "mês" : "meses")} de {Formato.Moeda(periodo.Salario)} x {fatores} = {Formato.Moeda(meses.Sum(mes => mes.Corrigido))}");
        }).ToList();
        formulas.Add(new("Correção", m.CorrigidoAte is { } ate
            ? $"Fator da competência: produto de (1 + INPC do mês) dela até {Formato.Competencia(ate)}; cada salário corrigido é arredondado aos centavos."
            : "Sem correção: os salários são de meses ainda sem INPC publicado."));
        formulas.Add(new("Média", $"{Formato.Moeda(m.Soma)} ÷ {m.Quantidade} competências = {Formato.Moeda(m.Media)}"));
        return new GrupoMemoriaDto("Média dos salários de contribuição", Formato.Moeda(m.Media), formulas);
    }

    private static string Fator(decimal fator) => Math.Round(fator, 6).ToString("0.000000", Formato.Cultura);

    private static string Calculo(ResultadoRegraAposentadoria regra, EntradaAposentadoria e)
    {
        var limite = regra.Valor is { } valor && (valor == e.SalarioMinimo || valor == e.Teto) ? $" (entre o mínimo de {Formato.Moeda(e.SalarioMinimo)} e o teto de {Formato.Moeda(e.Teto)})" : "";
        return regra switch
        {
            { Fator: { } fator } => $"{Formato.Moeda(e.Media)} x fator {fator.ToString("0.0000", Formato.Cultura)} = {Formato.Moeda(regra.Valor ?? 0m)}{limite}",
            { Coeficiente: { } coeficiente } => $"{Formato.Moeda(e.Media)} x {Formato.Percentual(coeficiente)} = {Formato.Moeda(regra.Valor ?? 0m)}{limite}",
            _ => "Não calculado: informe a expectativa de sobrevida para o fator previdenciário."
        };
    }

    private static string Base(ResultadoRegraAposentadoria regra) => regra switch
    {
        { Fator: { } fator } => $"Média x fator {fator.ToString("0.0000", Formato.Cultura)}",
        { Coeficiente: { } coeficiente } => $"{Formato.Percentual(coeficiente)} da média",
        _ => "Falta a expectativa de sobrevida"
    };

    private static string Tempo(int meses) => meses % 12 == 0 ? $"{meses / 12} anos" : $"{meses / 12} anos e {meses % 12} {(meses % 12 == 1 ? "mês" : "meses")}";

    private static string Nome(RegraAposentadoria regra) => regra switch
    {
        RegraAposentadoria.Pontos => "Pontos (art. 15)",
        RegraAposentadoria.IdadeProgressiva => "Idade mínima progressiva (art. 16)",
        RegraAposentadoria.Pedagio50 => "Pedágio de 50% (art. 17)",
        RegraAposentadoria.Pedagio100 => "Pedágio de 100% (art. 20)",
        RegraAposentadoria.IdadeTransicao => "Transição por idade (art. 18)",
        _ => "Regra permanente (art. 19)"
    };
}
