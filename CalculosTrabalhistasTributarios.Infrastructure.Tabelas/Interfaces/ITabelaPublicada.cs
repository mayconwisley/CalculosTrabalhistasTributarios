namespace CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

public interface ITabelaPublicada<in TTabela>
{
    DateOnly Competencia { get; }

    /// <summary>Lança <see cref="InvalidOperationException"/> se a página trouxe uma estrutura que não corresponde à tabela.</summary>
    void Validar();

    /// <summary>Compara os valores que todas as fontes publicam, para confirmar a tabela de uma fonte com outra.</summary>
    bool TemMesmosValores(TTabela outra);
}
