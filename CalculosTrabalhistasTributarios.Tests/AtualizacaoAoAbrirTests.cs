using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>A atualização de todas as tabelas na tela de abertura, uma vez por dia.</summary>
public class AtualizacaoAoAbrirTests
{
    private static readonly DateOnly Hoje = new(2027, 1, 15);

    [Fact]
    public async Task Consulta_cada_fonte_uma_vez_e_informa_o_andamento()
    {
        var atualizador = new AtualizadorFalso();
        var andamento = new List<ProgressoAtualizacaoTabelas>();
        var useCase = new AtualizarTodasTabelasUseCase(atualizador);

        var resultados = await useCase.ExecutarAsync(new ProgressoImediato(andamento.Add), TestContext.Current.CancellationToken);

        Assert.Equal(useCase.Total, resultados.Count);
        Assert.Equal(useCase.Total, atualizador.Consultadas.Distinct().Count());
        // Simplificado, dependente e redução vêm da página do IRRF: consultá-los de novo repetiria a mesma gravação.
        Assert.DoesNotContain(TipoTabelaTributaria.Simplificado, atualizador.Consultadas);
        Assert.DoesNotContain(TipoTabelaTributaria.Dependente, atualizador.Consultadas);
        Assert.DoesNotContain(TipoTabelaTributaria.ReducaoMensalIrrf, atualizador.Consultadas);
        Assert.Equal(Enumerable.Range(0, useCase.Total + 1), andamento.Select(etapa => etapa.Concluidas));
        Assert.Equal("Tabela INSS", andamento[0].TabelaAtual);
        Assert.Equal(string.Empty, andamento[^1].TabelaAtual);
    }

    [Fact]
    public async Task Uma_fonte_fora_do_ar_nao_impede_as_demais()
    {
        var atualizador = new AtualizadorFalso(falham: [TipoTabelaTributaria.Selic]);

        var resultados = await new AtualizarTodasTabelasUseCase(atualizador).ExecutarAsync(null, TestContext.Current.CancellationToken);

        var selic = Assert.Single(resultados, resultado => resultado.Resultado.Falhou);
        Assert.Equal(TipoTabelaTributaria.Selic, selic.Tipo);
        Assert.Equal(TipoTabelaTributaria.Tr, atualizador.Consultadas[^1]);
    }

    [Fact]
    public async Task Pular_interrompe_as_tabelas_que_faltam()
    {
        using var pular = new CancellationTokenSource();
        var atualizador = new AtualizadorFalso(aoConsultar: tipo => { if (tipo == TipoTabelaTributaria.Irrf) pular.Cancel(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AtualizarTodasTabelasUseCase(atualizador).ExecutarAsync(null, pular.Token));
        Assert.Equal([TipoTabelaTributaria.Inss, TipoTabelaTributaria.Irrf], atualizador.Consultadas);
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData("2027-01-14", true, true)]
    [InlineData("2027-01-15", true, false)]
    [InlineData("2027-01-20", true, true)]
    [InlineData(null, false, false)]
    public void Atualiza_na_primeira_abertura_do_dia(string? ultima, bool ligada, bool atualiza)
    {
        var configuracoes = new Configuracoes { AtualizarTabelasAoAbrir = ligada, UltimaAtualizacaoTabelas = ultima is null ? null : DateOnly.Parse(ultima) };

        Assert.Equal(atualiza, AberturaViewModel.DeveAtualizar(configuracoes, Hoje));
    }

    [Theory]
    [InlineData("2027-01-15T08:12:00", null, "Tabelas atualizadas pela internet hoje às 08:12.")]
    [InlineData("2027-01-14T17:40:00", null, "Tabelas atualizadas pela internet ontem às 17:40.")]
    [InlineData("2026-12-31T23:59:00", null, "Tabelas atualizadas pela internet em 31/12/2026 às 23:59.")]
    [InlineData(null, "2027-01-10", "Tabelas atualizadas pela internet em 10/01/2027.")]
    [InlineData(null, null, "As tabelas ainda não foram atualizadas pela internet neste computador.")]
    public void Descreve_quando_as_tabelas_foram_atualizadas(string? horario, string? dia, string esperado)
    {
        // Arquivos das versões anteriores só têm o dia da atualização.
        var configuracoes = new Configuracoes
        {
            HorarioAtualizacaoTabelas = horario is null ? null : DateTime.Parse(horario, System.Globalization.CultureInfo.InvariantCulture),
            UltimaAtualizacaoTabelas = dia is null ? null : DateOnly.Parse(dia, System.Globalization.CultureInfo.InvariantCulture)
        };

        Assert.Equal(esperado, AberturaViewModel.DescreverUltimaAtualizacao(configuracoes, new DateTime(2027, 1, 15, 10, 0, 0)));
    }

    [Fact]
    public void Avisa_as_tabelas_nao_atualizadas_com_o_motivo()
    {
        AtualizacaoDeTabela[] resultados =
        [
            new(TipoTabelaTributaria.Inss, "Tabela INSS", Resultado()),
            new(TipoTabelaTributaria.Selic, "Selic", Erro.Indisponivel("O Banco Central não respondeu."))
        ];

        Assert.Null(AberturaViewModel.DescreverPendencias(resultados[..1]));
        Assert.Contains("1 de 2 tabelas não foram atualizadas (Selic)", AberturaViewModel.DescreverPendencias(resultados));
        Assert.Equal("Selic: O Banco Central não respondeu.", AberturaViewModel.DetalharPendencias(resultados));
        Assert.Contains("nenhuma fonte respondeu", AberturaViewModel.DescreverPendencias(resultados[1..]));
    }

    private static Result<AtualizacaoTabelaResultado> Resultado() => new AtualizacaoTabelaResultado(Hoje, 1, ["Fonte"], true, []);

    private sealed class ProgressoImediato(Action<ProgressoAtualizacaoTabelas> registrar) : IProgress<ProgressoAtualizacaoTabelas>
    {
        public void Report(ProgressoAtualizacaoTabelas value) => registrar(value);
    }

    private sealed class AtualizadorFalso(TipoTabelaTributaria[]? falham = null, Action<TipoTabelaTributaria>? aoConsultar = null) : IAtualizadorTabelas
    {
        public List<TipoTabelaTributaria> Consultadas { get; } = [];

        public Uri FonteOficial(TipoTabelaTributaria tipo) => new("https://www.gov.br/");
        public string NomeFonteOficial(TipoTabelaTributaria tipo) => "Fonte";

        public Task<Result<AtualizacaoTabelaResultado>> AtualizarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken)
        {
            Consultadas.Add(tipo);
            aoConsultar?.Invoke(tipo);
            return Task.FromResult(falham?.Contains(tipo) == true ? Erro.Indisponivel("Fonte fora do ar.") : Resultado());
        }
    }
}
