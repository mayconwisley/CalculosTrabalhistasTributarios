using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

/// <summary>
/// Preenche uma calculadora nova com o formulário salvo e calcula, como ao reabrir o cálculo: com as tabelas atuais da
/// competência informada.
/// </summary>
public sealed class RecalculoHistorico(IServiceProvider servicos) : IRecalculoHistorico
{
    private const string Prefixo = "Calculadora.";

    public bool Suporta(CalculoSalvoDto salvo) => Tipo(salvo) is not null;

    public async Task<Result<DemonstrativoDto>> RecalcularAsync(CalculoSalvoDto salvo, CancellationToken cancellationToken)
    {
        if (Tipo(salvo) is not { } tipo)
            return Erro.NaoEncontrado($"O cálculo “{salvo.Nome}” não é de uma calculadora da janela padrão.");
        var calculadora = servicos.GetRequiredKeyedService<ICalculadora>(tipo);
        calculadora.ImportarCampos(DadosFormulario.DeJson(salvo.Dados).Campos);
        return await calculadora.CalcularAsync(cancellationToken);
    }

    private static TipoCalculadora? Tipo(CalculoSalvoDto salvo) =>
        salvo.Tipo.StartsWith(Prefixo, StringComparison.Ordinal) && Enum.TryParse<TipoCalculadora>(salvo.Tipo[Prefixo.Length..], out var tipo) ? tipo : null;
}
