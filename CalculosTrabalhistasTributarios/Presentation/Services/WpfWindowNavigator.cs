using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Views;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>Detalhe de navegação WPF, isolado dos ViewModels.</summary>
public sealed class WpfWindowNavigator(
    ITabelaManutencaoViewModelFactory tabelaViewModelFactory,
    IPensaoViewModelFactory pensaoViewModelFactory,
    IPensaoAtrasoViewModelFactory pensaoAtrasoViewModelFactory,
    IEstabilidadeViewModelFactory estabilidadeViewModelFactory,
    ICalculadoraViewModelFactory calculadoraViewModelFactory,
    IJornadaViewModelFactory jornadaViewModelFactory,
    IDebitoJudicialViewModelFactory debitoJudicialViewModelFactory) : IWindowNavigator
{
    public void AbrirTabela(TipoTabelaTributaria tipo) => Abrir(new TabelaManutencaoWindow(tabelaViewModelFactory.Criar(tipo)), $"Tabela.{tipo}");
    public void AbrirPensao() => Abrir(new PensaoWindow(pensaoViewModelFactory.Criar()), "Pensao");
    public void AbrirPensaoAtraso() => Abrir(new PensaoAtrasoWindow(pensaoAtrasoViewModelFactory.Criar()), "PensaoAtraso");
    public void AbrirEstabilidade() => Abrir(new EstabilidadeWindow(estabilidadeViewModelFactory.Criar()), "Estabilidade");
    public void AbrirCalculadora(TipoCalculadora tipo) => Abrir(new CalculadoraWindow(CriarCalculadora(tipo)), $"Calculadora.{tipo}");
    public void AbrirJornada() => Abrir(new JornadaWindow(jornadaViewModelFactory.Criar()), "Jornada");
    public void AbrirDebitoJudicial() => Abrir(new DebitoJudicialWindow(debitoJudicialViewModelFactory.Criar()), "DebitoJudicial");

    public void AbrirCalculadora(TipoCalculadora tipo, IReadOnlyDictionary<string, string> valores)
    {
        var calculadora = CriarCalculadora(tipo);
        calculadora.ImportarCampos(valores);
        Abrir(new CalculadoraWindow(calculadora), $"Calculadora.{tipo}");
    }

    private const string PrefixoCalculadora = "Calculadora.";

    public async Task<Result> AbrirCalculoSalvoAsync(CalculoSalvoDto salvo)
    {
        switch (salvo.Tipo)
        {
            case "Pensao":
                var pensao = pensaoViewModelFactory.Criar();
                await pensao.Historico.CarregarAsync(salvo);
                Abrir(new PensaoWindow(pensao), "Pensao");
                break;
            case "PensaoAtraso":
                var atraso = pensaoAtrasoViewModelFactory.Criar();
                await atraso.Historico.CarregarAsync(salvo);
                Abrir(new PensaoAtrasoWindow(atraso), "PensaoAtraso");
                break;
            case "Jornada":
                var jornada = jornadaViewModelFactory.Criar();
                await jornada.Historico.CarregarAsync(salvo);
                Abrir(new JornadaWindow(jornada), "Jornada");
                break;
            case "DebitoJudicial":
                var debito = debitoJudicialViewModelFactory.Criar();
                await debito.Historico.CarregarAsync(salvo);
                Abrir(new DebitoJudicialWindow(debito), "DebitoJudicial");
                break;
            case "Estabilidade":
                var estabilidade = estabilidadeViewModelFactory.Criar();
                await estabilidade.Historico.CarregarAsync(salvo);
                Abrir(new EstabilidadeWindow(estabilidade), "Estabilidade");
                break;
            default:
                if (!salvo.Tipo.StartsWith(PrefixoCalculadora, StringComparison.Ordinal) || !Enum.TryParse<TipoCalculadora>(salvo.Tipo[PrefixoCalculadora.Length..], out var tipo))
                    return Erro.NaoEncontrado($"O cálculo “{salvo.Nome}” é de uma calculadora que não existe nesta versão do aplicativo.");
                var calculadora = CriarCalculadora(tipo);
                await calculadora.Historico.CarregarAsync(salvo);
                Abrir(new CalculadoraWindow(calculadora), $"Calculadora.{tipo}");
                break;
        }
        return Result.Ok();
    }

    private CalculadoraViewModel CriarCalculadora(TipoCalculadora tipo)
    {
        var calculadora = calculadoraViewModelFactory.Criar(tipo);
        calculadora.ConfigurarAberturaCalculadora(AbrirCalculadora);
        return calculadora;
    }

    private static void Abrir(Window janela, string chave)
    {
        var aplicativo = System.Windows.Application.Current;
        if (aplicativo.MainWindow is MainWindow principal && principal.Exibir(janela, chave))
            return;
        if (aplicativo.MainWindow is MainWindow)
            return;
        janela.Owner = aplicativo.Windows.OfType<Window>().FirstOrDefault(aberta => aberta.IsActive) ?? aplicativo.MainWindow;
        janela.ShowDialog();
    }
}
