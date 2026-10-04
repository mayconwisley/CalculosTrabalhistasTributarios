using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Pensão alimentícia e IRRF nas duas modalidades. Nas deduções legais, as pensões são deduzidas da base do IRRF; no
/// desconto simplificado, não, porque ele substitui todas as deduções legais, inclusive a pensão (Lei 9.250/1995, art. 4º).
/// </summary>
public sealed class SimularPensaoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularPensaoUseCase
{
    private const int MaximoBeneficiarios = 10;
    private static readonly DateOnly InicioSimplificado = new(2023, 5, 1);

    public async Task<Result<SimulacaoPensaoDto>> ExecutarAsync(SimularPensaoRequest request, CancellationToken cancellationToken)
    {
        if (request.ValorBruto < 0m || request.BaseInss < 0m || request.Dependentes < 0 || request.OutrosDescontos < 0m || request.OutrosDescontos > request.ValorBruto)
            return Erro.Validacao("Os parâmetros da pensão são inválidos.");
        if (request.Beneficiarios.Count is 0 or > MaximoBeneficiarios)
            return Erro.Validacao($"Informe de 1 a {MaximoBeneficiarios} beneficiários.");
        if (Result.Combinar(request.Beneficiarios.Select(beneficiario => beneficiario.Regra.Validar()).ToArray()) is { Falhou: true } regraInvalida)
            return regraInvalida.Erro;
        // Na mesma base, as pensões não podem somar mais que os próprios rendimentos.
        if (!request.Sucessiva)
            foreach (var grupo in request.Beneficiarios.Where(beneficiario => beneficiario.Regra.Base is BasePensao.RendimentosLiquidos or BasePensao.RendimentosBrutos).GroupBy(beneficiario => beneficiario.Regra.Base))
                if (grupo.Sum(beneficiario => beneficiario.Regra.Percentual) > 100m)
                    return Erro.Validacao($"A soma dos percentuais sobre os rendimentos {(grupo.Key == BasePensao.RendimentosLiquidos ? "líquidos" : "brutos")} passa de 100%.");

        var consultaPerfil = await tributacaoConsulta.ObterPerfilAsync(request.Competencia, cancellationToken);
        if (consultaPerfil.Falhou)
            return consultaPerfil.Erro;
        var perfil = consultaPerfil.Valor;
        var salarioMinimo = perfil.SalarioMinimo;
        if (request.Beneficiarios.Any(beneficiario => beneficiario.Regra.Base == BasePensao.SalarioMinimo) && salarioMinimo is null)
            return Erro.NaoEncontrado($"Não há salário mínimo cadastrado para a competência {request.Competencia:MM/yyyy}.");

        var inss = perfil.FaixasInss;
        var irrf = perfil.FaixasIrrf;
        var regrasReducao = perfil.ReducoesMensaisIrrf;
        var baseInss = Math.Min(request.BaseInss, inss.Max(x => x.Limite));
        var valorInss = CalculadoraTributacao.Arredondar(CalculadoraInss.CalcularDetalhes(request.Competencia, baseInss, inss).Sum(x => x.Imposto));
        var rendimentos = request.ValorBruto - request.OutrosDescontos;
        var pensoes = new CalculoPensoes(request.Beneficiarios, request.Sucessiva, rendimentos, valorInss, salarioMinimo ?? 0m);
        var deducoesNormal = valorInss + request.Dependentes * perfil.DeducaoPorDependente;
        var limiteDispensa = perfil.DescontoMinimo;
        var simplificadoDisponivel = request.Competencia >= InicioSimplificado;
        var normal = Calcular("Normal", rendimentos, deducoesNormal, request.Dependentes > 0 ? "INSS e dependentes" : "INSS", true, pensoes, irrf, regrasReducao, limiteDispensa);
        var simplificada = Calcular("Simplificado", rendimentos, perfil.DeducaoSimplificada, "desconto simplificado", false, pensoes, irrf, regrasReducao, limiteDispensa);
        // A fonte pagadora aplica a modalidade de menor IRRF.
        var vantagem = !simplificadoDisponivel ? "Só a modalidade normal: o desconto simplificado existe a partir de 05/2023."
            : normal.Imposto == simplificada.Imposto ? "As duas modalidades resultam no mesmo IRRF."
            : normal.Imposto < simplificada.Imposto ? "O cálculo normal é mais vantajoso." : "O cálculo simplificado é mais vantajoso.";
        // Sem a pensão, a fonte pagadora aplicaria a modalidade de menor imposto; o simplificado não muda com a pensão.
        var semPensaoAntesReducao = CalculadoraTributacao.CalcularPorFaixa(Math.Max(0m, rendimentos - deducoesNormal), irrf);
        var normalSemPensao = Reter(CalculadoraTributacao.Arredondar(semPensaoAntesReducao - CalculadoraReducaoMensalIrrf.Calcular(rendimentos, semPensaoAntesReducao, regrasReducao)), limiteDispensa);
        return new SimulacaoPensaoDto(valorInss, normal, simplificada, vantagem, simplificadoDisponivel ? Math.Min(normalSemPensao, simplificada.Imposto) : normalSemPensao, simplificadoDisponivel);
    }

    /// <param name="deduzPensao">Nas deduções legais, as pensões reduzem a base do IRRF e o cálculo se repete até o total se estabilizar.</param>
    /// <summary>O IRRF de até o limite (R$ 10,00) não é retido (Lei 9.430/1996, art. 67).</summary>
    private static decimal Reter(decimal imposto, decimal limiteDispensa) => imposto > 0m && imposto <= limiteDispensa ? 0m : imposto;

    private static ModalidadePensaoDto Calcular(string nome, decimal rendimentosTributaveis, decimal deducoesBase, string rotuloDeducoes, bool deduzPensao, CalculoPensoes calculo, IReadOnlyList<FaixaTributaria> faixas, IReadOnlyList<RegraReducaoMensalIrrf> regrasReducao, decimal limiteDispensa)
    {
        var baseIrrfInicial = rendimentosTributaveis - deducoesBase;
        // Fora da base líquida, as pensões não dependem do IRRF e já entram na base desde a primeira iteração.
        var pensao = calculo.DependeDoIrrf ? 0m : calculo.Calcular(0m).Sum(item => item.Pensao);
        var imposto = 0m; var impostoAntesReducao = 0m; var reducaoMensal = 0m; var iteracoes = 0;
        var detalhes = new List<IteracaoPensaoDto>();
        for (; iteracoes < 100; iteracoes++)
        {
            var pensaoDeduzida = deduzPensao ? pensao : 0m;
            var baseIrrf = Math.Max(0m, baseIrrfInicial - pensaoDeduzida);
            var faixa = faixas.OrderBy(item => item.Numero).FirstOrDefault(item => baseIrrf <= item.Limite) ?? faixas.MaxBy(item => item.Limite)!;
            impostoAntesReducao = CalculadoraTributacao.CalcularPorFaixa(baseIrrf, faixas);
            reducaoMensal = CalculadoraReducaoMensalIrrf.Calcular(rendimentosTributaveis, impostoAntesReducao, regrasReducao);
            imposto = Reter(CalculadoraTributacao.Arredondar(impostoAntesReducao - reducaoMensal), limiteDispensa);
            var pensoes = calculo.Calcular(imposto);
            var novaPensao = pensoes.Sum(item => item.Pensao);
            detalhes.Add(new IteracaoPensaoDto(iteracoes + 1, baseIrrf, faixa.Aliquota, faixa.Deducao, impostoAntesReducao, reducaoMensal, imposto, pensoes[0].Base, novaPensao, pensaoDeduzida, pensoes));
            // Estável quando o IRRF foi apurado com esta mesma pensão deduzida; sem a pensão na base, o imposto não muda.
            if (!deduzPensao || novaPensao == pensao) { pensao = novaPensao; break; }
            pensao = novaPensao;
        }
        return new ModalidadePensaoDto(nome, impostoAntesReducao, reducaoMensal, imposto, pensao, CalculadoraTributacao.Arredondar(imposto + pensao), iteracoes + 1, detalhes, deducoesBase, rotuloDeducoes, deduzPensao);
    }

    /// <summary>Base e valor da pensão de cada beneficiário pela regra da decisão, dado o IRRF que reduz os rendimentos líquidos.</summary>
    /// <param name="sucessiva">Cada pensão percentual sobre os rendimentos é calculada depois de descontar as anteriores.</param>
    private sealed class CalculoPensoes(IReadOnlyList<BeneficiarioPensao> beneficiarios, bool sucessiva, decimal rendimentos, decimal valorInss, decimal salarioMinimo)
    {
        public bool DependeDoIrrf => beneficiarios.Any(beneficiario => beneficiario.Regra.Base == BasePensao.RendimentosLiquidos);

        public IReadOnlyList<PensaoBeneficiarioDto> Calcular(decimal imposto)
        {
            var pensoes = new List<PensaoBeneficiarioDto>(beneficiarios.Count);
            var anteriores = 0m;
            foreach (var (nome, regra) in beneficiarios)
            {
                var descontoAnteriores = sucessiva ? anteriores : 0m;
                var basePensao = regra.Base switch
                {
                    BasePensao.RendimentosLiquidos => Math.Max(0m, rendimentos - valorInss - imposto - descontoAnteriores),
                    BasePensao.RendimentosBrutos => Math.Max(0m, rendimentos - descontoAnteriores),
                    BasePensao.SalarioMinimo => salarioMinimo,
                    _ => regra.Valor
                };
                var pensao = regra.EhPercentual ? CalculadoraTributacao.Arredondar(basePensao * regra.Percentual / 100m) : regra.Valor;
                pensoes.Add(new PensaoBeneficiarioDto(nome, regra, basePensao, pensao));
                anteriores += pensao;
            }
            return pensoes;
        }
    }
}
