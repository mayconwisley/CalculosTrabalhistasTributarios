using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Tabelas de INSS e IRRF vigentes em uma competência e a apuração mensal comum a todas as calculadoras:
/// o INSS por faixas e o IRRF nas duas modalidades, deduções legais e desconto simplificado, com a redução mensal.
/// </summary>
public sealed class TabelasDaCompetencia
{
    private static readonly DateOnly InicioSimplificado = new(2023, 5, 1);
    private const decimal AliquotaContribuinteIndividual = 11m;
    private readonly IReadOnlyList<FaixaTributaria> _faixasInss;
    private readonly IReadOnlyList<FaixaTributaria> _faixasIrrf;
    private readonly IReadOnlyList<RegraReducaoMensalIrrf> _regrasReducao;
    private readonly IReadOnlyList<FaixaTributaria> _faixasPlr;

    public TabelasDaCompetencia(DateOnly competencia, PerfilTributario perfil)
    {
        Competencia = competencia;
        _faixasInss = perfil.FaixasInss;
        _faixasIrrf = perfil.FaixasIrrf;
        _regrasReducao = perfil.ReducoesMensaisIrrf;
        TetoInss = _faixasInss.Max(item => item.Limite);
        DeducaoPorDependente = perfil.DeducaoPorDependente;
        DescontoSimplificado = competencia >= InicioSimplificado ? perfil.DeducaoSimplificada : null;
        DescontoMinimo = perfil.DescontoMinimo;
        SalarioMinimo = perfil.SalarioMinimo;
        _faixasPlr = perfil.FaixasPlr ?? [];
        FaixasSalarioFamilia = perfil.FaixasSalarioFamilia ?? [];
        FaixasSeguroDesemprego = perfil.FaixasSeguroDesemprego ?? [];
    }

    public DateOnly Competencia { get; }
    public decimal TetoInss { get; }

    /// <summary>Limite de cada faixa do INSS, da menor para a maior.</summary>
    public IReadOnlyList<decimal> LimitesInss => _faixasInss.OrderBy(item => item.Numero).Select(item => item.Limite).ToArray();
    public decimal DeducaoPorDependente { get; }

    /// <summary>Nulo antes de 05/2023, quando a modalidade simplificada não existia.</summary>
    public decimal? DescontoSimplificado { get; }

    public decimal DescontoMinimo { get; }

    /// <summary>Nulo quando não há salário mínimo cadastrado até a competência.</summary>
    public decimal? SalarioMinimo { get; }

    /// <summary>O salário mínimo vigente, para os cálculos que não podem seguir sem ele.</summary>
    public Result<decimal> ObterSalarioMinimo() =>
        SalarioMinimo is { } valor ? valor : Erro.NaoEncontrado($"Não há salário mínimo cadastrado para a competência {Competencia:MM/yyyy}.");

    /// <summary>Tabela anual da PLR vigente; vazia sem tabela cadastrada.</summary>
    public IReadOnlyList<FaixaTributaria> FaixasPlr => _faixasPlr;

    /// <summary>Faixas do salário-família vigentes, da menor para a maior remuneração; vazias sem tabela cadastrada.</summary>
    public IReadOnlyList<FaixaSalarioFamilia> FaixasSalarioFamilia { get; }

    /// <summary>Faixas do seguro-desemprego vigentes: limite da média, percentual sobre o excedente e valor fixo; vazias sem tabela.</summary>
    public IReadOnlyList<FaixaTributaria> FaixasSeguroDesemprego { get; }

    /// <summary>INSS do segurado empregado, com a base limitada ao teto. Valores negativos devem ser barrados antes, na validação.</summary>
    public ApuracaoInss CalcularInss(decimal baseInss)
    {
        var baseConsiderada = Math.Min(baseInss, TetoInss);
        var detalhes = CalculadoraInss.CalcularDetalhes(Competencia, baseConsiderada, _faixasInss);
        return new ApuracaoInss(baseInss, baseConsiderada, CalculadoraTributacao.Arredondar(detalhes.Sum(item => item.Imposto)), detalhes);
    }

    /// <summary>Faixas de um novo vínculo de empregado, após as faixas já ocupadas por vínculos anteriores dessa categoria.</summary>
    internal IReadOnlyList<ResultadoFaixaTributaria> CalcularFaixasInssEntre(decimal baseAnterior, decimal baseFinal) =>
        CalculadoraInss.CalcularDetalhesIntervalo(baseAnterior, baseFinal, _faixasInss);

    /// <summary>INSS de 11% do contribuinte individual (sócio ou autônomo) retido pela empresa, com a base limitada ao teto.</summary>
    public ApuracaoInss CalcularInssContribuinteIndividual(decimal remuneracao)
    {
        var baseConsiderada = Math.Min(Math.Max(0m, remuneracao), TetoInss);
        var valor = CalculadoraTributacao.Truncar(baseConsiderada * AliquotaContribuinteIndividual / 100m);
        return new ApuracaoInss(remuneracao, baseConsiderada, valor, baseConsiderada > 0m ? [new ResultadoFaixaTributaria(1, baseConsiderada, AliquotaContribuinteIndividual, valor)] : []);
    }

    /// <param name="rendimentos">Rendimentos tributáveis, que também definem a redução mensal.</param>
    /// <param name="inss">Contribuição previdenciária deduzida na modalidade de deduções legais.</param>
    /// <param name="pensao">
    /// Pensão alimentícia judicial ou por escritura pública, deduzida só na modalidade de deduções legais: o desconto
    /// simplificado substitui todas elas, inclusive a pensão.
    /// </param>
    /// <param name="tributacaoExclusiva">
    /// Verdadeiro no 13º, tributado à parte e fora da declaração de ajuste: a dispensa de retenção do IRRF de até
    /// <see cref="DescontoMinimo"/> só vale para os rendimentos que entram no ajuste anual (Lei 9.430/1996, art. 67).
    /// </param>
    /// <param name="previdenciaComplementar">
    /// Contribuição do trabalhador à previdência complementar ou ao Fapi, deduzida por inteiro na base mensal, só na
    /// modalidade de deduções legais (IN RFB 1.500/2014, art. 52, IV e V); o limite de 12% é aplicado na declaração anual.
    /// </param>
    /// <param name="livroCaixa">
    /// Despesas escrituradas no livro-caixa do autônomo, deduzidas no carnê-leão só na modalidade de deduções legais
    /// (IN RFB 1.500/2014); o desconto simplificado substitui essa dedução.
    /// </param>
    public ApuracaoIrrf CalcularIrrf(decimal rendimentos, decimal inss, int dependentes, decimal pensao = 0m, bool tributacaoExclusiva = false, decimal previdenciaComplementar = 0m, decimal livroCaixa = 0m)
    {
        var baseNormal = Math.Max(0m, rendimentos - inss - dependentes * DeducaoPorDependente - pensao - previdenciaComplementar - livroCaixa);
        var normal = CalcularModalidade("Normal", rendimentos, baseNormal);
        var simplificada = DescontoSimplificado is { } desconto
            ? CalcularModalidade("Simplificado", rendimentos, Math.Max(0m, rendimentos - desconto))
            : ModalidadeIrrf.Indisponivel("Simplificado");
        return new ApuracaoIrrf(rendimentos, inss, dependentes, DeducaoPorDependente, DescontoSimplificado, normal, simplificada, pensao, tributacaoExclusiva ? 0m : DescontoMinimo, previdenciaComplementar, livroCaixa);
    }

    /// <summary>
    /// IRRF pela tabela anual exclusiva da PLR (Lei 10.101/2000, art. 3º, § 5º), sem dependentes, desconto simplificado
    /// nem redução mensal: só a pensão alimentícia sobre a PLR é deduzida da base.
    /// </summary>
    public Result<ApuracaoPlr> CalcularIrrfPlr(decimal baseCalculo)
    {
        if (_faixasPlr.Count == 0)
            return Erro.NaoEncontrado($"Não há tabela de PLR cadastrada para a competência {Competencia:MM/yyyy}.");

        var faixa = _faixasPlr.OrderBy(item => item.Numero).FirstOrDefault(item => baseCalculo <= item.Limite) ?? _faixasPlr.MaxBy(item => item.Limite)!;
        return new ApuracaoPlr(baseCalculo, faixa.Numero, faixa.Aliquota, faixa.Deducao, CalculadoraTributacao.CalcularPorFaixa(baseCalculo, _faixasPlr));
    }

    /// <summary>
    /// IRRF dos rendimentos recebidos acumuladamente relativos a anos anteriores, pela tabela da competência do
    /// recebimento com limites e parcelas a deduzir multiplicados por <paramref name="meses"/> (IN RFB 1.500/2014,
    /// art. 37 e Anexo IV). Não há dependentes, desconto simplificado nem dispensa de retenção: a tributação é exclusiva.
    /// O imposto é a soma exata das faixas, arredondada ao final, o que equivale às parcelas a deduzir do Anexo IV.
    /// </summary>
    /// <param name="rendimentosTributaveis">Rendimentos tributáveis do RRA, que definem a faixa da redução.</param>
    /// <param name="aplicarReducao">
    /// Aplica a redução da Lei 15.270/2025 com os limites de rendimento e os valores multiplicados pelos meses. O art. 37
    /// manda observar a tabela do Anexo X, mas não detalha a multiplicação; a escolha fica explícita para o usuário.
    /// </param>
    public ApuracaoIrrfAcumulado CalcularIrrfAcumulado(decimal rendimentosTributaveis, decimal baseCalculo, decimal meses, bool aplicarReducao)
    {
        var faixas = _faixasIrrf.Select(item => new FaixaTributaria(item.Numero, item.Limite * meses, item.Aliquota, item.Deducao * meses)).ToArray();
        var baseConsiderada = Math.Max(0m, baseCalculo);
        var exatas = CalculadoraTributacao.CalcularProgressivo(baseConsiderada, faixas, valor => valor);
        var impostoAntesReducao = CalculadoraTributacao.Arredondar(exatas.Sum(item => item.Imposto));
        var faixa = faixas.OrderBy(item => item.Numero).FirstOrDefault(item => baseConsiderada <= item.Limite) ?? faixas.MaxBy(item => item.Limite)!;
        var anterior = faixas.Where(item => item.Numero < faixa.Numero).Select(item => item.Limite / meses).DefaultIfEmpty(0m).Max();
        var impostoNoLimiteAnterior = CalculadoraTributacao.CalcularProgressivo(anterior, _faixasIrrf, valor => valor).Sum(item => item.Imposto);
        var parcelaMensal = anterior * faixa.AliquotaDecimal - impostoNoLimiteAnterior;

        var reducaoAplicavel = aplicarReducao && _regrasReducao.Count > 0;
        var reducao = reducaoAplicavel
            ? CalculadoraReducaoMensalIrrf.Calcular(rendimentosTributaveis, impostoAntesReducao,
                _regrasReducao.Select(item => item with { LimiteRendimentos = item.LimiteRendimentos * meses, ValorBase = item.ValorBase * meses }).ToArray())
            : 0m;
        return new ApuracaoIrrfAcumulado(meses, rendimentosTributaveis, baseConsiderada, faixa.Aliquota, parcelaMensal,
            CalculadoraTributacao.CalcularProgressivo(baseConsiderada, faixas), impostoAntesReducao, reducaoAplicavel, reducao,
            CalculadoraTributacao.Arredondar(impostoAntesReducao - reducao));
    }

    /// <summary>Há redução mensal do IRRF na competência (Lei 15.270/2025, a partir de 01/2026).</summary>
    public bool TemReducaoMensal => _regrasReducao.Count > 0;

    private ModalidadeIrrf CalcularModalidade(string nome, decimal rendimentosTributaveis, decimal baseCalculo)
    {
        var faixa = _faixasIrrf.OrderBy(item => item.Numero).FirstOrDefault(item => baseCalculo <= item.Limite) ?? _faixasIrrf.MaxBy(item => item.Limite)!;
        var impostoAntesReducao = CalculadoraTributacao.CalcularPorFaixa(baseCalculo, _faixasIrrf);
        var reducao = CalculadoraReducaoMensalIrrf.Calcular(rendimentosTributaveis, impostoAntesReducao, _regrasReducao);
        var imposto = CalculadoraTributacao.Arredondar(impostoAntesReducao - reducao);
        var aliquotaEfetiva = rendimentosTributaveis == 0m ? 0m : Math.Truncate(imposto / rendimentosTributaveis * 10_000m) / 100m;
        return new ModalidadeIrrf(nome, baseCalculo, faixa.Aliquota, faixa.Deducao, impostoAntesReducao, reducao, imposto, aliquotaEfetiva, CalculadoraTributacao.CalcularProgressivo(baseCalculo, _faixasIrrf));
    }
}
