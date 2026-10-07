namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

/// <summary>Chaves dos cartões favoritos da tela inicial, na ordem em que foram marcados.</summary>
public interface IFavoritosAtalhos
{
    IReadOnlyList<string> Chaves { get; }
    void Definir(string chave, bool favorito);
}
