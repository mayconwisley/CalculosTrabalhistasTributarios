using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

/// <summary>Serializa preparação, descarte e gravação. A exportação só ocorre após edição e no contexto da UI.</summary>
public sealed class ControleRascunhoFormulario : IDisposable
{
    private readonly IFormularioEditavelMobile _formulario;
    private readonly SessaoRascunhoFormulario _sessao;
    private readonly TimeProvider _relogio;
    private readonly SemaphoreSlim _operacao = new(1, 1);
    private long _edicao;
    private long _persistida;
    private DateTimeOffset _ultimaEdicao;
    public bool Preparada { get; private set; }
    public bool TemEdicoes => Preparada && _edicao != _persistida;

    public ControleRascunhoFormulario(IRascunhosFormularios armazenamento, string chave, IFormularioEditavelMobile formulario, TimeProvider? relogio = null)
    {
        _formulario = formulario;
        _sessao = new(armazenamento, chave, formulario);
        _relogio = relogio ?? TimeProvider.System;
        formulario.FormularioAlterado += AoEditar;
    }
    private void AoEditar(object? sender, EventArgs args)
    {
        if (!Preparada) return;
        _edicao++;
        _ultimaEdicao = _relogio.GetUtcNow();
    }
    public async Task<Result> PrepararAsync(bool recebeuDados, Func<RascunhoFormularioDto, Task<bool>> confirmarRecuperacao, CancellationToken cancellationToken)
    {
        await _operacao.WaitAsync(cancellationToken);
        try
        {
            if (Preparada) return Result.Ok();
            if (!recebeuDados && _formulario.HistoricoId is null && await _sessao.ObterAsync(cancellationToken) is { } rascunho)
            {
                if (await confirmarRecuperacao(rascunho))
                {
                    var resultado = _sessao.Recuperar(rascunho);
                    if (resultado.Falhou) return resultado;
                }
                else await _sessao.DescartarAsync(_sessao.Capturar(), cancellationToken);
            }
            return Result.Ok();
        }
        finally
        {
            // Uma falha de leitura não dispara novos diálogos a cada Appearing.
            Preparada = true;
            _operacao.Release();
        }
    }
    public async Task<bool> PersistirAsync(bool imediato, CancellationToken cancellationToken)
    {
        if (!TemEdicoes || !imediato && _relogio.GetUtcNow() - _ultimaEdicao < TimeSpan.FromMilliseconds(750)) return false;
        await _operacao.WaitAsync(cancellationToken);
        try
        {
            if (!TemEdicoes || !imediato && _relogio.GetUtcNow() - _ultimaEdicao < TimeSpan.FromMilliseconds(750)) return false;
            var edicao = _edicao;
            var dados = _formulario.ExportarDados();
            // Os DTOs exportados são cópias; a serialização não acessa controles nem coleções mutáveis.
            var snapshot = await Task.Run(() => System.Text.Json.JsonSerializer.Serialize(dados), cancellationToken);
            var gravou = await _sessao.SalvarSeAlteradoAsync(snapshot, cancellationToken);
            _persistida = edicao;
            return gravou;
        }
        finally { _operacao.Release(); }
    }
    public async Task<Result> DescartarAsync(DadosFormularioMobile iniciais, CancellationToken cancellationToken)
    {
        await _operacao.WaitAsync(cancellationToken);
        try
        {
            var resultado = _formulario.TentarImportarDados(iniciais);
            if (resultado.Falhou) return resultado;
            _formulario.HistoricoId = null;
            var edicao = _edicao;
            var snapshot = _sessao.Capturar();
            await _sessao.DescartarAsync(snapshot, cancellationToken);
            _persistida = edicao;
            return Result.Ok();
        }
        finally { _operacao.Release(); }
    }
    public void Dispose() => _formulario.FormularioAlterado -= AoEditar;
}
