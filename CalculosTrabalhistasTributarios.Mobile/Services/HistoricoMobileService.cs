using System.Text.Json;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Exemplos;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;
using Microsoft.Extensions.Logging;
using CalculosTrabalhistasTributarios.Mobile.Features.Historico;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

/// <summary>Persiste os formulários no SQLite privado do aparelho e abre novas páginas para cada histórico.</summary>
public sealed class HistoricoMobileService(IHistoricoCalculos historico, Func<TipoCalculadoraMobile, Page> criarPagina,
    NavegadorMenu menu, ILogger<HistoricoMobileService> logger)
{
    private static Page Pagina => Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("Janela do aplicativo indisponível.");

    public Task<IReadOnlyList<CalculoSalvoDto>> ListarAsync() => historico.ListarAsync(CancellationToken.None);

    public async Task SalvarAsync(TipoCalculadoraMobile tipo, IFormularioHistoricoMobile formulario)
    {
        var nomeAtual = formulario.HistoricoId is { } id ? (await historico.ObterAsync(id, CancellationToken.None))?.Nome : null;
        var nome = await Pagina.DisplayPromptAsync("Salvar cálculo", "Dê um nome para encontrar este formulário no histórico.",
            "Salvar", "Cancelar", initialValue: nomeAtual ?? formulario.Titulo, maxLength: 100);
        if (nome is null) return;
        if (string.IsNullOrWhiteSpace(nome))
        {
            await Pagina.DisplayAlertAsync("Nome necessário", "Informe um nome para salvar o cálculo.", "Entendi");
            return;
        }
        try
        {
            var dados = JsonSerializer.Serialize(formulario.ExportarDados());
            formulario.HistoricoId = await historico.SalvarAsync(formulario.HistoricoId, $"Calculadora.{tipo}",
                formulario.Titulo, nome.Trim(), dados, CancellationToken.None);
            await Pagina.DisplayAlertAsync("Histórico", "Formulário salvo no aparelho.", "Entendi");
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao salvar formulário móvel {Tipo}.", tipo);
            await Pagina.DisplayAlertAsync("Histórico", "Não foi possível salvar o formulário. Tente novamente.", "Entendi");
        }
    }

    public async Task AbrirAsync(CalculoSalvoDto salvo)
    {
        if (!salvo.Tipo.StartsWith("Calculadora.", StringComparison.Ordinal)
            || !Enum.TryParse<TipoCalculadoraMobile>(salvo.Tipo["Calculadora.".Length..], out var tipo)
            || !Enum.IsDefined(tipo))
        {
            await Pagina.DisplayAlertAsync("Histórico", "Esta calculadora não está disponível nesta versão do mobile.", "Entendi");
            return;
        }
        if (salvo.Dados.Length > 2_000_000)
        {
            await Pagina.DisplayAlertAsync("Histórico", "O formulário salvo excede o tamanho permitido.", "Entendi");
            return;
        }
        try
        {
            var dados = JsonSerializer.Deserialize<DadosFormularioMobile>(salvo.Dados);
            if (dados is null) throw new JsonException("Dados ausentes.");
            var pagina = criarPagina(tipo);
            if (pagina.BindingContext is not IFormularioHistoricoMobile formulario)
                throw new InvalidOperationException("A calculadora não implementa histórico.");
            var importacao = formulario.TentarImportarDados(dados);
            if (importacao.Falhou)
            {
                await Pagina.DisplayAlertAsync("Histórico", importacao.Erro.Mensagem, "Entendi");
                return;
            }
            formulario.HistoricoId = salvo.Id;
            await menu.AbrirPaginaAsync(pagina);
            await CalcularAsync(formulario);
        }
        catch (JsonException)
        {
            await Pagina.DisplayAlertAsync("Histórico", "O formulário salvo está corrompido e não pôde ser aberto.", "Entendi");
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao abrir histórico móvel {Id}.", salvo.Id);
            await Pagina.DisplayAlertAsync("Histórico", "Não foi possível abrir o formulário salvo.", "Entendi");
        }
    }

    /// <summary>Substitui o formulário pelo exemplo do manual, como no desktop: confirma, volta os campos ao valor inicial e calcula.</summary>
    public async Task PreencherExemploAsync(TipoCalculadoraMobile tipo, IFormularioHistoricoMobile formulario)
    {
        if (!await Pagina.DisplayAlertAsync("Preencher exemplo",
            "Preencher o formulário com o exemplo do manual?\n\nOs dados do formulário serão substituídos por valores fictícios.",
            "Preencher", "Cancelar"))
            return;
        try
        {
            if (criarPagina(tipo).BindingContext is not IFormularioHistoricoMobile vazio)
                throw new InvalidOperationException("A calculadora não implementa histórico.");
            var exemplo = ExemplosMobile.Montar(tipo, vazio.ExportarDados());
            var importacao = exemplo.Falhou ? Result.Falha(exemplo.Erro) : formulario.TentarImportarDados(exemplo.Valor);
            if (importacao.Falhou)
            {
                await Pagina.DisplayAlertAsync("Exemplo", importacao.Erro.Mensagem, "Entendi");
                return;
            }
            // O exemplo não é o cálculo salvo que estava aberto: salvar depois cria um novo registro.
            formulario.HistoricoId = null;
            await CalcularAsync(formulario);
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao preencher o exemplo da calculadora móvel {Tipo}.", tipo);
            await Pagina.DisplayAlertAsync("Exemplo", "Não foi possível preencher o exemplo.", "Entendi");
        }
    }

    public async Task CompararAsync(CalculoSalvoDto primeiro, CalculoSalvoDto segundo)
    {
        if (primeiro.Tipo != segundo.Tipo)
        {
            await Pagina.DisplayAlertAsync("Comparação", "Selecione dois cálculos da mesma calculadora.", "Entendi");
            return;
        }
        try
        {
            var a = await RecalcularAsync(primeiro);
            var b = await RecalcularAsync(segundo);
            var linhas = a is not null && b is not null ? ComparacaoResultados.Linhas(a, b) : [];
            await menu.AbrirPaginaAsync(new ComparacaoPage(primeiro.Nome, segundo.Nome, linhas,
                a is null || b is null ? "Um dos formulários não pôde ser recalculado. Abra cada cálculo para conferir os dados." : null));
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao comparar históricos móveis {Primeiro} e {Segundo}.", primeiro.Id, segundo.Id);
            await Pagina.DisplayAlertAsync("Comparação", "Não foi possível comparar os cálculos salvos.", "Entendi");
        }
    }

    private async Task<DemonstrativoDto?> RecalcularAsync(CalculoSalvoDto salvo)
    {
        if (salvo.Dados.Length > 2_000_000 || !salvo.Tipo.StartsWith("Calculadora.", StringComparison.Ordinal)
            || !Enum.TryParse<TipoCalculadoraMobile>(salvo.Tipo["Calculadora.".Length..], out var tipo) || !Enum.IsDefined(tipo))
            return null;
        var dados = JsonSerializer.Deserialize<DadosFormularioMobile>(salvo.Dados);
        if (dados is null) return null;
        var formulario = criarPagina(tipo).BindingContext as IFormularioHistoricoMobile;
        if (formulario is null || formulario.TentarImportarDados(dados).Falhou) return null;
        await CalcularAsync(formulario);
        return formulario switch
        {
            CalculadoraTrabalhistaViewModel trabalhista => trabalhista.Demonstrativo,
            SaqueAniversarioViewModel saque => saque.Demonstrativo,
            SimulacaoTributariaViewModel tributaria => tributaria.Demonstrativo,
            _ => null
        };
    }

    private static async Task CalcularAsync(IFormularioHistoricoMobile formulario)
    {
        switch (formulario)
        {
            case CalculadoraTrabalhistaViewModel trabalhista: await trabalhista.CalcularCommand.ExecuteAsync(null); break;
            case SaqueAniversarioViewModel saque: await saque.CalcularCommand.ExecuteAsync(null); break;
            case SimulacaoTributariaViewModel tributaria: await tributaria.CalcularCommand.ExecuteAsync(null); break;
        }
    }

    public async Task DuplicarAsync(CalculoSalvoDto salvo)
    {
        var resultado = await historico.DuplicarAsync(salvo.Id, CancellationToken.None);
        if (resultado.Falhou) await Pagina.DisplayAlertAsync("Histórico", resultado.Erro.Mensagem, "Entendi");
    }

    public async Task RenomearAsync(CalculoSalvoDto salvo)
    {
        var nome = await Pagina.DisplayPromptAsync("Renomear cálculo", "Nome do formulário salvo", "Salvar", "Cancelar",
            initialValue: salvo.Nome, maxLength: 100);
        if (nome is null) return;
        if (string.IsNullOrWhiteSpace(nome))
        {
            await Pagina.DisplayAlertAsync("Nome necessário", "Informe um nome para o cálculo.", "Entendi");
            return;
        }
        await historico.RenomearAsync(salvo.Id, nome.Trim(), CancellationToken.None);
    }

    public async Task ExcluirAsync(CalculoSalvoDto salvo)
    {
        if (await Pagina.DisplayAlertAsync("Excluir cálculo", $"Excluir “{salvo.Nome}” do histórico?", "Excluir", "Cancelar"))
            await historico.ExcluirAsync(salvo.Id, CancellationToken.None);
    }
}
