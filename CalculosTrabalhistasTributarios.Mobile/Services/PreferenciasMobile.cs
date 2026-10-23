using System.Text.Json;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class PreferenciasMobile : IPreferenciasMobile
{
    private const string ChaveFavoritos = "atalhos.favoritos";
    private const string ChaveAtualizar = "tabelas.atualizarAoAbrir";
    private const string ChaveHorario = "tabelas.horarioAtualizacao";
    private const string ChaveTema = "aparencia.tema";
    private readonly List<string> _favoritos;

    public PreferenciasMobile()
    {
        try { _favoritos = JsonSerializer.Deserialize<List<string>>(Preferences.Default.Get(ChaveFavoritos, "[]")) ?? []; }
        catch (JsonException) { _favoritos = []; }
    }

    public IReadOnlyList<string> Favoritos => _favoritos;

    public void DefinirFavorito(string chave, bool favorito)
    {
        _favoritos.Remove(chave);
        if (favorito) _favoritos.Add(chave);
        Preferences.Default.Set(ChaveFavoritos, JsonSerializer.Serialize(_favoritos));
    }

    public bool AtualizarTabelasAoAbrir
    {
        get => Preferences.Default.Get(ChaveAtualizar, true);
        set => Preferences.Default.Set(ChaveAtualizar, value);
    }

    public DateTime? HorarioAtualizacaoTabelas
    {
        get => DateTime.TryParse(Preferences.Default.Get(ChaveHorario, string.Empty), null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var horario) ? horario : null;
        set => Preferences.Default.Set(ChaveHorario, value?.ToString("O") ?? string.Empty);
    }

    public TemaMobile Tema
    {
        get => Enum.IsDefined((TemaMobile)Preferences.Default.Get(ChaveTema, 0))
            ? (TemaMobile)Preferences.Default.Get(ChaveTema, 0) : TemaMobile.Automatico;
        set => Preferences.Default.Set(ChaveTema, (int)value);
    }
}
