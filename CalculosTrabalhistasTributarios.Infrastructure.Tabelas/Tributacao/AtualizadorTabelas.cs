using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Encaminha a atualização de cada tabela ao atualizador da sua fonte.</summary>
public sealed class AtualizadorTabelas(
    IAtualizadorTabelaInss inss,
    IAtualizadorTabelaIrrf irrf,
    AtualizadorTabelaPlr plr,
    AtualizadorSalarioFamilia salarioFamilia,
    AtualizadorSalarioMinimo salarioMinimo,
    AtualizadorSeguroDesemprego seguroDesemprego,
    AtualizadorDescontoMinimo descontoMinimo,
    AtualizadorIndices indices) : IAtualizadorTabelas
{
    public Uri FonteOficial(TipoTabelaTributaria tipo) => tipo switch
    {
        TipoTabelaTributaria.Inss => inss.FonteOficial,
        TipoTabelaTributaria.Plr => plr.FonteOficial,
        TipoTabelaTributaria.SalarioFamilia => salarioFamilia.FonteOficial,
        TipoTabelaTributaria.SalarioMinimo => salarioMinimo.FonteOficial,
        TipoTabelaTributaria.SeguroDesemprego => seguroDesemprego.FonteOficial,
        TipoTabelaTributaria.DescontoMinimo => descontoMinimo.FonteOficial,
        _ when EhIndice(tipo) => indices.FonteOficial(tipo),
        _ => irrf.FonteOficial
    };

    public string NomeFonteOficial(TipoTabelaTributaria tipo) => tipo switch
    {
        TipoTabelaTributaria.Inss or TipoTabelaTributaria.SalarioFamilia or TipoTabelaTributaria.SalarioMinimo => "INSS",
        TipoTabelaTributaria.SeguroDesemprego => "Ministério do Trabalho",
        TipoTabelaTributaria.DescontoMinimo => "Planalto",
        _ when EhIndice(tipo) => indices.NomeFonteOficial(tipo),
        _ => "Receita Federal"
    };

    /// <summary>
    /// Atualiza pela internet. As fontes lançam exceção quando não respondem ou mudam de formato; aqui elas viram um
    /// <see cref="Erro"/> para a tela, porque fonte fora do ar é uma falha esperada.
    /// </summary>
    public async Task<Result<AtualizacaoTabelaResultado>> AtualizarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken)
    {
        try
        {
            return await AtualizarPelaFonteAsync(tipo, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return Erro.Indisponivel(exception.Message);
        }
    }

    // Valor simplificado, dedução por dependente e redução mensal vêm da mesma página da Receita que as faixas do IRRF.
    private Task<AtualizacaoTabelaResultado> AtualizarPelaFonteAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken) => tipo switch
    {
        TipoTabelaTributaria.Inss => inss.AtualizarAsync(cancellationToken),
        TipoTabelaTributaria.Plr => plr.AtualizarAsync(cancellationToken),
        TipoTabelaTributaria.SalarioFamilia => salarioFamilia.AtualizarAsync(cancellationToken),
        TipoTabelaTributaria.SalarioMinimo => salarioMinimo.AtualizarAsync(cancellationToken),
        TipoTabelaTributaria.SeguroDesemprego => seguroDesemprego.AtualizarAsync(cancellationToken),
        TipoTabelaTributaria.DescontoMinimo => descontoMinimo.AtualizarAsync(cancellationToken),
        _ when EhIndice(tipo) => indices.AtualizarAsync(tipo, cancellationToken),
        _ => irrf.AtualizarAsync(cancellationToken)
    };

    private static bool EhIndice(TipoTabelaTributaria tipo) =>
        tipo is TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca or TipoTabelaTributaria.TaxaLegal or TipoTabelaTributaria.Selic or TipoTabelaTributaria.IpcaE or TipoTabelaTributaria.Tr;
}
