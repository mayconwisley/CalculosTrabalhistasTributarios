namespace CalculosTrabalhistasTributarios.Domain.Comum;

/// <summary>Natureza de uma falha esperada, para quem a recebe decidir como apresentá-la.</summary>
public enum TipoErro
{
    /// <summary>Dado informado inválido ou fora das regras: o usuário corrige e tenta de novo.</summary>
    Validacao,

    /// <summary>Informação necessária ausente, como uma tabela não cadastrada para a competência.</summary>
    NaoEncontrado,

    /// <summary>Uma fonte externa não respondeu ou respondeu fora do esperado, como os sites das tabelas oficiais.</summary>
    Indisponivel
}
