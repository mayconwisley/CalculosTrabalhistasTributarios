using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

/// <summary>Uma calculadora da janela padrão: os campos do formulário e o cálculo que produz o demonstrativo.</summary>
public interface ICalculadora
{
    string Titulo { get; }
    string Descricao { get; }
    string InstrucaoInicial { get; }
    IReadOnlyList<CampoViewModel> Campos { get; }

    /// <summary>Nome sugerido para o PDF do último cálculo.</summary>
    string NomeArquivoPdf { get; }

    /// <summary>Competência, salário e dependentes informados no formulário, com nulo nos que a calculadora não usa.</summary>
    ContextoCalculo Contexto { get; }

    void Preencher(ContextoCalculo contexto);

    /// <summary>Valores digitados e opções escolhidas, pelo rótulo do campo, para o histórico.</summary>
    Dictionary<string, string> ExportarCampos();

    /// <summary>Preenche o formulário com os valores salvos; rótulos que não existem mais são ignorados.</summary>
    void ImportarCampos(IReadOnlyDictionary<string, string> valores);

    /// <summary>O demonstrativo, ou o erro do campo a corrigir quando um campo está em formato inválido ou fora das regras.</summary>
    Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken);
}
