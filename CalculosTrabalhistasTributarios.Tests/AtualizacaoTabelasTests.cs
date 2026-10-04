using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;
using Microsoft.Data.Sqlite;
using System.IO;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Leitura das páginas do seguro-desemprego e do desconto mínimo, com trechos no formato de cada site, e a gravação
/// das tabelas atualizadas. Cada teste grava numa cópia própria do banco, para não mudar os valores dos outros testes.
/// </summary>
public sealed class AtualizacaoTabelasTests : IDisposable
{
    private const decimal AcimaDe = TabelaIrrfPublicada.LimiteUltimaFaixa;

    private static readonly FaixaSeguroDesempregoPublicada[] Faixas2026 = [new(2_222.17m, 80m, 0m), new(3_703.99m, 50m, 1_777.74m), new(AcimaDe, 0m, 2_518.65m)];

    // Tabela do ano seguinte, com valores que fecham entre si, para ver a gravação de uma competência que o banco ainda não tem.
    private static readonly FaixaSeguroDesempregoPublicada[] Faixas2027 = [new(2_300.00m, 80m, 0m), new(3_800.00m, 50m, 1_840.00m), new(AcimaDe, 0m, 2_590.00m)];

    private const string PaginaMinisterio = """
        <p><b>Tabela de Faixas de salários médios e cálculo do benefício Seguro-Desemprego</b></p>
        <p>Período: Ano de 2024</p>
        <table><tbody>
        <tr><td><p><b>Faixas de Salário Médio dos 3 meses anteriores à dispensa</b></p></td><td><p><b>Cálculo da Parcela</b></p></td></tr>
        <tr><td><p>até R$ 2.041,39</p></td><td><p>multiplica-se o salário médio por 0,8</p></td></tr>
        <tr><td><p>de R$&nbsp;2.041,40 até R$ 3.402,65</p></td><td><p>o que exceder a R$ 2.041,39 multiplica-se por 0,5 e soma-se com R$ 1.633,10</p></td></tr>
        <tr><td><p>acima de R$ 3.402,65</p></td><td><p>o valor será invariável de R$ 2.313,74</p></td></tr>
        </tbody></table>
        <p><b><strong><span>Esta tabela entra em vigor a partir do dia 11/01/2024</span></strong></b></p>
        """;

    private readonly string _caminhoBanco = Path.Combine(AppContext.BaseDirectory, "BancoDados", $"atualizacao-{Guid.NewGuid():N}.db");
    private readonly CacheContado _cache = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_caminhoBanco);
    }

    [Fact]
    public async Task Debit_le_a_tabela_mais_recente_do_historico()
    {
        using var cliente = PaginasFixas.Cliente(("www.debit.com.br", PaginaDebit(2026, Faixas2026) + PaginaDebit(2025, [new(2_138.76m, 80m, 0m), new(3_564.96m, 50m, 1_711.01m), new(AcimaDe, 0m, 2_424.11m)])));
        var tabela = await new FonteSeguroDesempregoDebit().ObterAsync(cliente, default);

        tabela.Validar();
        Assert.Equal(new DateOnly(2026, 1, 1), tabela.Competencia);
        Assert.Equal(Faixas2026, tabela.Faixas);
    }

    [Fact]
    public async Task Idinheiro_le_o_ano_mais_recente_com_os_mesmos_valores_do_debit()
    {
        using var cliente = PaginasFixas.Cliente(("www.idinheiro.com.br", PaginaIdinheiro(2025, 2_313.74m) + PaginaIdinheiro(2026, 2_518.65m)));
        var tabela = await new FonteSeguroDesempregoIdinheiro().ObterAsync(cliente, default);

        tabela.Validar();
        Assert.Equal(new DateOnly(2026, 1, 1), tabela.Competencia);
        Assert.Equal(Faixas2026, tabela.Faixas);
    }

    [Fact]
    public async Task Ministerio_do_trabalho_le_a_vigencia_abaixo_da_tabela()
    {
        using var cliente = PaginasFixas.Cliente(("www.gov.br", PaginaMinisterio));
        var tabela = await new FonteSeguroDesempregoGovBr().ObterAsync(cliente, default);

        tabela.Validar();
        Assert.Equal(new DateOnly(2024, 1, 1), tabela.Competencia);
        Assert.Equal([new(2_041.39m, 80m, 0m), new(3_402.65m, 50m, 1_633.10m), new(AcimaDe, 0m, 2_313.74m)], tabela.Faixas);
    }

    [Fact]
    public void Tabela_com_valor_maximo_de_outro_ano_nao_fecha_a_conta()
    {
        // Um site publicou 2025 com o valor máximo de 2024: 1.711,01 + 50% de (3.564,96 − 2.138,76) dá 2.424,11, e não 2.313,74.
        var tabela = new TabelaSeguroDesempregoPublicada(new DateOnly(2025, 1, 1), [new(2_138.76m, 80m, 0m), new(3_564.96m, 50m, 1_711.01m), new(AcimaDe, 0m, 2_313.74m)]);

        Assert.Throws<InvalidOperationException>(tabela.Validar);
    }

    [Fact]
    public async Task Seguro_desemprego_grava_a_tabela_confirmada_pelas_duas_fontes_alternativas()
    {
        var banco = await CriarBancoAsync();
        var atualizador = new AtualizadorSeguroDesemprego(
            () => PaginasFixas.Cliente(("www.gov.br", PaginaMinisterio), ("www.debit.com.br", PaginaDebit(2027, Faixas2027)), ("www.idinheiro.com.br", PaginaIdinheiro(2027, Faixas2027))),
            [new FonteSeguroDesempregoGovBr(), new FonteSeguroDesempregoDebit(), new FonteSeguroDesempregoIdinheiro()], banco, _cache);

        var resultado = await atualizador.AtualizarAsync(default);

        Assert.Equal(new DateOnly(2027, 1, 1), resultado.Competencia);
        Assert.False(resultado.Oficial);
        Assert.Equal(["debit.com.br", "idinheiro.com.br"], resultado.Fontes);
        Assert.Contains("A página oficial ainda mostra a tabela de 01/2024.", resultado.Observacoes);
        Assert.Equal(Faixas2027, await ListarSeguroDesempregoAsync(banco, new DateOnly(2027, 1, 1)));
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Planalto_le_o_art_67_em_vigor_e_o_inicio_dos_efeitos_da_lei()
    {
        using var cliente = PaginasFixas.ClienteLatin1("www.planalto.gov.br", PaginaLei(RedacaoRevogada + Artigo67("R$ 10,00 (dez reais)")));
        var publicado = await new FonteDescontoMinimoPlanalto().ObterAsync(cliente, default);

        Assert.Equal(new DescontoMinimoPublicado(new DateOnly(1997, 1, 1), 10m, "art. 67 da Lei 9.430/1996"), publicado);
    }

    [Fact]
    public async Task Planalto_com_nova_redacao_pede_o_cadastro_manual()
    {
        // A página dá só o ano da lei que alterou o artigo, e não a vigência do novo valor. "Redação" em Latin-1 também confere
        // a leitura da página sem o charset: lida como UTF-8, ela viraria "Reda��o" e a alteração passaria despercebida.
        using var cliente = PaginasFixas.ClienteLatin1("www.planalto.gov.br", PaginaLei(Artigo67("R$ 20,00 (vinte reais)", " (Redação dada pela Lei nº 15.999, de 2027)")));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => new FonteDescontoMinimoPlanalto().ObterAsync(cliente, default));
        Assert.Contains("nova redação (Lei nº 15.999, de 2027)", erro.Message);
    }

    [Fact]
    public async Task Desconto_minimo_igual_ao_da_lei_so_e_conferido()
    {
        var banco = await CriarBancoAsync();
        var antes = await ListarDescontoMinimoAsync(banco);

        var resultado = await AtualizadorDescontoMinimo(banco).AtualizarAsync(default);

        Assert.Equal(0, resultado.QuantidadeFaixas);
        Assert.True(resultado.Oficial);
        Assert.Contains("O art. 67 da Lei 9.430/1996 dispensa a retenção do IRRF de até R$ 10,00 desde 01/1997.", resultado.Observacoes);
        Assert.All(antes, registro => Assert.Equal(10m, registro.Item2));
        Assert.Equal(antes, await ListarDescontoMinimoAsync(banco));
        Assert.Equal(0, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Desconto_minimo_diferente_da_lei_e_corrigido_sem_mudar_competencia_futura()
    {
        var banco = await CriarBancoAsync();
        var competencias = (await ListarDescontoMinimoAsync(banco)).Select(registro => registro.Item1).ToArray();
        await ExecutarAsync(banco, "UPDATE DescontoMinimo SET Valor = 15");
        await ExecutarAsync(banco, "INSERT INTO DescontoMinimo (Competencia, Valor) VALUES ($competencia, 20)", ("$competencia", new DateTime(2099, 1, 1)));

        var resultado = await AtualizadorDescontoMinimo(banco).AtualizarAsync(default);

        Assert.Equal(competencias.Length, resultado.QuantidadeFaixas);
        Assert.Equal([.. competencias.Select(competencia => (competencia, 10m)), (new DateOnly(2099, 1, 1), 20m)], await ListarDescontoMinimoAsync(banco));
        Assert.Equal(1, _cache.Invalidacoes);
    }

    [Fact]
    public async Task Desconto_minimo_sem_registros_entra_desde_a_vigencia_da_lei()
    {
        var banco = await CriarBancoAsync();
        await ExecutarAsync(banco, "DELETE FROM DescontoMinimo");

        var resultado = await AtualizadorDescontoMinimo(banco).AtualizarAsync(default);

        Assert.Equal(1, resultado.QuantidadeFaixas);
        Assert.Equal([(new DateOnly(1997, 1, 1), 10m)], await ListarDescontoMinimoAsync(banco));
    }

    private AtualizadorDescontoMinimo AtualizadorDescontoMinimo(BancoTributario banco) => new(
        () => PaginasFixas.ClienteLatin1("www.planalto.gov.br", PaginaLei(Artigo67("R$ 10,00 (dez reais)"))), [new FonteDescontoMinimoPlanalto()], banco, _cache);

    private async Task<BancoTributario> CriarBancoAsync()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "BancoDados", "base.db"), _caminhoBanco);
        var banco = new BancoTributario($"Data Source={_caminhoBanco}");
        await new InicializadorBancoTributario(banco).InicializarAsync(default);
        return banco;
    }

    private static async Task ExecutarAsync(BancoTributario banco, string sql, params (string Nome, object Valor)[] parametros)
    {
        await using var conexao = await banco.AbrirAsync(default);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nome, valor) in parametros)
            comando.Parameters.AddWithValue(nome, valor);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<List<(DateOnly, decimal)>> ListarDescontoMinimoAsync(BancoTributario banco)
    {
        await using var conexao = await banco.AbrirAsync(default);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT Competencia, Valor FROM DescontoMinimo ORDER BY Competencia";
        await using var leitor = await comando.ExecuteReaderAsync();
        var registros = new List<(DateOnly, decimal)>();
        while (await leitor.ReadAsync())
            registros.Add((DateOnly.FromDateTime(leitor.GetDateTime(0)), leitor.GetDecimal(1)));
        return registros;
    }

    private static async Task<List<FaixaSeguroDesempregoPublicada>> ListarSeguroDesempregoAsync(BancoTributario banco, DateOnly competencia)
    {
        await using var conexao = await banco.AbrirAsync(default);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT Valor, Porcentagem, ValorFixo FROM SeguroDesemprego WHERE Competencia = $competencia ORDER BY Faixa";
        comando.Parameters.AddWithValue("$competencia", competencia.ToDateTime(TimeOnly.MinValue));
        await using var leitor = await comando.ExecuteReaderAsync();
        var faixas = new List<FaixaSeguroDesempregoPublicada>();
        while (await leitor.ReadAsync())
            faixas.Add(new(Math.Round(leitor.GetDecimal(0), 2), leitor.GetDecimal(1), leitor.GetDecimal(2)));
        return faixas;
    }

    // Trechos no formato de cada site: o debit.com.br escreve "Mais de R$ 2222.17", sem o formato brasileiro, antes do limite.
    private static string PaginaDebit(int ano, FaixaSeguroDesempregoPublicada[] faixas) => $"""
        <div class="card"><div class="card-body pb-0"><h2>Seguro desemprego ref. 01/{ano}</h2></div><div class="card-body"><table class="table">
        <thead><tr><th>Faixas de salário médio</th><th>Valor da parcela</th></tr></thead><tbody>
        <tr><td>Até: R$ {Moeda(faixas[0].LimiteMedia)}</td><td>Multiplica-se o salário médio por 0,8 (80%).</td></tr>
        <tr><td>Mais de R$ {faixas[0].LimiteMedia.ToString(System.Globalization.CultureInfo.InvariantCulture)} <br>Até R$ {Moeda(faixas[1].LimiteMedia)}</td><td>O que exceder a R$ {Moeda(faixas[0].LimiteMedia)} multiplica-se<br> por 0,5 (50%) e soma-se a R$ {Moeda(faixas[1].ValorFixo)}.</td></tr>
        <tr><td>Acima de R$ {faixas[1].LimiteMedia.ToString(System.Globalization.CultureInfo.InvariantCulture)}</td><td>O valor da parcela<br> será R$ {Moeda(faixas[2].ValorFixo)} invariavelmente.</td></tr>
        </tbody></table></div></div>
        """;

    private static string PaginaIdinheiro(int ano, decimal maximo) => PaginaIdinheiro(ano, [Faixas2026[0], Faixas2026[1], Faixas2026[2] with { ValorFixo = maximo }]);

    private static string PaginaIdinheiro(int ano, FaixaSeguroDesempregoPublicada[] faixas) => $"""
        <h5 class="wp-block-heading">Tabela do Seguro Desemprego {ano}</h5><figure class="wp-block-table"><table>
        <thead><tr><th>Faixa de salário médio</th><th>Valor da parcela</th></tr></thead><tbody>
        <tr><td><span>Até </span>R$ {Moeda(faixas[0].LimiteMedia)}</td><td><span>Multiplicar</span> o salário médio por 0,8</td></tr>
        <tr><td><span>De </span>R$ {Moeda(faixas[0].LimiteMedia + 0.01m)} até R$ {Moeda(faixas[1].LimiteMedia)}</td><td><span>Multiplicar </span>o que exceder R$ {Moeda(faixas[0].LimiteMedia)} por 0,5 e adicionar R$ {Moeda(faixas[1].ValorFixo)}</td></tr>
        <tr><td><span>Acima </span>de R$ {Moeda(faixas[1].LimiteMedia)}</td><td>R$ {Moeda(faixas[2].ValorFixo)}, sem variações</td></tr>
        </tbody></table></figure>
        """;

    // Texto compilado da lei no Planalto: as redações revogadas ficam riscadas antes da que está em vigor.
    private const string RedacaoRevogada = """
        <p ALIGN="JUSTIFY"><strike><small><font face="Arial">Art. 67. Fica dispensada a retenção de imposto de renda, de valor igual ou inferior a R$ 5,00 (cinco reais).</font></small></strike></p>
        """;

    private static string Artigo67(string valor, string nota = "") => $"""
        <p ALIGN="CENTER"><strong><small><font face="Arial">Dispensa de Retenção de Imposto de
        Renda</font></small></strong></p>
        <p ALIGN="JUSTIFY" style="text-indent: 1cm"><small><font face="Arial">
        <a name="art67"></a>Art.&nbsp;67.&nbsp;Fica dispensada a retenção de imposto de renda, de valor igual ou
        inferior a {valor}, incidente na fonte sobre rendimentos que devam integrar a
        base de cálculo do imposto devido na declaração de ajuste anual.{nota}</font></small></p>
        """;

    private static string PaginaLei(string artigo) => $"""
        <html><head><title>L9430</title></head><body>
        {artigo}
        <p ALIGN="CENTER"><strong><small><font face="Arial">Vigência</font></small></strong></p>
        <p ALIGN="JUSTIFY"><small><font face="Arial"><a name="art87"></a>Art.&nbsp;87.&nbsp;Esta Lei entra em vigor na data da sua publicação, produzindo efeitos
        financeiros a partir de 1º de janeiro de 1997.</font></small></p>
        </body></html>
        """;

    private static string Moeda(decimal valor) => valor.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    private sealed class CacheContado : ICacheTabelasTributarias
    {
        public int Invalidacoes { get; private set; }

        public void Invalidar() => Invalidacoes++;
    }
}
