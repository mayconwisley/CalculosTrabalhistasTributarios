using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Pensão por morte do RGPS: valor, cota por dependente, duração para o cônjuge e o redutor da acumulação.</summary>
public sealed class SimularPensaoMorteUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularPensaoMorteRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularPensaoMorteRequest r, CancellationToken cancellationToken)
    {
        var tabelas = await tributacaoConsulta.ObterTabelasAsync(new DateOnly(r.DataObito.Year, r.DataObito.Month, 1), cancellationToken);
        if (tabelas.Falhou)
            return tabelas.Erro;
        var minimo = tabelas.Valor.ObterSalarioMinimo();
        if (minimo.Falhou)
            return minimo.Erro;

        var resultado = CalculadoraPensaoMorte.Calcular(new EntradaPensaoMorte(r.DataObito, r.Situacao, r.ValorAposentadoria, r.Media, r.SexoInstituidor,
            r.MesesContribuicao, r.Causa, r.HaConjuge, r.IdadeConjuge, r.UniaoDoisAnos, r.OutrosDependentes, r.DependenteInvalido, r.BeneficioConjuge,
            minimo.Valor, tabelas.Valor.TetoInss));
        if (resultado.Falhou)
            return resultado.Erro;
        return Montar(resultado.Valor);
    }

    private static DemonstrativoDto Montar(ApuracaoPensaoMorte p)
    {
        var e = p.Entrada;
        var aposentado = e.Situacao == SituacaoInstituidor.Aposentado;
        var cota = Formato.PercentualCurto(p.Cota);
        var dependentes = p.Dependentes == 1 ? "1 dependente" : $"{p.Dependentes} dependentes";

        var destaques = new List<DestaqueDto>
        {
            new("Pensão mensal", Formato.Moeda(p.Pensao), p.ElevadaAoMinimo ? "Elevada ao salário mínimo" : $"{cota} de {Formato.Moeda(p.Base)}"),
            new("Cota familiar", cota, e.DependenteInvalido ? "100% com dependente inválido ou com deficiência" : $"50% + 10% x {dependentes}"),
            new("Por dependente", Formato.Moeda(p.CotaPorDependente), $"Partes iguais entre {dependentes}"),
            new("Duração para o cônjuge", Duracao(p), p.FimConjuge is { } fim ? $"Até {Formato.Data(fim)}" : p.Vitalicia ? $"Cônjuge com {e.IdadeConjuge} anos no óbito" : "Sem cônjuge ou companheiro")
        };

        var proventos = new List<VerbaDto> { new($"Pensão por morte (cota familiar de {cota})", dependentes, p.Pensao) };
        var informativos = new List<VerbaDto>
        {
            new(aposentado ? "Aposentadoria do segurado" : "Aposentadoria por incapacidade que serviria de base", aposentado ? "Recebida" : $"{Formato.Percentual(p.CoeficienteBase ?? 0m)} da média", p.Base),
            new("Cota de cada dependente", dependentes, p.CotaPorDependente)
        };
        if (p.ValorReduzido is { } reduzido && p.TotalConjuge is { } total)
        {
            informativos.Add(new(p.ReduzidoPensao ? "Cota da pensão do cônjuge com o redutor" : "Benefício próprio do cônjuge com o redutor", "Art. 24 da EC 103/2019", reduzido));
            informativos.Add(new("Total do cônjuge com a acumulação", "Com o redutor", total));
        }

        var formulas = new List<FormulaDto>
        {
            aposentado
                ? new("Base", $"Aposentadoria recebida: {Formato.Moeda(p.Base)}")
                : new("Base", $"Aposentadoria por incapacidade permanente: {Formato.Moeda(e.Media)} x {Formato.Percentual(p.CoeficienteBase ?? 0m)} = {Formato.Moeda(p.Base)}{(e.Causa == CausaObito.AcidenteTrabalho ? " (100% por acidente do trabalho ou doença profissional)" : $" (60% mais 2 pontos por ano acima de {(e.SexoInstituidor == Domain.Previdencia.SexoSegurado.Feminino ? 15 : 20)} anos)")}"),
            new("Cota familiar", e.DependenteInvalido ? "100%, por haver dependente inválido ou com deficiência" : $"50% + 10% x {p.Dependentes} = {cota}"),
            new("Pensão", $"{Formato.Moeda(p.Base)} x {cota} = {Formato.Moeda(p.Calculada)}{(p.ElevadaAoMinimo ? $", elevada ao salário mínimo de {Formato.Moeda(e.SalarioMinimo)}" : "")}"),
            new("Por dependente", $"{Formato.Moeda(p.Pensao)} ÷ {p.Dependentes} = {Formato.Moeda(p.CotaPorDependente)}")
        };
        if (e.HaConjuge)
            formulas.Add(new("Duração para o cônjuge", DuracaoExplicada(p)));
        if (p.ValorReduzido is { } valorReduzido)
        {
            var menor = p.ReduzidoPensao ? p.CotaPorDependente : e.BeneficioConjuge;
            formulas.Add(new("Redutor da acumulação", $"{Formato.Moeda(menor)} {(p.ReduzidoPensao ? "(cota da pensão)" : "(benefício próprio)")}: inteiro até 1 salário mínimo, 60% de 1 a 2, 40% de 2 a 3, 20% de 3 a 4 e 10% acima de 4 = {Formato.Moeda(valorReduzido)}"));
        }

        var observacoes = new List<string>
        {
            "Óbitos desde 13/11/2019: cota familiar de 50% da aposentadoria do segurado, ou da aposentadoria por incapacidade permanente a que teria direito, mais 10 pontos por dependente, até 100%; com dependente inválido ou com deficiência intelectual, mental ou grave, 100% (EC 103/2019, art. 23). A pensão não fica abaixo do salário mínimo.",
            "As cotas são iguais e, quando um dependente perde essa condição, a cota dele deixa de ser paga e não passa aos demais. Os filhos recebem até os 21 anos, salvo invalidez ou deficiência (Lei 8.213/1991, art. 77).",
            "Cônjuge ou companheiro: 4 meses se o óbito ocorreu antes de 18 contribuições ou com menos de 2 anos de casamento ou união, salvo óbito por acidente de qualquer natureza ou doença profissional; nos demais casos, pela idade no óbito: menos de 22 anos, 3 anos; de 22 a 27, 6; de 28 a 30, 10; de 31 a 41, 15; de 42 a 44, 20; 45 ou mais, vitalícia (art. 77, § 2º, V, com as idades da Portaria ME 424/2020 desde 2021). O cônjuge inválido ou com deficiência recebe enquanto durar a condição.",
            "Acumulação de pensão com aposentadoria própria ou outra pensão de outro regime: o benefício mais vantajoso é pago inteiro e o outro com o redutor por faixas de salário mínimo (EC 103/2019, art. 24, § 2º). Não há redutor quando os dois direitos foram adquiridos antes da reforma.",
            "Simulação com o salário mínimo e o teto da data do óbito. A qualidade de segurado, a comprovação da dependência e a média dos salários são conferidas pelo INSS."
        };

        return new DemonstrativoDto("Pensão por morte", $"Óbito em {Formato.Data(e.DataObito)} • {dependentes}",
            destaques, proventos, [], informativos, [new GrupoMemoriaDto("Pensão por morte", Formato.Moeda(p.Pensao), formulas)], observacoes,
            RotuloProventos: "Pensão", RotuloResultado: "Pensão mensal");
    }

    private static string Duracao(ApuracaoPensaoMorte p) => p switch
    {
        { Vitalicia: true } => "Vitalícia",
        { DuracaoMeses: 4 } => "4 meses",
        { DuracaoMeses: { } meses } => $"{meses / 12} anos",
        _ => "Não se aplica"
    };

    private static string DuracaoExplicada(ApuracaoPensaoMorte p)
    {
        var e = p.Entrada;
        if (p.DuracaoMeses == 4)
            return $"4 meses: {(e.MesesContribuicao < 18 ? "menos de 18 contribuições" : "menos de 2 anos de casamento ou união")} e óbito sem acidente";
        var idade = $"cônjuge com {e.IdadeConjuge} anos no óbito{(e.Causa != CausaObito.Comum && (e.MesesContribuicao < 18 || !e.UniaoDoisAnos) ? ", com a carência dispensada pelo acidente" : "")}";
        return p.Vitalicia ? $"Vitalícia: {idade}" : $"{p.DuracaoMeses / 12} anos: {idade}";
    }
}
