using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IWindowNavigator
{
    void AbrirTabela(TipoTabelaTributaria tipo);
    void AbrirSimulacaoTributaria();
    void AbrirPensao();
    void AbrirPensaoAtraso();
    void AbrirEstabilidade();
    void AbrirCalculadora(TipoCalculadora tipo);
    void AbrirJornada();
    void AbrirDebitoJudicial();

    /// <summary>Abre a calculadora com os campos informados, pelo rótulo, sobre os do último cálculo.</summary>
    void AbrirCalculadora(TipoCalculadora tipo, IReadOnlyDictionary<string, string> valores);

    /// <summary>Abre a janela do cálculo salvo, preenchida e já calculada; falha se a calculadora não existe nesta versão.</summary>
    Task<Result> AbrirCalculoSalvoAsync(CalculoSalvoDto salvo);
}
