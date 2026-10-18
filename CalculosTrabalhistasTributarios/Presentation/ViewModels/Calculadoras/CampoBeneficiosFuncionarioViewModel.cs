using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoBeneficiosFuncionarioViewModel : CampoViewModel
{
    public ObservableCollection<LinhaBeneficioFuncionarioViewModel> Linhas { get; } = [];
    public RelayCommand AdicionarCommand { get; }
    public RelayCommand RemoverCommand { get; }

    public CampoBeneficiosFuncionarioViewModel() : base("Benefícios detalhados", "Até 30 benefícios opcionais; informe custo e participação separadamente. Não repita o valor já informado no total agregado.")
    {
        AdicionarCommand = new(_ => Linhas.Add(new()), _ => Linhas.Count < Domain.Trabalhista.CustoFuncionario.CalculadoraCustoFuncionario.LimiteBeneficios);
        RemoverCommand = new(item => { if (item is LinhaBeneficioFuncionarioViewModel linha) Linhas.Remove(linha); });
        Linhas.CollectionChanged += (_, _) => AdicionarCommand.RaiseCanExecuteChanged();
    }

    public Result<IReadOnlyList<BeneficioFuncionario>> Ler()
    {
        if (Linhas.Count > Domain.Trabalhista.CustoFuncionario.CalculadoraCustoFuncionario.LimiteBeneficios)
            return Erro.Validacao("Benefícios detalhados: informe no máximo 30 linhas.");
        var leitor = new LeitorFormulario();
        var itens = Linhas.Select(linha => new BeneficioFuncionario(linha.Descricao.Valor.Trim(), leitor.Moeda(linha.Custo), leitor.Moeda(linha.Participacao))).ToArray();
        if (leitor.Erro is { } erro) { MensagemErro = erro.Mensagem; return erro; }
        MensagemErro = null;
        return itens;
    }

    public string Exportar() => JsonSerializer.Serialize(Linhas.Select(linha => new LinhaSalva(linha.Descricao.Valor, linha.Custo.Valor, linha.Participacao.Valor)).ToArray());

    public static bool Valido(string json)
    {
        if (json.Length > 50_000) return false;
        try
        {
            var linhas = JsonSerializer.Deserialize<LinhaSalva[]>(json);
            return linhas is { Length: <= 30 } && linhas.All(linha => linha is not null && linha.Descricao is { Length: <= 100 }
                && linha.Custo is { Length: <= 100 } && linha.Participacao is { Length: <= 100 });
        }
        catch (JsonException) { return false; }
    }

    public void Importar(string json)
    {
        if (!Valido(json)) return;
        var linhas = JsonSerializer.Deserialize<LinhaSalva[]>(json) ?? [];
        Linhas.Clear();
        foreach (var linha in linhas)
        {
            var item = new LinhaBeneficioFuncionarioViewModel();
            item.Descricao.Valor = linha.Descricao;
            item.Custo.Valor = linha.Custo;
            item.Participacao.Valor = linha.Participacao;
            Linhas.Add(item);
        }
    }

    private sealed record LinhaSalva(string Descricao, string Custo, string Participacao);
}
