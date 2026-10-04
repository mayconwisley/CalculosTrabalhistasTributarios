using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

/// <summary>
/// Adaptador SQLite da porta de leitura consumida pela camada Application.
/// As tabelas são pequenas e só mudam pela manutenção ou pela atualização online, por isso são carregadas uma vez
/// e mantidas em memória até <see cref="Invalidar"/> ser chamado.
/// </summary>
public sealed class SqliteTributacaoConsulta(BancoTributario banco) : ITributacaoConsulta, ICacheTabelasTributarias
{
    private TabelasTributarias? _tabelas;
    private int _versao;

    public async Task<Result<PerfilTributario>> ObterPerfilAsync(DateOnly competencia, CancellationToken cancellationToken)
    {
        var tabelas = await ObterTabelasAsync(cancellationToken);
        var data = competencia.ToDateTime(TimeOnly.MinValue);
        var competenciaInss = ObterUltimaCompetencia(tabelas.FaixasInss, data, "INSS");
        var competenciaIrrf = ObterUltimaCompetencia(tabelas.FaixasIrrf, data, "IRRF");
        var competenciaSimplificado = ObterUltimaCompetencia(tabelas.Simplificados, data, "desconto simplificado");
        var competenciaDependente = ObterUltimaCompetencia(tabelas.Dependentes, data, "dedução por dependente");
        var competenciaMinimo = ObterUltimaCompetencia(tabelas.DescontosMinimos, data, "desconto mínimo");
        if (Result.Combinar(competenciaInss, competenciaIrrf, competenciaSimplificado, competenciaDependente, competenciaMinimo) is { Falhou: true } ausente)
            return ausente.Erro;
        var competenciaReducao = ObterUltimaCompetenciaOpcional(tabelas.ReducoesMensais, data);

        var inss = tabelas.FaixasInss.Where(item => item.Competencia == competenciaInss.Valor).Select(item => item.Valor).OrderBy(item => item.Numero).ToArray();
        var irrf = tabelas.FaixasIrrf.Where(item => item.Competencia == competenciaIrrf.Valor).Select(item => item.Valor).OrderBy(item => item.Numero).ToArray();
        var simplificado = tabelas.Simplificados.First(item => item.Competencia == competenciaSimplificado.Valor).Valor;
        var dependente = tabelas.Dependentes.First(item => item.Competencia == competenciaDependente.Valor).Valor;
        var minimo = tabelas.DescontosMinimos.First(item => item.Competencia == competenciaMinimo.Valor).Valor;
        var reducoes = competenciaReducao is null
            ? []
            : tabelas.ReducoesMensais.Where(item => item.Competencia == competenciaReducao).Select(item => item.Valor).OrderBy(item => item.Faixa).ToArray();

        // As tabelas das calculadoras trabalhistas são opcionais: faltar uma delas não impede os cálculos de INSS e IRRF.
        var competenciaSalarioMinimo = ObterUltimaCompetenciaOpcional(tabelas.SalariosMinimos, data);
        var competenciaPlr = ObterUltimaCompetenciaOpcional(tabelas.FaixasPlr, data);
        var competenciaSalarioFamilia = ObterUltimaCompetenciaOpcional(tabelas.FaixasSalarioFamilia, data);
        var competenciaSeguroDesemprego = ObterUltimaCompetenciaOpcional(tabelas.FaixasSeguroDesemprego, data);
        var salarioMinimo = competenciaSalarioMinimo is null ? (decimal?)null : tabelas.SalariosMinimos.First(item => item.Competencia == competenciaSalarioMinimo).Valor;
        var plr = tabelas.FaixasPlr.Where(item => item.Competencia == competenciaPlr).Select(item => item.Valor).OrderBy(item => item.Numero).ToArray();
        var salarioFamilia = tabelas.FaixasSalarioFamilia.Where(item => item.Competencia == competenciaSalarioFamilia).Select(item => item.Valor).OrderBy(item => item.Faixa).ToArray();
        var seguroDesemprego = tabelas.FaixasSeguroDesemprego.Where(item => item.Competencia == competenciaSeguroDesemprego).Select(item => item.Valor).OrderBy(item => item.Numero).ToArray();

        return new PerfilTributario(inss, irrf, simplificado, dependente, minimo, reducoes, salarioMinimo, plr, salarioFamilia, seguroDesemprego);
    }

    public async Task<decimal?> ObterSalarioMinimoAsync(DateOnly competencia, CancellationToken cancellationToken)
    {
        var tabelas = await ObterTabelasAsync(cancellationToken);
        var vigente = ObterUltimaCompetenciaOpcional(tabelas.SalariosMinimos, competencia.ToDateTime(TimeOnly.MinValue));
        return vigente is null ? null : tabelas.SalariosMinimos.First(item => item.Competencia == vigente).Valor;
    }

    public void Invalidar()
    {
        Interlocked.Increment(ref _versao);
        Volatile.Write(ref _tabelas, null);
    }

    private async Task<TabelasTributarias> ObterTabelasAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _tabelas) is { } carregadas)
            return carregadas;

        var versao = Volatile.Read(ref _versao);
        await using var conexao = await banco.AbrirAsync(cancellationToken);

        // GetDecimal converte o texto que o SQLite gera para cada REAL, mantendo a mesma escala decimal lida pelo EF Core até então.
        // A ordenação por Id preserva a ordem física usada antes quando há mais de um registro na mesma competência.
        var tabelas = new TabelasTributarias(
            await conexao.ListarAsync(null, "SELECT Competencia, Faixa, Valor, Porcentagem FROM Inss ORDER BY Id",
                leitor => new Vigencia<FaixaTributaria>(leitor.GetDateTime(0), new FaixaTributaria(leitor.GetInt32(1), leitor.GetDecimal(2), leitor.GetDecimal(3), 0m)), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Faixa, Valor, Porcentagem, Deducao FROM Irrf ORDER BY Id",
                leitor => new Vigencia<FaixaTributaria>(leitor.GetDateTime(0), new FaixaTributaria(leitor.GetInt32(1), leitor.GetDecimal(2), leitor.GetDecimal(3), leitor.GetDecimal(4))), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Valor FROM Simplificado ORDER BY Id",
                leitor => new Vigencia<decimal>(leitor.GetDateTime(0), leitor.GetDecimal(1)), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Valor FROM Dependente ORDER BY Id",
                leitor => new Vigencia<decimal>(leitor.GetDateTime(0), leitor.GetDecimal(1)), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Valor FROM DescontoMinimo ORDER BY Id",
                leitor => new Vigencia<decimal>(leitor.GetDateTime(0), leitor.GetDecimal(1)), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Faixa, LimiteRendimentos, Multiplicador, ValorBase FROM ReducaoMensalIrrf ORDER BY Id",
                leitor => new Vigencia<RegraReducaoMensalIrrf>(leitor.GetDateTime(0), new RegraReducaoMensalIrrf(leitor.GetInt32(1), leitor.GetDecimal(2), leitor.GetDecimal(3), leitor.GetDecimal(4))), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Valor FROM SalarioMinimo ORDER BY Id",
                leitor => new Vigencia<decimal>(leitor.GetDateTime(0), leitor.GetDecimal(1)), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Faixa, Valor, Porcentagem, Deducao FROM Plr ORDER BY Id",
                leitor => new Vigencia<FaixaTributaria>(leitor.GetDateTime(0), new FaixaTributaria(leitor.GetInt32(1), leitor.GetDecimal(2), leitor.GetDecimal(3), leitor.GetDecimal(4))), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Faixa, LimiteRemuneracao, Cota FROM SalarioFamilia ORDER BY Id",
                leitor => new Vigencia<FaixaSalarioFamilia>(leitor.GetDateTime(0), new FaixaSalarioFamilia(leitor.GetInt32(1), leitor.GetDecimal(2), leitor.GetDecimal(3))), cancellationToken),
            await conexao.ListarAsync(null, "SELECT Competencia, Faixa, Valor, Porcentagem, ValorFixo FROM SeguroDesemprego ORDER BY Id",
                leitor => new Vigencia<FaixaTributaria>(leitor.GetDateTime(0), new FaixaTributaria(leitor.GetInt32(1), leitor.GetDecimal(2), leitor.GetDecimal(3), leitor.GetDecimal(4))), cancellationToken));

        // Se houve gravação durante a carga, o resultado atende esta chamada, mas não é guardado.
        if (versao == Volatile.Read(ref _versao))
            Volatile.Write(ref _tabelas, tabelas);
        return tabelas;
    }

    private static Result<DateTime> ObterUltimaCompetencia<T>(IEnumerable<Vigencia<T>> vigencias, DateTime competencia, string tabela) =>
        ObterUltimaCompetenciaOpcional(vigencias, competencia) is { } vigente
        ? vigente
        : Erro.NaoEncontrado(vigencias.Any()
            ? $"Não há tabela de {tabela} cadastrada para {competencia:MM/yyyy}: as tabelas começam em {vigencias.Min(item => item.Competencia):MM/yyyy}. Informe uma competência a partir dessa data."
            : $"Não há tabela de {tabela} cadastrada. Cadastre os valores na tabela ou atualize-a pela internet.");

    private static DateTime? ObterUltimaCompetenciaOpcional<T>(IEnumerable<Vigencia<T>> vigencias, DateTime competencia) =>
        vigencias.Where(item => item.Competencia <= competencia).Max(item => (DateTime?)item.Competencia);

    private sealed record Vigencia<T>(DateTime Competencia, T Valor);

    private sealed record TabelasTributarias(
        Vigencia<FaixaTributaria>[] FaixasInss,
        Vigencia<FaixaTributaria>[] FaixasIrrf,
        Vigencia<decimal>[] Simplificados,
        Vigencia<decimal>[] Dependentes,
        Vigencia<decimal>[] DescontosMinimos,
        Vigencia<RegraReducaoMensalIrrf>[] ReducoesMensais,
        Vigencia<decimal>[] SalariosMinimos,
        Vigencia<FaixaTributaria>[] FaixasPlr,
        Vigencia<FaixaSalarioFamilia>[] FaixasSalarioFamilia,
        Vigencia<FaixaTributaria>[] FaixasSeguroDesemprego);
}
