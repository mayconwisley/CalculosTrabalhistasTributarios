namespace CalculosTrabalhistasTributarios.Domain.Comum;

/// <summary>Resultado com o valor produzido pela operação quando ela dá certo.</summary>
public sealed class Result<T> : Result
{
    private readonly T _valor;

    private Result(T valor) : base(null) => _valor = valor;

    private Result(Erro erro) : base(erro) => _valor = default!;

    /// <summary>O valor do sucesso; consultar em uma falha é erro de programação.</summary>
    public T Valor => Sucesso ? _valor : throw new InvalidOperationException($"O resultado falhou e não tem valor: {Erro.Mensagem}");

    public static Result<T> Ok(T valor) => new(valor);
    public static new Result<T> Falha(Erro erro) => new(erro);

    public static implicit operator Result<T>(T valor) => Ok(valor);
    public static implicit operator Result<T>(Erro erro) => Falha(erro);

    /// <summary>Transforma o valor do sucesso; a falha passa adiante como está.</summary>
    public Result<TNovo> Map<TNovo>(Func<T, TNovo> transformar) => Sucesso ? Result<TNovo>.Ok(transformar(_valor)) : Result<TNovo>.Falha(Erro);

    /// <summary>Encadeia outra operação que também pode falhar.</summary>
    public Result<TNovo> Bind<TNovo>(Func<T, Result<TNovo>> continuar) => Sucesso ? continuar(_valor) : Result<TNovo>.Falha(Erro);

    public TSaida Match<TSaida>(Func<T, TSaida> sucesso, Func<Erro, TSaida> falha) => Sucesso ? sucesso(_valor) : falha(Erro);
}
