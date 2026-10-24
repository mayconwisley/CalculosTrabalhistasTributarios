using System.Text.Json;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

/// <summary>Uma sessão por formulário; snapshots imutáveis e escrita serial para não persistir versões fora de ordem.</summary>
public sealed class SessaoRascunhoFormulario(IRascunhosFormularios armazenamento, string chave, IFormularioHistoricoMobile formulario)
{
    private readonly SemaphoreSlim _acesso = new(1, 1);
    private string _ultimo = JsonSerializer.Serialize(formulario.ExportarDados());

    public Task<RascunhoFormularioDto?> ObterAsync(CancellationToken cancellationToken) => armazenamento.ObterAsync(chave, cancellationToken);

    public Result Recuperar(RascunhoFormularioDto rascunho)
    {
        try
        {
            if (rascunho.Versao != 1 || rascunho.Dados is null || rascunho.Dados.Length > 2_000_000)
                return Erro.Validacao("O rascunho tem formato incompatível ou excede o tamanho permitido.");
            var dados = JsonSerializer.Deserialize<DadosFormularioMobile>(rascunho.Dados);
            if (dados is null) return Erro.Validacao("O rascunho não contém um formulário.");
            var resultado = formulario.TentarImportarDados(dados);
            if (resultado.Falhou) return resultado;
            // Recuperação nunca associa um registro salvo: Salvar cria um novo histórico.
            formulario.HistoricoId = null;
            _ultimo = JsonSerializer.Serialize(formulario.ExportarDados());
            return Result.Ok();
        }
        catch (JsonException) { return Erro.Validacao("Não foi possível ler o rascunho. Descarte-o para iniciar outro formulário."); }
    }

    /// <summary>Capture no contexto da interface; a persistência não acessa objetos mutáveis do formulário.</summary>
    public string Capturar() => JsonSerializer.Serialize(formulario.ExportarDados());

    public async Task<bool> SalvarSeAlteradoAsync(string snapshot, CancellationToken cancellationToken)
    {
        await _acesso.WaitAsync(cancellationToken);
        try
        {
            if (snapshot == _ultimo) return false;
            await armazenamento.SalvarAsync(chave, new(1, snapshot, DateTimeOffset.UtcNow), cancellationToken);
            _ultimo = snapshot;
            return true;
        }
        finally { _acesso.Release(); }
    }

    public async Task DescartarAsync(string snapshotAtual, CancellationToken cancellationToken)
    {
        await _acesso.WaitAsync(cancellationToken);
        try
        {
            await armazenamento.RemoverAsync(chave, cancellationToken);
            _ultimo = snapshotAtual;
        }
        finally { _acesso.Release(); }
    }
}
