using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoVinculosInssViewModel : CampoViewModel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public CampoVinculosInssViewModel() : base("Vínculos em ordem de desconto",
        "A ordem define qual vínculo usa cada faixa e a parte disponível do teto. Na mesma fonte pagadora, coloque empregado antes de contribuinte individual. Use apenas vínculos do RGPS da mesma competência.")
    {
        Competencia.Visivel = false;
        Linhas = [new(), new()];
        AdicionarCommand = new RelayCommand(_ => Adicionar(), _ => Linhas.Count < 50);
        RemoverCommand = new RelayCommand(linha => Remover(linha as VinculoInssLinhaViewModel));
        SubirCommand = new RelayCommand(linha => Mover(linha as VinculoInssLinhaViewModel, -1));
        DescerCommand = new RelayCommand(linha => Mover(linha as VinculoInssLinhaViewModel, 1));
        AtualizarPosicoes();
    }

    public ObservableCollection<VinculoInssLinhaViewModel> Linhas { get; }
    public CampoTextoViewModel Competencia { get; } = new("Competência", TipoCampo.Competencia,
        DateTime.Today.ToString("MM/yyyy", Cultura), "Mês comum a todos os vínculos; define as faixas e o teto do INSS.");
    public ICommand AdicionarCommand { get; }
    public ICommand RemoverCommand { get; }
    public ICommand SubirCommand { get; }
    public ICommand DescerCommand { get; }

    public Result<IReadOnlyList<VinculoInss>> Ler()
    {
        if (Linhas.Count > 50)
            return Erro.Validacao("O cálculo aceita no máximo 50 vínculos.");
        var vinculos = new List<VinculoInss>(Linhas.Count);
        foreach (var linha in Linhas)
        {
            if (linha.Categoria?.Valor is not TipoVinculoInss tipo || !Enum.IsDefined(tipo))
                return Erro.Validacao($"No {linha.Numero}º vínculo, selecione uma categoria válida.");
            if (!LeituraNumerica.TentarLer(linha.Remuneracao, out var remuneracao) || remuneracao <= 0m
                || remuneracao > 1_000_000_000m || decimal.Round(remuneracao, 2) != remuneracao)
                return Erro.Validacao($"No {linha.Numero}º vínculo, informe uma remuneração maior que zero em reais e centavos.");
            var identificacao = linha.Identificacao?.Trim() ?? string.Empty;
            if (identificacao.Length > 120)
                return Erro.Validacao($"No {linha.Numero}º vínculo, limite a fonte pagadora a 120 caracteres.");
            vinculos.Add(new VinculoInss(identificacao.Length > 0 ? identificacao : $"Vínculo {linha.Numero}",
                tipo, remuneracao));
        }
        return vinculos;
    }

    public string Exportar() => JsonSerializer.Serialize(Linhas.Select(linha => new LinhaSalva(
        linha.Identificacao, (TipoVinculoInss)linha.Categoria.Valor, linha.Remuneracao)).ToArray());

    public bool Importar(string valor)
    {
        try
        {
            var linhas = JsonSerializer.Deserialize<LinhaSalva[]>(valor);
            if (linhas is null || linhas.Length is < 2 or > 50 || linhas.Any(linha => linha is null || !Enum.IsDefined(linha.Tipo)
                || linha.Identificacao is null || linha.Remuneracao is null))
                return false;
            Linhas.Clear();
            foreach (var linha in linhas)
                Linhas.Add(new VinculoInssLinhaViewModel
                {
                    Categoria = VinculoInssLinhaViewModel.Categorias.Single(opcao => (TipoVinculoInss)opcao.Valor == linha.Tipo),
                    Remuneracao = linha.Remuneracao,
                    Identificacao = linha.Identificacao
                });
            AtualizarPosicoes();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public void Preencher(IReadOnlyList<VinculoInss> vinculos)
    {
        Linhas.Clear();
        foreach (var vinculo in vinculos)
            Linhas.Add(new VinculoInssLinhaViewModel
            {
                Categoria = VinculoInssLinhaViewModel.Categorias.Single(opcao => (TipoVinculoInss)opcao.Valor == vinculo.Tipo),
                Remuneracao = vinculo.Remuneracao.ToString("N2", Cultura),
                Identificacao = vinculo.Identificacao
            });
        AtualizarPosicoes();
    }

    private void Adicionar()
    {
        if (Linhas.Count >= 50)
            return;
        Linhas.Add(new VinculoInssLinhaViewModel());
        AtualizarPosicoes();
    }

    private void Remover(VinculoInssLinhaViewModel? linha)
    {
        if (linha is null || Linhas.Count <= 2)
            return;
        Linhas.Remove(linha);
        AtualizarPosicoes();
    }

    private void Mover(VinculoInssLinhaViewModel? linha, int deslocamento)
    {
        if (linha is null)
            return;
        var origem = Linhas.IndexOf(linha);
        var destino = origem + deslocamento;
        if (origem < 0 || destino < 0 || destino >= Linhas.Count)
            return;
        Linhas.Move(origem, destino);
        AtualizarPosicoes();
    }

    private void AtualizarPosicoes()
    {
        for (var indice = 0; indice < Linhas.Count; indice++)
        {
            var linha = Linhas[indice];
            linha.Numero = indice + 1;
            linha.PodeSubir = indice > 0;
            linha.PodeDescer = indice < Linhas.Count - 1;
            linha.PodeRemover = Linhas.Count > 2;
        }
        ((RelayCommand)AdicionarCommand).RaiseCanExecuteChanged();
    }

    private sealed record LinhaSalva(string Identificacao, TipoVinculoInss Tipo, string Remuneracao);
}
