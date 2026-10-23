namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Preferências gravadas no armazenamento privado do aparelho, equivalentes ao settings.json do desktop.</summary>
public interface IPreferenciasMobile
{
    /// <summary>Chaves dos atalhos favoritos, na ordem em que foram marcados.</summary>
    IReadOnlyList<string> Favoritos { get; }
    void DefinirFavorito(string chave, bool favorito);

    bool AtualizarTabelasAoAbrir { get; set; }

    /// <summary>Quando as tabelas foram atualizadas pela internet pela última vez, na abertura ou por Atualizar agora.</summary>
    DateTime? HorarioAtualizacaoTabelas { get; set; }

    TemaMobile Tema { get; set; }
}
