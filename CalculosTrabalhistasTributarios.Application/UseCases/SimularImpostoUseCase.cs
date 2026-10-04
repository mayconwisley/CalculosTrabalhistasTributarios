using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.Mapeamentos;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularImpostoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularImpostoUseCase
{
    public async Task<Result<SimulacaoImpostoDto>> ExecutarAsync(SimularImpostoRequest request, CancellationToken cancellationToken)
    {
        if (Validar(request) is { Falhou: true } invalido)
            return invalido.Erro;
        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var inss = tabelas.CalcularInss(request.BaseInss);
        var irrf = tabelas.CalcularIrrf(request.ValorBruto, inss.Valor, request.QuantidadeDependentes);
        var simplificadoDisponivel = tabelas.DescontoSimplificado is not null;

        var vantagem = CriarMensagemVantagem(irrf.Normal.Imposto, irrf.Simplificada.Imposto, simplificadoDisponivel);
        var modalidadeMaisVantajosa = ObterModalidadeMaisVantajosa(irrf.Normal, irrf.Simplificada, simplificadoDisponivel);
        return new SimulacaoImpostoDto(
            request,
            inss.BaseConsiderada,
            inss.Valor,
            irrf.Normal.ParaDto(),
            irrf.Simplificada.ParaDto(),
            tabelas.DescontoMinimo,
            // O FGTS não tem teto: incide sobre toda a remuneração informada, e não sobre a base limitada do INSS.
            CalculadoraTributacao.Arredondar(request.BaseInss * .08m),
            CalculadoraTributacao.Arredondar(request.BaseInss * .02m),
            vantagem,
            modalidadeMaisVantajosa,
            inss.Detalhes.ParaDto(),
            tabelas.DeducaoPorDependente,
            tabelas.DescontoSimplificado);
    }

    private static Result Validar(SimularImpostoRequest request)
    {
        if (request.ValorBruto < 0m || request.BaseInss < 0m || request.QuantidadeDependentes < 0)
            return Erro.Validacao("Os valores monetários e a quantidade de dependentes não podem ser negativos.");
        return Result.Ok();
    }

    private static string CriarMensagemVantagem(decimal normal, decimal simplificado, bool simplificadoDisponivel)
    {
        if (!simplificadoDisponivel)
            return "O desconto simplificado está disponível a partir de 05/2023.";
        if (normal == 0m && simplificado == 0m)
            return "Não há IRRF a recolher nas modalidades calculadas.";
        if (normal == simplificado)
            return "As modalidades normal e simplificada possuem o mesmo resultado.";
        var diferenca = Math.Abs(normal - simplificado);
        return normal > simplificado
            ? $"O cálculo simplificado é mais vantajoso. Diferença: {Formato.Moeda(diferenca)}."
            : $"O cálculo normal é mais vantajoso. Diferença: {Formato.Moeda(diferenca)}.";
    }

    private static string? ObterModalidadeMaisVantajosa(ModalidadeIrrf normal, ModalidadeIrrf simplificada, bool simplificadoDisponivel)
    {
        if (!simplificadoDisponivel || normal.Imposto == simplificada.Imposto)
            return null;

        return normal.Imposto < simplificada.Imposto ? normal.Nome : simplificada.Nome;
    }
}
