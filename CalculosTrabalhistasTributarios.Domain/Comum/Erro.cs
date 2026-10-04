namespace CalculosTrabalhistasTributarios.Domain.Comum;

/// <summary>Falha esperada de uma operação, com a mensagem que o usuário lê para saber o que corrigir.</summary>
public sealed record Erro(TipoErro Tipo, string Mensagem)
{
    public static Erro Validacao(string mensagem) => new(TipoErro.Validacao, mensagem);

    public static Erro NaoEncontrado(string mensagem) => new(TipoErro.NaoEncontrado, mensagem);

    public static Erro Indisponivel(string mensagem) => new(TipoErro.Indisponivel, mensagem);

    public override string ToString() => Mensagem;
}
