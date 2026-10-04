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
    ISimulacaoTributariaViewModelFactory simulacaoViewModelFactory,
    IPensaoViewModelFactory pensaoViewModelFactory,
    IPensaoAtrasoViewModelFactory pensaoAtrasoViewModelFactory,
    IEstabilidadeViewModelFactory estabilidadeViewModelFactory,
    ICalculadoraViewModelFactory calculadoraViewModelFactory,
    IJornadaViewModelFactory jornadaViewModelFactory,
    IDebitoJudicialViewModelFactory debitoJudicialViewModelFactory) : IWindowNavigator
{
    public void AbrirTabela(TipoTabelaTributaria tipo) => Abrir(new TabelaManutencaoWindow(tabelaViewModelFactory.Criar(tipo)));
    public void AbrirSimulacaoTributaria() => Abrir(new SimulacaoTributariaWindow(simulacaoViewModelFactory.Criar()));
    public void AbrirPensao() => Abrir(new PensaoWindow(pensaoViewModelFactory.Criar()));
    public void AbrirPensaoAtraso() => Abrir(new PensaoAtrasoWindow(pensaoAtrasoViewModelFactory.Criar()));
    public void AbrirEstabilidade() => Abrir(new EstabilidadeWindow(estabilidadeViewModelFactory.Criar()));
    public void AbrirCalculadora(TipoCalculadora tipo) => Abrir(new CalculadoraWindow(calculadoraViewModelFactory.Criar(tipo)));
    public void AbrirJornada() => Abrir(new JornadaWindow(jornadaViewModelFactory.Criar()));
    public void AbrirDebitoJudicial() => Abrir(new DebitoJudicialWindow(debitoJudicialViewModelFactory.Criar()));

    public void AbrirCalculadora(TipoCalculadora tipo, IReadOnlyDictionary<string, string> valores)
    {
        var calculadora = calculadoraViewModelFactory.Criar(tipo);
        calculadora.ImportarCampos(valores);
        Abrir(new CalculadoraWindow(calculadora));
    }

    private const string PrefixoCalculadora = "Calculadora.";

    public async Task<Result> AbrirCalculoSalvoAsync(CalculoSalvoDto salvo)
    {
        switch (salvo.Tipo)
        {
            case "SimulacaoTributaria":
                var simulacao = simulacaoViewModelFactory.Criar();
                await simulacao.Historico.CarregarAsync(salvo);
                Abrir(new SimulacaoTributariaWindow(simulacao));
                break;
            case "Pensao":
                var pensao = pensaoViewModelFactory.Criar();
                await pensao.Historico.CarregarAsync(salvo);
                Abrir(new PensaoWindow(pensao));
                break;
            case "PensaoAtraso":
                var atraso = pensaoAtrasoViewModelFactory.Criar();
                await atraso.Historico.CarregarAsync(salvo);
                Abrir(new PensaoAtrasoWindow(atraso));
                break;
            case "Jornada":
                var jornada = jornadaViewModelFactory.Criar();
                await jornada.Historico.CarregarAsync(salvo);
                Abrir(new JornadaWindow(jornada));
                break;
            case "DebitoJudicial":
                var debito = debitoJudicialViewModelFactory.Criar();
                await debito.Historico.CarregarAsync(salvo);
                Abrir(new DebitoJudicialWindow(debito));
                break;
            case "Estabilidade":
                var estabilidade = estabilidadeViewModelFactory.Criar();
                await estabilidade.Historico.CarregarAsync(salvo);
                Abrir(new EstabilidadeWindow(estabilidade));
                break;
            default:
                if (!salvo.Tipo.StartsWith(PrefixoCalculadora, StringComparison.Ordinal) || !Enum.TryParse<TipoCalculadora>(salvo.Tipo[PrefixoCalculadora.Length..], out var tipo))
                    return Erro.NaoEncontrado($"O cálculo “{salvo.Nome}” é de uma calculadora que não existe nesta versão do aplicativo.");
                var calculadora = calculadoraViewModelFactory.Criar(tipo);
                await calculadora.Historico.CarregarAsync(salvo);
                Abrir(new CalculadoraWindow(calculadora));
                break;
        }
        return Result.Ok();
    }

    private static void Abrir(Window janela)
    {
        // A janela aberta a partir de outra, como a calculadora aberta pela jornada, fica sobre ela.
        var aplicativo = System.Windows.Application.Current;
        janela.Owner = aplicativo.Windows.OfType<Window>().FirstOrDefault(aberta => aberta.IsActive) ?? aplicativo.MainWindow;
        janela.ShowDialog();
    }
}
