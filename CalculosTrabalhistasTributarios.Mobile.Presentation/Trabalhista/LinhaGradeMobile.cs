using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class LinhaGradeMobile : ObservableObject
{
    private readonly Dictionary<string, CampoMobile> _campos;
    private string _titulo = "";

    public LinhaGradeMobile(IEnumerable<ColunaGradeMobile> colunas)
    {
        _campos = colunas.ToDictionary(coluna => coluna.Chave,
            coluna => new CampoMobile(coluna.Chave, coluna.Rotulo, coluna.Dica, coluna.Tipo, coluna.Valor, coluna.Opcoes));
        Campos = [.. _campos.Values];
    }

    public string Titulo { get => _titulo; internal set => SetProperty(ref _titulo, value); }
    public IReadOnlyList<CampoMobile> Campos { get; }
    public CampoMobile Campo(string chave) => _campos[chave];
}
