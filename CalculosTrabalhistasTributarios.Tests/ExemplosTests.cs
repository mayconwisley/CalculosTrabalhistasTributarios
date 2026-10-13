using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Os exemplos do botão "Preencher exemplo" são os dados fictícios das imagens do manual: cada um precisa abrir na sua
/// janela, calcular sem erro e, quando a imagem mostra o resultado, chegar aos mesmos valores.
/// </summary>
public class ExemplosTests
{
    /// <summary>Destaques do resultado mostrados na imagem do manual de cada calculadora, pelo rótulo.</summary>
    private static readonly Dictionary<string, (string Rotulo, string Valor)[]> Esperados = new()
    {
        ["Calculadora.CltPj"] = [("CLT: total no ano", "R$ 81.721,32"), ("PJ: total no ano", "R$ 105.504,00"), ("Diferença no ano", "+R$ 23.782,68"), ("PJ equivalente", "R$ 7.820,18")],
        ["Calculadora.SalarioPeloLiquido"] = [("Salário bruto necessário", "R$ 5.947,91"), ("Líquido obtido", "R$ 5.000,00"), ("Descontos", "R$ 947,91"), ("FGTS (8%)", "R$ 475,83")],
        ["Calculadora.HorasExtras"] = [("Horas extras e adicionais", "R$ 487,38"), ("Remuneração do mês", "R$ 3.127,38"), ("Salário líquido", "R$ 2.863,51"), ("Valor da hora", "R$ 12,00")],
        ["Calculadora.DecimoTerceiro"] = [("1ª parcela", "R$ 1.437,50"), ("2ª parcela líquida", "R$ 410,90"), ("13º líquido total", "R$ 1.848,40"), ("FGTS (8%)", "R$ 230,00")],
        ["Calculadora.CustoFuncionario"] = [("Custo mensal", "R$ 5.366,16"), ("Custo anual", "R$ 60.319,92"), ("Acréscimo sobre o salário", "78,87%"), ("Custo por hora", "R$ 24,39")],
        ["Calculadora.ProLaboreAutonomo"] = [("Líquido a receber", "R$ 7.482,81"), ("INSS retido", "R$ 932,31"), ("IRRF retido", "R$ 1.584,88"), ("Custo para a empresa", "R$ 12.000,00")],
        ["Calculadora.Adicionais"] = [("Adicional", "R$ 900,00"), ("Remuneração do mês", "R$ 3.900,00"), ("Salário líquido", "R$ 3.543,42"), ("Base da periculosidade", "R$ 3.000,00")],
        ["Calculadora.SalarioFamilia"] = [("Salário-família", "R$ 135,08"), ("Cota por filho", "R$ 67,54"), ("Limite de remuneração", "R$ 1.980,38"), ("Dias considerados", "30 dias")],
        ["Calculadora.SeguroDesemprego"] = [("Valor da parcela", "R$ 1.621,00"), ("Parcelas", "3"), ("Total do benefício", "R$ 4.863,00")],
        ["Calculadora.IrpfAnual"] = [("Imposto devido", "R$ 17.244,34"), ("Saldo da declaração", "R$ 5.244,34"), ("Tributação mínima", "R$ 102.755,66"), ("Alíquota efetiva", "10,00%")],
        ["Calculadora.Dividendos"] = [("IRRF a reter", "R$ 6.000,00"), ("Dividendos no mês", "R$ 60.000,00"), ("Imposto do mês", "R$ 6.000,00"), ("Limite mensal", "R$ 50.000,00")],
        ["Calculadora.TributoAtraso"] = [("Total a pagar", "R$ 1.232,10"), ("Multa", "R$ 200,00"), ("Juros", "R$ 32,10"), ("Atraso", "85 dias")],
        ["Calculadora.CorrecaoValor"] = [("Valor atualizado", "R$ 1.188,61"), ("Valor corrigido", "R$ 1.042,64"), ("Juros", "R$ 125,12"), ("Multa", "R$ 20,85")],
        ["Calculadora.Domestico"] = [("Líquido do empregado", "R$ 1.724,32"), ("Total do DAE", "R$ 555,68"), ("Encargos do empregador", "R$ 400,00"), ("Custo do empregador", "R$ 2.480,00")],
        ["Calculadora.Estagio"] = [("Líquido do estagiário", "R$ 1.700,00"), ("Recesso a usufruir", "30 dias"), ("Valor do recesso", "R$ 1.500,00"), ("Tempo de estágio", "12 meses")],
        ["Calculadora.Intermitente"] = [("Total do período", "R$ 1.146,67"), ("Remuneração e DSR", "R$ 960,00"), ("Férias + 1/3 e 13º", "R$ 186,67"), ("FGTS", "R$ 83,20")],
        ["Calculadora.CarneLeao"] = [("Imposto a pagar", "R$ 2.361,79"), ("Base de cálculo", "R$ 11.892,80"), ("Rendimentos tributáveis", "R$ 12.500,00"), ("Alíquota efetiva", "18,89%")],
        ["Calculadora.TrabalhoExterior"] = [("Após câmbio e tributos", "R$ 35.873,71"), ("Crédito do banco", "R$ 38.548,00"), ("IR brasileiro", "R$ 2.674,29"), ("Crédito do imposto exterior", "R$ 10.000,00")],
        ["Calculadora.RevisaoPensao"] = [("Pensão atual", "R$ 2.111,99"), ("Pensão proposta", "R$ 1.621,00"), ("Diferença por mês", "-R$ 490,99"), ("Líquido de quem paga", "R$ 5.283,93")],
        ["Calculadora.GanhoCapital"] = [("Imposto a pagar", "R$ 36.492,86"), ("Ganho apurado", "R$ 370.000,00"), ("Ganho tributável", "R$ 243.285,73"), ("Alíquota efetiva", "9,86%")],
        ["Calculadora.Afastamento"] = [("Retorno previsto", "06/11/2026"), ("Pago pela empresa", "R$ 1.500,00"), ("Pago pelo INSS", "R$ 1.365,00"), ("Estabilidade até", "Não há")],
        ["Calculadora.SaqueAniversario"] = [("Saldo fora da garantia", "R$ 8.000,00"), ("Próximo aniversário", "04/2027")],
        ["Calculadora.AbonoSalarial"] = [("Valor do abono", "R$ 1.621,00"), ("Direito", "Sim"), ("Limite de renda", "R$ 2.766,00"), ("Salário mínimo", "R$ 1.621,00")],
        ["Calculadora.Comissoes"] = [("Comissões + DSR", "R$ 3.100,00"), ("DSR das comissões", "R$ 500,00"), ("Impacto líquido", "R$ 2.271,01"), ("Líquido do cenário", "R$ 5.022,43")],
        ["Calculadora.InssMultiplosVinculos"] = [("INSS total do segurado", "R$ 851,50"), ("Base sujeita ao INSS", "R$ 7.500,00"), ("Acima do teto", "R$ 0,00"), ("Após INSS", "R$ 6.648,50")],
        ["Calculadora.ReajusteRetroativo"] = [("Diferença bruta", "R$ 675,00"), ("Salários", "R$ 600,00"), ("13º e férias", "R$ 75,00"), ("FGTS estimado", "R$ 54,00")],
        ["Calculadora.MediaVerbasVariaveis"] = [("Média mensal total", "R$ 1.203,33"), ("Comissões + DSR", "R$ 1.150,00"), ("Horas extras", "R$ 33,33"), ("Adicionais e outras", "R$ 20,00")],
        ["Calculadora.BancoHoras"] = [("Saldo", "1:30"), ("Créditos", "2:00"), ("Compensações", "0:30"), ("Quitação estimada", "R$ 30,68")],
        ["Calculadora.Rra"] = [("IR de anos anteriores", "R$ 0,00"), ("IR da parte de 2026", "R$ 2.403,04"), ("IR estimado do pagamento", "R$ 2.403,04"), ("Total recebido", "R$ 19.000,00")],
        ["Calculadora.SimplesNacional"] = [("DAS do mês", "R$ 3.225,00"), ("Alíquota efetiva", "16,125%"), ("Receita de 12 meses (RBT12)", "R$ 240.000,00"), ("Fator r", "25,00%")],
        ["Calculadora.ConferenciaFgts"] = [("Falta depositar (mensal)", "R$ 40,00"), ("Falta depositar (rescisório)", "R$ 400,00"), ("FGTS devido", "R$ 1.120,00"), ("Reflexo na multa de 40%", "R$ 176,00")],
        ["Calculadora.Transferencia"] = [("Total ao empregado", "R$ 8.069,44"), ("Adicional de transferência", "R$ 5.500,00"), ("Reflexos", "R$ 1.069,44"), ("FGTS", "R$ 525,56")],
        ["Calculadora.Gorjetas"] = [("Total ao empregado", "R$ 11.466,67"), ("Gorjetas repassadas", "R$ 9.600,00"), ("Média mensal", "R$ 1.600,00"), ("FGTS", "R$ 917,33")],
        ["Calculadora.ContribuicaoSegurado"] = [("Valor da GPS", "R$ 178,31"), ("Código da GPS", "1473"), ("Vencimento", "16/11/2026"), ("Complementação até 20%", "R$ 145,89")],
        ["Calculadora.ResgatePrevidencia"] = [("Valor líquido", "R$ 38.200,00"), ("IR retido", "R$ 9.800,00"), ("Base do IR", "R$ 48.000,00"), ("IR no progressivo", "R$ 7.200,00")],
        ["Calculadora.Sobreaviso"] = [("Total bruto ao empregado", "R$ 1.432,75"), ("Sobreaviso e prontidão", "R$ 1.040,00"), ("DSR e reflexos", "R$ 392,75"), ("FGTS", "R$ 114,62")],
        ["Calculadora.Plr"] = [("Líquido da PLR", "R$ 5.560,25"), ("IRRF desta parcela", "R$ 439,75"), ("PLR no ano", "R$ 12.000,00"), ("Isenção anual", "R$ 8.214,40")],
    };

    private static readonly string[] JanelasProprias = ["SimulacaoTributaria", "Pensao", "PensaoAtraso", "DebitoJudicial", "Estabilidade", "Jornada"];

    public static TheoryData<string> Calculadoras() => new(ExemplosDoManual.Chaves.Where(chave => chave.StartsWith("Calculadora.", StringComparison.Ordinal)));

    public static TheoryData<string> Proprias() => new(ExemplosDoManual.Chaves.Where(JanelasProprias.Contains));

    [Fact]
    public void Toda_calculadora_tem_um_exemplo()
    {
        var esperadas = Enum.GetValues<TipoCalculadora>().Select(tipo => $"Calculadora.{tipo}").Concat(JanelasProprias);
        Assert.Empty(esperadas.Except(ExemplosDoManual.Chaves));
    }

    [Theory]
    [MemberData(nameof(Calculadoras))]
    public async Task Exemplo_da_calculadora_calcula_como_no_manual(string chave)
    {
        var servicos = await ServicosAsync();
        var calculadora = servicos.GetRequiredKeyedService<ICalculadora>(Enum.Parse<TipoCalculadora>(chave["Calculadora.".Length..]));
        var exemplo = ExemplosDoManual.Obter(chave)!;
        calculadora.ImportarCampos(exemplo.Campos);

        // Um rótulo que não existe seria ignorado em silêncio ao importar; o exemplo ficaria incompleto.
        Assert.Empty(exemplo.Campos.Keys.Except(calculadora.ExportarCampos().Keys));
        var resultado = await calculadora.CalcularAsync(CancellationToken.None);
        Assert.True(resultado.Sucesso, resultado.Sucesso ? null : resultado.Erro.Mensagem);
        foreach (var (rotulo, valor) in Esperados.GetValueOrDefault(chave, []))
            Assert.Equal(valor, Texto.Normalizar(resultado.Valor.Destaques.Single(destaque => destaque.Rotulo == rotulo).Valor));
    }

    [Theory]
    [MemberData(nameof(Proprias))]
    public async Task Exemplo_da_janela_propria_abre_e_calcula(string chave)
    {
        var servicos = await ServicosAsync();
        ICalculoSalvavel tela = chave switch
        {
            "SimulacaoTributaria" => servicos.GetRequiredService<ISimulacaoTributariaViewModelFactory>().Criar(),
            "Pensao" => servicos.GetRequiredService<IPensaoViewModelFactory>().Criar(),
            "PensaoAtraso" => servicos.GetRequiredService<IPensaoAtrasoViewModelFactory>().Criar(),
            "DebitoJudicial" => servicos.GetRequiredService<IDebitoJudicialViewModelFactory>().Criar(),
            "Estabilidade" => servicos.GetRequiredService<IEstabilidadeViewModelFactory>().Criar(),
            _ => servicos.GetRequiredService<IJornadaViewModelFactory>().Criar()
        };
        var exemplo = ExemplosDoManual.Obter(chave)!;
        tela.ImportarDados(exemplo);
        await tela.RecalcularAsync();

        Assert.Empty(Notificador.Mensagens);
        var exportado = tela.ExportarDados();
        foreach (var (campo, valor) in exemplo.Campos)
            Assert.Equal(valor, exportado.Valor(campo, "(campo inexistente)"));
        Assert.Equal(exemplo.Linhas?.Count ?? 0, exportado.Linhas?.Count ?? 0);

        // Os resultados das imagens 06 (pensão) e 48 (jornada) do manual.
        if (tela is Presentation.ViewModels.PensaoViewModel pensao)
            Assert.Equal("R$ 2.111,99", Texto.Normalizar(pensao.IndicadoresResumo[0].Valor));
        if (tela is Presentation.ViewModels.JornadaViewModel jornada)
            Assert.Equal(("187:00", "3:00"), (jornada.Indicadores[0].Valor, jornada.Indicadores[1].Valor));
    }

    private static readonly NotificadorDeTeste Notificador = new();

    private static readonly Lazy<Task<IServiceProvider>> Servicos = new(async () =>
    {
        // O banco de testes já inicializado; as calculadoras e as janelas próprias vêm da composição do aplicativo.
        await Ambiente.ConsultaAsync();
        var colecao = new ServiceCollection().AddApplication().AddInfrastructure().AddPresentation().AddSingleton<IUserNotifier>(Notificador);
        return colecao.BuildServiceProvider().CreateScope().ServiceProvider;
    });

    private static Task<IServiceProvider> ServicosAsync() => Servicos.Value;

    private sealed class NotificadorDeTeste : IUserNotifier
    {
        public List<string> Mensagens { get; } = [];
        public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") => Mensagens.Add(mensagem);
        public void MostrarErro(string mensagem, Exception exception) => Mensagens.Add($"{mensagem} {exception.Message}");
        public void MostrarFalha(Erro erro, string contexto) => Mensagens.Add($"{contexto} {erro.Mensagem}");
        public bool Confirmar(string mensagem, string titulo) => true;
    }
}
