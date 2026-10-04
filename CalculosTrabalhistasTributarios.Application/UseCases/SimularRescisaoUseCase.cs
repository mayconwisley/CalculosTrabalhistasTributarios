using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Rescisão do contrato de trabalho: as verbas vêm do domínio; aqui entram as tabelas da competência, o INSS e o IRRF
/// das verbas do mês e do 13º (cada um à parte), a pensão e a estimativa do seguro-desemprego. Aviso indenizado, férias
/// indenizadas, indenizações e multas não têm INSS nem IRRF.
/// </summary>
public sealed class SimularRescisaoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularRescisaoRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularRescisaoRequest r, CancellationToken cancellationToken)
    {
        if (r.Dependentes < 0)
            return Erro.Validacao("Os valores, as faltas e a quantidade de dependentes não podem ser negativos.");
        var calculo = CalculadoraRescisao.Calcular(Contrato(r));
        if (calculo.Falhou)
            return calculo.Erro;
        var verbas = calculo.Valor;

        // O INSS segue a competência do desligamento; o IRRF, o mês do pagamento (regime de caixa), que pode cair no mês seguinte.
        var competenciaDesligamento = verbas.Contrato.CompetenciaDesligamento;
        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(competenciaDesligamento, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var pagamento = r.DataPagamento is { } dataPagamento ? new DateOnly(dataPagamento.Year, dataPagamento.Month, 1) : competenciaDesligamento;
        var tabelasIrrf = tabelas;
        if (pagamento != competenciaDesligamento)
        {
            var consultaTabelasIrrf = await tributacaoConsulta.ObterTabelasAsync(pagamento, cancellationToken);
            if (consultaTabelasIrrf.Falhou)
                return consultaTabelasIrrf.Erro;
            tabelasIrrf = consultaTabelasIrrf.Valor;
        }

        var seguro = EstimarSeguro(verbas, tabelas);
        if (seguro.Falhou)
            return seguro.Erro;
        var tributos = Tributar(verbas, r, tabelas, tabelasIrrf, pagamento);
        if (tributos.Falhou)
            return tributos.Erro;
        return DemonstrativoRescisao.Montar(verbas, tributos.Valor, seguro.Valor);
    }

    private static ContratoRescindido Contrato(SimularRescisaoRequest r) => new(
        r.Admissao, r.Desligamento, r.Motivo, r.Aviso, r.Salario, r.Medias, r.PeriodosFeriasVencidas, r.FaltasPeriodoAtual, r.SaldoFgts,
        r.AdiantamentoDecimoTerceiro, r.DataPagamento, r.FimPrevistoContrato, r.MesDataBase, r.OutrosProventos, r.FaltasNoMes);

    /// <summary>Estimativa na 1ª solicitação, com os meses deste contrato e a remuneração atual como média; nula sem direito ou sem tabela.</summary>
    private static Result<EstimativaSeguroRescisao?> EstimarSeguro(VerbasRescisorias verbas, TabelasDaCompetencia tabelas)
    {
        var c = verbas.Contrato;
        var temSeguro = c.Motivo is MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.RescisaoAntecipadaPeloEmpregador
            && tabelas.FaixasSeguroDesemprego.Count > 0 && tabelas.SalarioMinimo is not null && c.Remuneracao > 0m;
        if (!temSeguro)
            return Result.Ok<EstimativaSeguroRescisao?>(null);
        var meses = Math.Min(36, RegrasTrabalhistas.AvosFerias(c.Admissao, c.Desligamento));
        var parcela = RegrasSeguroDesemprego.ValorDaParcela(c.Remuneracao, tabelas.FaixasSeguroDesemprego, tabelas.SalarioMinimo!.Value);
        return parcela.Falhou
            ? parcela.Erro
            : Result.Ok<EstimativaSeguroRescisao?>(new EstimativaSeguroRescisao(meses, RegrasSeguroDesemprego.Parcelas(SolicitacaoSeguroDesemprego.Primeira, meses), parcela.Valor));
    }

    /// <summary>
    /// INSS e IRRF das verbas do mês e do 13º. A pensão incide sobre essas verbas salariais, cada uma deduzida da base do
    /// IRRF dela; o valor informado sai do saldo de salário.
    /// </summary>
    private static Result<TributosRescisao> Tributar(VerbasRescisorias verbas, SimularRescisaoRequest r, TabelasDaCompetencia tabelas, TabelasDaCompetencia tabelasIrrf, DateOnly pagamento)
    {
        var verbasDoMes = verbas.Saldo.VerbasDoMes;
        var total13 = verbas.DecimoTerceiro.Total;
        var inssSaldo = tabelas.CalcularInss(verbasDoMes);
        var inss13 = tabelas.CalcularInss(total13);
        var regra = r.Pensao;
        var calculoPensaoSaldo = regra is null ? null
            : CalculadoraPensao.Calcular(regra, verbasDoMes, inssSaldo.Valor, valor => tabelasIrrf.CalcularIrrf(verbasDoMes, inssSaldo.Valor, r.Dependentes, valor), apuracao => apuracao.Imposto);
        if (calculoPensaoSaldo is { Falhou: true })
            return calculoPensaoSaldo.Erro;
        var calculoPensao13 = regra is { EhPercentual: true } && total13 > 0m
            ? CalculadoraPensao.Calcular(regra, total13, inss13.Valor, valor => tabelasIrrf.CalcularIrrf(total13, inss13.Valor, r.Dependentes, valor, tributacaoExclusiva: true), apuracao => apuracao.Imposto)
            : null;
        if (calculoPensao13 is { Falhou: true })
            return calculoPensao13.Erro;
        var pensaoSaldo = calculoPensaoSaldo?.Valor;
        var pensao13 = calculoPensao13?.Valor;
        return new TributosRescisao(
            pagamento,
            inssSaldo,
            inss13,
            pensaoSaldo?.Apuracao ?? tabelasIrrf.CalcularIrrf(verbasDoMes, inssSaldo.Valor, r.Dependentes),
            pensao13?.Apuracao ?? tabelasIrrf.CalcularIrrf(total13, inss13.Valor, r.Dependentes, tributacaoExclusiva: true),
            regra,
            pensaoSaldo,
            pensao13);
    }
}
