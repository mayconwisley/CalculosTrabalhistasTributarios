namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Destaques do resultado mostrados na imagem do manual de cada calculadora, pelo rótulo, com a chave do histórico do desktop.
/// Os testes do desktop e do Android conferem o mesmo exemplo contra esta lista, para os dois aplicativos chegarem aos mesmos valores.
/// </summary>
internal static class DestaquesDoManual
{
    public static readonly IReadOnlyDictionary<string, (string Rotulo, string Valor)[]> Esperados = new Dictionary<string, (string Rotulo, string Valor)[]>
    {
        ["Calculadora.CltPj"] = [("CLT: total no ano", "R$ 81.721,32"), ("PJ: total no ano", "R$ 105.504,00"), ("Diferença no ano", "+R$ 23.782,68"), ("PJ equivalente", "R$ 7.820,18")],
        ["Calculadora.SimulacaoTributaria"] = [("Salário líquido", "R$ 6.459,15"), ("INSS", "R$ 988,07"), ("IRRF", "R$ 1.052,78"), ("FGTS (8%)", "R$ 680,00")],
        ["Calculadora.SalarioPeloLiquido"] = [("Salário bruto necessário", "R$ 5.947,91"), ("Líquido obtido", "R$ 5.000,00"), ("Descontos", "R$ 947,91"), ("FGTS (8%)", "R$ 475,83")],
        ["Calculadora.HorasExtras"] = [("Horas extras e adicionais", "R$ 487,38"), ("Remuneração do mês", "R$ 3.127,38"), ("Salário líquido", "R$ 2.863,51"), ("Valor da hora", "R$ 12,00")],
        ["Calculadora.DecimoTerceiro"] = [("1ª parcela", "R$ 1.437,50"), ("2ª parcela líquida", "R$ 410,90"), ("13º líquido total", "R$ 1.848,40"), ("FGTS (8%)", "R$ 230,00")],
        ["Calculadora.CustoFuncionario"] = [("Desembolso recorrente", "R$ 4.574,00"), ("Reserva de provisões", "R$ 792,16"), ("Custo anual", "R$ 60.319,92"), ("Custo médio mensal", "R$ 5.026,66"), ("Acréscimo médio sobre o salário", "67,56%"), ("Custo por hora contratual", "R$ 22,85")],
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
        ["Calculadora.Aposentadoria"] = [("Primeira aposentadoria", "04/2029"), ("Benefício na primeira", "R$ 5.418,40"), ("Maior benefício", "R$ 6.020,44"), ("Tempo de contribuição hoje", "33 anos")],
        ["Calculadora.PensaoMorte"] = [("Pensão mensal", "R$ 4.204,80"), ("Cota familiar", "70%"), ("Por dependente", "R$ 2.102,40"), ("Duração para o cônjuge", "15 anos")],
        ["Calculadora.Sobreaviso"] = [("Total bruto ao empregado", "R$ 1.432,75"), ("Sobreaviso e prontidão", "R$ 1.040,00"), ("DSR e reflexos", "R$ 392,75"), ("FGTS", "R$ 114,62")],
        ["Calculadora.Plr"] = [("Líquido da PLR", "R$ 5.560,25"), ("IRRF desta parcela", "R$ 439,75"), ("PLR no ano", "R$ 12.000,00"), ("Isenção anual", "R$ 8.214,40")],
    };
}
