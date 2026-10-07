using CalculosTrabalhistasTributarios.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>Favoritas gravadas nas preferências do usuário, em settings.json.</summary>
public sealed class FavoritosAtalhosConfiguracao : IFavoritosAtalhos
{
    public IReadOnlyList<string> Chaves => ConfiguracoesUsuario.Atuais.Favoritos;

    public void Definir(string chave, bool favorito)
    {
        if (ConfiguracoesUsuario.Atuais.Favoritos.Contains(chave) == favorito)
            return;
        ConfiguracoesUsuario.Alterar(configuracoes =>
        {
            if (favorito) configuracoes.Favoritos.Add(chave);
            else configuracoes.Favoritos.Remove(chave);
        });
    }
}
