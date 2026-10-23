using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tabelas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class TabelaMobileTests
{
    [Fact]
    public async Task Seleciona_edita_valida_e_atualiza_tabela_sem_perder_dados_locais()
    {
        var service = new TabelaDeTeste();
        var atualizador = new AtualizadorDeTeste();
        var vm = new TabelaMobileViewModel(TipoTabelaTributaria.Inss, service, atualizador, new PlataformaDeTeste(),
            NullLogger<TabelaMobileViewModel>.Instance);
        await vm.CarregarAsync();
        var linha = Assert.Single(vm.Registros);
        Assert.Equal("1.621,00", linha.Valor);
        Assert.False(linha.TemDeducao);
        vm.SelecionarCommand.Execute(linha);
        Assert.Equal("01/2026", vm.Competencia);
        Assert.Equal("Salvar alteração", vm.TextoSalvar);
        vm.Valor = "inválido";
        await vm.SalvarCommand.ExecuteAsync(null);
        Assert.Null(service.Salvo);
        Assert.Contains("valores numéricos", vm.Mensagem);
        vm.Valor = "1.700,00";
        await vm.SalvarCommand.ExecuteAsync(null);
        Assert.Equal(7, service.Salvo?.Id);
        Assert.Equal(new DateOnly(2026, 1, 1), service.Salvo?.Competencia);
        Assert.Equal(1700m, service.Salvo?.Valor);
        Assert.Equal(7.5m, service.Salvo?.Aliquota);
        await vm.AtualizarPelaInternetCommand.ExecuteAsync(null);
        Assert.Equal(TipoTabelaTributaria.Inss, atualizador.TipoConsultado);
        Assert.Contains("atualiz", vm.StatusAtualizacao, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TabelaDeTeste : ITabelaTributariaService
    {
        public SalvarRegistroTabelaRequest? Salvo { get; private set; }
        public Task<IReadOnlyList<RegistroTabelaDto>> ListarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RegistroTabelaDto>>([new(7, new DateOnly(2026, 1, 1), 1, 1621m, 7.5m, null)]);
        public Task<Result> SalvarAsync(TipoTabelaTributaria tipo, SalvarRegistroTabelaRequest request, CancellationToken cancellationToken)
        {
            Salvo = request;
            return Task.FromResult(Result.Ok());
        }
        public Task<Result> ExcluirAsync(TipoTabelaTributaria tipo, int id, CancellationToken cancellationToken) => Task.FromResult(Result.Ok());
    }

    private sealed class AtualizadorDeTeste : IAtualizadorTabelas
    {
        public TipoTabelaTributaria? TipoConsultado { get; private set; }
        public Uri FonteOficial(TipoTabelaTributaria tipo) => new("https://www.gov.br/inss/");
        public string NomeFonteOficial(TipoTabelaTributaria tipo) => "INSS";
        public Task<Result<AtualizacaoTabelaResultado>> AtualizarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken)
        {
            TipoConsultado = tipo;
            return Task.FromResult<Result<AtualizacaoTabelaResultado>>(new AtualizacaoTabelaResultado(new DateOnly(2026, 1, 1), 4, ["INSS"], true, []));
        }
    }

    private sealed class PlataformaDeTeste : IPlataformaMobile
    {
        public string Versao => "1.0";
        public Task MostrarAsync(string titulo, string mensagem) => Task.CompletedTask;
        public Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar) => Task.FromResult(true);
        public Task AbrirEnderecoAsync(Uri endereco) => Task.CompletedTask;
        public void AplicarTema(TemaMobile tema) { }
    }
}
