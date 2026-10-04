namespace CalculosTrabalhistasTributarios.Domain.Comum;

/// <summary>
/// Resultado de uma operação que pode falhar de forma esperada (Result Pattern): em vez de lançar exceção para dados
/// inválidos ou tabelas ausentes, a operação devolve o <see cref="Comum.Erro"/>. Exceções ficam para falhas inesperadas.
/// </summary>
public class Result
{
    private static readonly Result SucessoUnico = new(null);
    private readonly Erro? _erro;

    protected Result(Erro? erro) => _erro = erro;

    public bool Sucesso => _erro is null;
    public bool Falhou => _erro is not null;

    /// <summary>O erro da falha; consultar em um sucesso é erro de programação.</summary>
    public Erro Erro => _erro ?? throw new InvalidOperationException("Um resultado de sucesso não tem erro.");

    public static Result Ok() => SucessoUnico;
    public static Result Falha(Erro erro) => new(erro);
    public static Result<T> Ok<T>(T valor) => Result<T>.Ok(valor);

    /// <summary>O primeiro erro entre as validações, ou sucesso quando todas passam.</summary>
    public static Result Combinar(params Result[] resultados) => resultados.FirstOrDefault(resultado => resultado.Falhou) ?? SucessoUnico;

    public static implicit operator Result(Erro erro) => Falha(erro);
}
