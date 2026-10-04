using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure.Diagnostico;
using System.IO;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Os avisos da abertura (versão nova e tabelas do ano) e o registro de erros inesperados.</summary>
public class AtualizacoesEDiagnosticoTests
{
    private static readonly DateOnly Hoje = new(2027, 2, 10);
    private static readonly VersaoPublicada Versao170 = new("1.7.0", new Uri("https://github.com/mayconwisley/CalculosTrabalhistasTributarios/releases/tag/v1.7.0"));

    [Theory]
    [InlineData("1.7.0", "1.6.1", true)]
    [InlineData("1.6.10", "1.6.9", true)]
    [InlineData("1.6.1", "1.6.1", false)]
    [InlineData("1.6.0", "1.6.1", false)]
    [InlineData("versão", "1.6.1", false)]
    public void Compara_as_versoes_pelos_numeros(string publicada, string atual, bool maisNova) =>
        Assert.Equal(maisNova, VerificarAtualizacoesUseCase.EhMaisNova(publicada, atual));

    [Fact]
    public async Task Avisa_da_versao_nova_e_das_tabelas_do_ano_sem_cadastro()
    {
        var avisos = await new VerificarAtualizacoesUseCase(new VersaoFixa(Versao170), new TabelaInss(new DateOnly(2026, 1, 1)))
            .ExecutarAsync("1.6.1", consultarVersao: true, Hoje, default);

        Assert.Collection(avisos,
            tabelas => Assert.Equal(TipoAvisoAtualizacao.TabelasDesatualizadas, tabelas.Tipo),
            versao =>
            {
                Assert.Equal(TipoAvisoAtualizacao.NovaVersao, versao.Tipo);
                Assert.Equal(Versao170.Endereco, versao.Endereco);
                Assert.Contains("1.7.0", versao.Mensagem);
            });
    }

    [Fact]
    public async Task Sem_consulta_da_versao_e_com_as_tabelas_do_ano_nao_ha_aviso()
    {
        var versoes = new VersaoFixa(Versao170);
        var avisos = await new VerificarAtualizacoesUseCase(versoes, new TabelaInss(new DateOnly(2027, 1, 1)))
            .ExecutarAsync("1.6.1", consultarVersao: false, Hoje, default);

        Assert.Empty(avisos);
        Assert.Equal(0, versoes.Consultas);
    }

    [Fact]
    public async Task Github_fora_do_ar_nao_gera_aviso()
    {
        var avisos = await new VerificarAtualizacoesUseCase(new VersaoFixa(Erro.Indisponivel("fora do ar")), new TabelaInss(new DateOnly(2027, 1, 1)))
            .ExecutarAsync("1.6.1", consultarVersao: true, Hoje, default);

        Assert.Empty(avisos);
    }

    [Fact]
    public void Registro_de_erros_grava_o_contexto_e_a_excecao()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"registro-{Guid.NewGuid():N}");
        try
        {
            var registro = new ArquivoRegistroDeErros(pasta);
            registro.Registrar(new InvalidOperationException("banco travado"), "Salvar o cálculo");

            var texto = File.ReadAllText(registro.Arquivo);
            Assert.Contains("Salvar o cálculo", texto);
            Assert.Contains("InvalidOperationException: banco travado", texto);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    private sealed class VersaoFixa(Result<VersaoPublicada> resultado) : IConsultaVersaoPublicada
    {
        public int Consultas { get; private set; }

        public Task<Result<VersaoPublicada>> ConsultarAsync(CancellationToken cancellationToken)
        {
            Consultas++;
            return Task.FromResult(resultado);
        }
    }

    private sealed class TabelaInss(DateOnly ultimaCompetencia) : ITabelaTributariaService
    {
        public Task<IReadOnlyList<RegistroTabelaDto>> ListarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RegistroTabelaDto>>([new(1, ultimaCompetencia, 1, 1_621m, 7.5m, null)]);

        public Task<Result> SalvarAsync(TipoTabelaTributaria tipo, SalvarRegistroTabelaRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> ExcluirAsync(TipoTabelaTributaria tipo, int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
