using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class FiltroHistoricoMobileTests
{
    private static readonly DateTime Hoje = new(2026, 10, 23, 15, 0, 0);
    private static readonly CalculoSalvoDto[] Salvos =
    [
        Salvo(1, "Rescisão", "José da Silva", new(2026, 10, 20)),
        Salvo(2, "Férias", "Ana Souza", new(2026, 3, 1)),
        Salvo(3, "Rescisão", "Processo 123", new(2025, 12, 31)),
        Salvo(4, "13º salário", "ana paula", new(2026, 10, 1))
    ];

    [Fact]
    public void Busca_ignora_acentos_e_maiusculas_no_nome_e_na_calculadora()
    {
        Assert.Equal([1], Ids(FiltroHistoricoMobile.Aplicar(Salvos, "JOSE", null, 0, 0, Hoje)));
        Assert.Equal([1, 3], Ids(FiltroHistoricoMobile.Aplicar(Salvos, " rescisao ", null, 0, 0, Hoje)));
        Assert.Equal([4, 2], Ids(FiltroHistoricoMobile.Aplicar(Salvos, "ana", null, 0, 0, Hoje)));
    }

    [Fact]
    public void Filtra_calculadora_e_periodo_com_limite_inclusivo()
    {
        Assert.Equal([1, 3], Ids(FiltroHistoricoMobile.Aplicar(Salvos, "", "Rescisão", 0, 0, Hoje)));
        // 30 dias antes de 23/10 é 23/09: 01/10 e 20/10 entram, 01/03 não.
        Assert.Equal([1, 4], Ids(FiltroHistoricoMobile.Aplicar(Salvos, "", FiltroHistoricoMobile.TodasAsCalculadoras, 1, 0, Hoje)));
        Assert.Equal([1, 4, 2], Ids(FiltroHistoricoMobile.Aplicar(Salvos, "", null, 2, 0, Hoje)));
        Assert.Single(FiltroHistoricoMobile.Aplicar([Salvo(5, "Férias", "Limite", new(2026, 9, 23))], "", null, 1, 0, Hoje));
    }

    [Fact]
    public void Ordena_por_data_e_por_nome_e_lista_as_calculadoras_salvas()
    {
        Assert.Equal([3, 2, 4, 1], Ids(FiltroHistoricoMobile.Aplicar(Salvos, null, null, 0, 1, Hoje)));
        Assert.Equal([4, 2, 1, 3], Ids(FiltroHistoricoMobile.Aplicar(Salvos, null, null, 0, 2, Hoje)));
        Assert.Equal(["Todas as calculadoras", "13º salário", "Férias", "Rescisão"], FiltroHistoricoMobile.Calculadoras(Salvos));
    }

    private static long[] Ids(IEnumerable<CalculoSalvoDto> salvos) => salvos.Select(salvo => salvo.Id).ToArray();

    private static CalculoSalvoDto Salvo(long id, string calculadora, string nome, DateTime alteradoEm) =>
        new(id, "Calculadora.Teste", calculadora, nome, "{}", alteradoEm, alteradoEm);
}
