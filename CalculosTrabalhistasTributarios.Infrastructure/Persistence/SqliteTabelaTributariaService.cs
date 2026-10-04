using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

public sealed class SqliteTabelaTributariaService(BancoTributario banco, ICacheTabelasTributarias cache) : ITabelaTributariaService
{
    private const string RegistroNaoEncontrado = "O registro não foi encontrado: ele pode ter sido excluído. Atualize a lista e tente de novo.";
    private static readonly DefinicaoTabela Inss = new("Inss", TemFaixa: true, "Valor", "Porcentagem", null, "Faixa INSS não encontrada.");
    private static readonly DefinicaoTabela Irrf = new("Irrf", TemFaixa: true, "Valor", "Porcentagem", "Deducao", "Faixa IRRF não encontrada.");
    private static readonly DefinicaoTabela Simplificado = new("Simplificado", TemFaixa: false, "Valor", null, null, null);
    private static readonly DefinicaoTabela Dependente = new("Dependente", TemFaixa: false, "Valor", null, null, null);
    private static readonly DefinicaoTabela DescontoMinimo = new("DescontoMinimo", TemFaixa: false, "Valor", null, null, null);
    private static readonly DefinicaoTabela ReducaoMensalIrrf = new("ReducaoMensalIrrf", TemFaixa: true, "LimiteRendimentos", "Multiplicador", "ValorBase", "Faixa de redução mensal não encontrada.");
    private static readonly DefinicaoTabela SalarioMinimo = new("SalarioMinimo", TemFaixa: false, "Valor", null, null, null);
    private static readonly DefinicaoTabela SalarioFamilia = new("SalarioFamilia", TemFaixa: true, "LimiteRemuneracao", null, "Cota", "Faixa do salário-família não encontrada.");
    private static readonly DefinicaoTabela Plr = new("Plr", TemFaixa: true, "Valor", "Porcentagem", "Deducao", "Faixa da PLR não encontrada.");
    // A inflação de um mês pode ser negativa (deflação); a taxa legal, não (Código Civil, art. 406, § 3º).
    private static readonly DefinicaoTabela Inpc = new("Inpc", TemFaixa: false, "Valor", null, null, null, AceitaNegativo: true);
    private static readonly DefinicaoTabela Ipca = new("Ipca", TemFaixa: false, "Valor", null, null, null, AceitaNegativo: true);
    private static readonly DefinicaoTabela TaxaLegal = new("TaxaLegal", TemFaixa: false, "Valor", null, null, null);
    private static readonly DefinicaoTabela Selic = new("Selic", TemFaixa: false, "Valor", null, null, null);
    private static readonly DefinicaoTabela IpcaE = new("IpcaE", TemFaixa: false, "Valor", null, null, null, AceitaNegativo: true);
    private static readonly DefinicaoTabela Tr = new("Tr", TemFaixa: false, "Valor", null, null, null);
    private static readonly DefinicaoTabela SeguroDesemprego = new("SeguroDesemprego", TemFaixa: true, "Valor", "Porcentagem", "ValorFixo", "Faixa do seguro-desemprego não encontrada.");

    public async Task<IReadOnlyList<RegistroTabelaDto>> ListarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken)
    {
        var tabela = ObterDefinicao(tipo);
        await using var conexao = await banco.AbrirAsync(cancellationToken);
        return await conexao.ListarAsync(null, tabela.SqlListar, leitor => new RegistroTabelaDto(
            leitor.GetInt32(0),
            DateOnly.FromDateTime(leitor.GetDateTime(1)),
            tabela.TemFaixa ? leitor.GetInt32(2) : null,
            leitor.GetDecimal(3),
            tabela.ColunaAliquota is null ? null : leitor.GetDecimal(4),
            tabela.ColunaDeducao is null ? null : leitor.GetDecimal(5)), cancellationToken);
    }

    public async Task<Result> SalvarAsync(TipoTabelaTributaria tipo, SalvarRegistroTabelaRequest request, CancellationToken cancellationToken)
    {
        if (Validar(tipo, request) is { Falhou: true } invalido)
            return invalido;
        var tabela = ObterDefinicao(tipo);
        await using var conexao = await banco.AbrirAsync(cancellationToken);
        await using var comando = conexao.CriarComando(null, request.Id == 0 ? tabela.SqlIncluir : tabela.SqlAlterar,
            ("$competencia", request.Competencia.ToDateTime(TimeOnly.MinValue)), ("$valor", (double)request.Valor));
        if (request.Id != 0) comando.Parameters.AddWithValue("$id", request.Id);
        if (tabela.TemFaixa) comando.Parameters.AddWithValue("$faixa", request.Faixa!.Value);
        if (tabela.ColunaAliquota is not null) comando.Parameters.AddWithValue("$aliquota", (double)request.Aliquota!.Value);
        if (tabela.ColunaDeducao is not null) comando.Parameters.AddWithValue("$deducao", (double)request.Deducao!.Value);

        if (await comando.ExecuteNonQueryAsync(cancellationToken) == 0)
            return Erro.NaoEncontrado(tabela.MensagemNaoEncontrada ?? RegistroNaoEncontrado);
        cache.Invalidar();
        return Result.Ok();
    }

    public async Task<Result> ExcluirAsync(TipoTabelaTributaria tipo, int id, CancellationToken cancellationToken)
    {
        var tabela = ObterDefinicao(tipo);
        await using var conexao = await banco.AbrirAsync(cancellationToken);
        if (await conexao.ExecutarAsync(null, tabela.SqlExcluir, cancellationToken, ("$id", id)) == 0)
            return Erro.NaoEncontrado(RegistroNaoEncontrado);
        cache.Invalidar();
        return Result.Ok();
    }

    private static DefinicaoTabela ObterDefinicao(TipoTabelaTributaria tipo) => tipo switch
    {
        TipoTabelaTributaria.Inss => Inss,
        TipoTabelaTributaria.Irrf => Irrf,
        TipoTabelaTributaria.Simplificado => Simplificado,
        TipoTabelaTributaria.Dependente => Dependente,
        TipoTabelaTributaria.DescontoMinimo => DescontoMinimo,
        TipoTabelaTributaria.ReducaoMensalIrrf => ReducaoMensalIrrf,
        TipoTabelaTributaria.SalarioMinimo => SalarioMinimo,
        TipoTabelaTributaria.SalarioFamilia => SalarioFamilia,
        TipoTabelaTributaria.Plr => Plr,
        TipoTabelaTributaria.Inpc => Inpc,
        TipoTabelaTributaria.Ipca => Ipca,
        TipoTabelaTributaria.TaxaLegal => TaxaLegal,
        TipoTabelaTributaria.Selic => Selic,
        TipoTabelaTributaria.IpcaE => IpcaE,
        TipoTabelaTributaria.Tr => Tr,
        TipoTabelaTributaria.SeguroDesemprego => SeguroDesemprego,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tabela desconhecida.")
    };

    private static Result Validar(TipoTabelaTributaria tipo, SalvarRegistroTabelaRequest request)
    {
        var definicao = ObterDefinicao(tipo);
        if ((request.Valor < 0m && !definicao.AceitaNegativo) || request.Aliquota is < 0m || request.Deducao is < 0m ||
            (definicao.TemFaixa && request.Faixa is null or <= 0) ||
            (definicao.ColunaAliquota is not null && request.Aliquota is null) ||
            (definicao.ColunaDeducao is not null && request.Deducao is null))
            return Erro.Validacao("Os dados informados são inválidos.");
        return Result.Ok();
    }

    /// <summary>Colunas de uma tabela tributária; o SQL é montado uma vez a partir de nomes fixos, nunca de dados do usuário.</summary>
    private sealed record DefinicaoTabela(string Nome, bool TemFaixa, string ColunaValor, string? ColunaAliquota, string? ColunaDeducao, string? MensagemNaoEncontrada, bool AceitaNegativo = false)
    {
        public string SqlListar { get; } = $"SELECT Id, Competencia, {(TemFaixa ? "Faixa" : "NULL")}, {ColunaValor}, {ColunaAliquota ?? "NULL"}, {ColunaDeducao ?? "NULL"} FROM {Nome} ORDER BY Competencia DESC{(TemFaixa ? ", Faixa" : "")}";
        public string SqlIncluir { get; } = $"INSERT INTO {Nome} ({string.Join(", ", Colunas(TemFaixa, ColunaValor, ColunaAliquota, ColunaDeducao).Select(item => item.Coluna))}) VALUES ({string.Join(", ", Colunas(TemFaixa, ColunaValor, ColunaAliquota, ColunaDeducao).Select(item => item.Parametro))})";
        public string SqlAlterar { get; } = $"UPDATE {Nome} SET {string.Join(", ", Colunas(TemFaixa, ColunaValor, ColunaAliquota, ColunaDeducao).Select(item => $"{item.Coluna} = {item.Parametro}"))} WHERE Id = $id";
        public string SqlExcluir { get; } = $"DELETE FROM {Nome} WHERE Id = $id";

        private static IEnumerable<(string Coluna, string Parametro)> Colunas(bool temFaixa, string colunaValor, string? colunaAliquota, string? colunaDeducao)
        {
            yield return ("Competencia", "$competencia");
            if (temFaixa) yield return ("Faixa", "$faixa");
            yield return (colunaValor, "$valor");
            if (colunaAliquota is not null) yield return (colunaAliquota, "$aliquota");
            if (colunaDeducao is not null) yield return (colunaDeducao, "$deducao");
        }
    }
}
