using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>Calculadoras da tela inicial, nos grupos e na ordem do desktop. O registro dos formulários e a navegação partem daqui.</summary>
public static class CatalogoCalculadorasMobile
{
    public static IReadOnlyList<GrupoCalculadorasMobile> Grupos { get; } =
    [
        new("Impostos e salário",
        [
            new(TipoCalculadoraMobile.SimulacaoTributaria, "Simulação tributária", "INSS por faixas, IRRF nas duas modalidades, salário líquido e FGTS por competência.", "Simular impostos e salário", null),
            new(TipoCalculadoraMobile.SalarioPeloLiquido, "Salário bruto pelo líquido", "O salário bruto necessário para chegar a um líquido desejado.", "Calcular bruto", typeof(SalarioPeloLiquidoMobileViewModel)),
            new(TipoCalculadoraMobile.InssMultiplosVinculos, "INSS em múltiplos vínculos", "Desconto por vínculo com teto compartilhado e faixas progressivas.", "Calcular INSS dos vínculos", typeof(InssMultiplosVinculosMobileViewModel)),
            new(TipoCalculadoraMobile.Plr, "PLR (participação nos lucros)", "IRRF pela tabela anual exclusiva, sem INSS ou FGTS.", "Calcular PLR", typeof(PlrMobileViewModel)),
            new(TipoCalculadoraMobile.ProLabore, "Pró-labore e autônomo", "Líquido do sócio ou RPA e custo da empresa.", "Calcular pró-labore ou RPA", typeof(ProLaboreMobileViewModel)),
            new(TipoCalculadoraMobile.ContribuicaoSegurado, "Contribuição do autônomo e do facultativo", "GPS de 20%, 11% ou 5%: valor, código, vencimento e complementação.", "Calcular GPS", typeof(ContribuicaoSeguradoMobileViewModel)),
            new(TipoCalculadoraMobile.CltPj, "CLT x PJ", "Compare o valor do profissional e o custo da empresa nos dois regimes.", "Comparar CLT x PJ", typeof(CltPjMobileViewModel)),
            new(TipoCalculadoraMobile.IrpfAnual, "IRPF anual", "Declaração completa ou simplificada, redução anual e tributação mínima.", "Calcular IRPF", typeof(IrpfAnualMobileViewModel)),
            new(TipoCalculadoraMobile.CarneLeao, "Carnê-leão", "IR mensal de honorários e aluguéis recebidos de pessoas físicas.", "Calcular carnê-leão", typeof(CarneLeaoMobileViewModel)),
            new(TipoCalculadoraMobile.TrabalhoExterior, "Trabalho no exterior", "Carnê-leão de salário ou serviço pago por fonte estrangeira.", "Calcular carnê-leão", typeof(TrabalhoExteriorMobileViewModel)),
            new(TipoCalculadoraMobile.CreditoTrabalhador, "Crédito do Trabalhador", "Consignado: margem, parcelas, juros, custos e crédito líquido.", "Simular crédito", typeof(CreditoTrabalhadorMobileViewModel)),
            new(TipoCalculadoraMobile.EmprestimoPessoal, "Empréstimo pessoal", "Parcelas, IOF, seguro, custos e valor líquido recebido.", "Simular empréstimo", typeof(EmprestimoPessoalMobileViewModel)),
            new(TipoCalculadoraMobile.GanhoCapital, "Ganho de capital", "IR na venda de imóvel ou outro bem, com isenções e fatores de redução.", "Calcular ganho", typeof(GanhoCapitalMobileViewModel)),
            new(TipoCalculadoraMobile.Rra, "RRA: rendimentos acumulados", "IRRF de pagamentos atrasados, separando anos anteriores e o ano do pagamento.", "Calcular RRA", typeof(RraMobileViewModel)),
            new(TipoCalculadoraMobile.ResgatePrevidencia, "Resgate de previdência privada", "IR do PGBL ou VGBL por aporte, nos regimes regressivo e progressivo.", "Calcular resgate", typeof(ResgatePrevidenciaMobileViewModel)),
            new(TipoCalculadoraMobile.SimplesNacional, "Simples Nacional e fator r", "DAS pela receita e folha de 12 meses, anexo e efeito do pró-labore.", "Calcular DAS", typeof(SimplesNacionalMobileViewModel)),
            new(TipoCalculadoraMobile.Dividendos, "Dividendos", "Retenção de 10% acima de R$ 50 mil no mês, desde 2026.", "Calcular retenção", typeof(DividendosMobileViewModel)),
            new(TipoCalculadoraMobile.TributoAtraso, "Tributo em atraso", "Multa e juros pela Selic de DARF, DAS, DAE ou GPS pago depois do vencimento.", "Calcular acréscimos", typeof(TributoAtrasoMobileViewModel))
        ]),
        new("Pensão e débitos judiciais",
        [
            new(TipoCalculadoraMobile.PensaoAlimenticia, "Pensão alimentícia", "Um ou mais beneficiários e o efeito no IRRF de quem paga.", "Calcular pensão", typeof(PensaoMobileViewModel)),
            new(TipoCalculadoraMobile.PensaoAtraso, "Pensão em atraso", "Parcelas corrigidas, juros e ritos da prisão e da penhora.", "Calcular atraso", typeof(PensaoAtrasoMobileViewModel)),
            new(TipoCalculadoraMobile.RevisaoPensao, "Revisão de pensão", "A pensão atual e a proposta lado a lado, com o efeito em quem paga.", "Comparar pensões", typeof(RevisaoPensaoMobileViewModel)),
            new(TipoCalculadoraMobile.DebitoJudicial, "Débitos judiciais", "Correção e juros de débitos trabalhistas e cíveis pelas fases da ADC 58, do TST, do STJ e da Lei 14.905/2024.", "Atualizar débito", typeof(DebitoJudicialMobileViewModel)),
            new(TipoCalculadoraMobile.CorrecaoValor, "Correção de valores", "Valor atualizado pelo IPCA, INPC, Selic ou outro índice, com juros e multa.", "Corrigir valor", typeof(CorrecaoValorMobileViewModel))
        ]),
        new("Remuneração e custos",
        [
            new(TipoCalculadoraMobile.Holerite, "Holerite do mês", "Salário, horas, comissões, incidências, descontos e benefícios em um demonstrativo.", "Montar holerite", typeof(HoleriteMobileViewModel)),
            new(TipoCalculadoraMobile.Comissoes, "Comissões e DSR", "Repouso remunerado das comissões e seus efeitos na folha.", "Calcular comissões", typeof(ComissoesMobileViewModel)),
            new(TipoCalculadoraMobile.BancoHoras, "Banco de horas", "Créditos, folgas, saldo e quitação no fechamento ou na rescisão.", "Conferir banco de horas", typeof(BancoHorasMobileViewModel)),
            new(TipoCalculadoraMobile.Jornada, "Jornada pelo ponto", "Horas extras, noturnas, faltas, atrasos e intervalos pelas marcações do ponto.", "Apurar jornada", typeof(JornadaMobileViewModel)),
            new(TipoCalculadoraMobile.HorasExtras, "Horas extras e adicionais", "Horas extras, adicional noturno e reflexo no DSR.", "Calcular horas extras", typeof(HorasExtrasMobileViewModel)),
            new(TipoCalculadoraMobile.Adicionais, "Insalubridade e periculosidade", "Adicionais do mês, INSS, IRRF e salário líquido.", "Calcular adicionais", typeof(AdicionaisMobileViewModel)),
            new(TipoCalculadoraMobile.Sobreaviso, "Sobreaviso e prontidão", "Horas a 1/3 e a 2/3 do salário-hora por mês, com DSR, 13º, férias e FGTS.", "Calcular sobreaviso", typeof(SobreavisoMobileViewModel)),
            new(TipoCalculadoraMobile.Transferencia, "Adicional de transferência", "Mínimo de 25% na transferência provisória, com 13º, férias, aviso e FGTS.", "Calcular adicional", typeof(TransferenciaMobileViewModel)),
            new(TipoCalculadoraMobile.Gorjetas, "Gorjetas", "Rateio, retenção da empresa, média de 12 meses e reflexos no 13º, férias e FGTS.", "Calcular gorjetas", typeof(GorjetasMobileViewModel)),
            new(TipoCalculadoraMobile.ReajusteRetroativo, "Diferenças de reajuste retroativo", "Salários pagos e devidos por mês, com 13º, férias e FGTS.", "Calcular diferenças", typeof(ReajusteRetroativoMobileViewModel)),
            new(TipoCalculadoraMobile.SalarioFamilia, "Salário-família", "Direito e cota por filho, com dias proporcionais.", "Calcular salário-família", typeof(SalarioFamiliaMobileViewModel)),
            new(TipoCalculadoraMobile.CustoFuncionario, "Custo do funcionário", "Encargos, provisões e benefícios, com custo médio anualizado.", "Calcular custo do funcionário", typeof(CustoFuncionarioMobileViewModel)),
            new(TipoCalculadoraMobile.Domestico, "Empregado doméstico (DAE)", "Salário líquido do doméstico e o DAE do mês, com os encargos do empregador.", "Calcular DAE", typeof(DomesticoMobileViewModel)),
            new(TipoCalculadoraMobile.Estagio, "Estágio", "Bolsa líquida do estagiário, com o IRRF, e o recesso proporcional.", "Calcular estágio", typeof(EstagioMobileViewModel)),
            new(TipoCalculadoraMobile.Intermitente, "Trabalho intermitente", "Pagamento de cada convocação: horas, DSR, férias, 13º e FGTS.", "Calcular convocação", typeof(IntermitenteMobileViewModel))
        ]),
        new("Férias, 13º e desligamento",
        [
            new(TipoCalculadoraMobile.MediaVerbasVariaveis, "Média de verbas variáveis", "Comissões, DSR, horas extras e adicionais por mês para férias, 13º e rescisão.", "Calcular média", typeof(MediaVerbasVariaveisMobileViewModel)),
            new(TipoCalculadoraMobile.DecimoTerceiro, "13º salário", "Parcelas e complemento das médias, com INSS e IRRF.", "Calcular 13º salário", typeof(DecimoTerceiroMobileViewModel)),
            new(TipoCalculadoraMobile.Ferias, "Férias", "Recibo, abono, fracionamento e bases por mês de gozo.", "Calcular férias", typeof(FeriasMobileViewModel)),
            new(TipoCalculadoraMobile.Rescisao, "Rescisão", "Verbas do desligamento, aviso, férias, 13º e FGTS.", "Calcular rescisão", typeof(RescisaoMobileViewModel)),
            new(TipoCalculadoraMobile.Estabilidade, "Estabilidade", "Indenização do período restante, com 13º, férias, FGTS e multa.", "Calcular estabilidade", typeof(EstabilidadeMobileViewModel)),
            new(TipoCalculadoraMobile.SeguroDesemprego, "Seguro-desemprego", "Valor e quantidade de parcelas para empregado CLT ou doméstico.", "Calcular seguro-desemprego", typeof(SeguroDesempregoMobileViewModel))
        ]),
        new("FGTS, afastamentos e benefícios",
        [
            new(TipoCalculadoraMobile.Afastamento, "Afastamentos e licenças", "Doença, acidente de trabalho e licenças: quem paga, quanto e até quando.", "Calcular afastamento", typeof(AfastamentoMobileViewModel)),
            new(TipoCalculadoraMobile.ConferenciaFgts, "Conferência do FGTS", "Devido e depositado por competência, com diferenças mensais e rescisórias.", "Conferir FGTS", typeof(ConferenciaFgtsMobileViewModel)),
            new(TipoCalculadoraMobile.SaqueAniversario, "Saque-aniversário do FGTS", "Valor do saque anual, saldo restante e parcela comprometida com o banco.", "Simular saque-aniversário", null),
            new(TipoCalculadoraMobile.AbonoSalarial, "Abono salarial (PIS/Pasep)", "Direito e valor do abono pelos meses trabalhados no ano-base.", "Calcular abono", typeof(AbonoSalarialMobileViewModel)),
            new(TipoCalculadoraMobile.Aposentadoria, "Simulação de aposentadoria", "Quando você cumpre cada regra do INSS e o benefício estimado.", "Simular aposentadoria", typeof(AposentadoriaMobileViewModel)),
            new(TipoCalculadoraMobile.PensaoMorte, "Pensão por morte", "Cota familiar, valor por dependente, duração e redutor de acumulação.", "Calcular pensão", typeof(PensaoMorteMobileViewModel))
        ])
    ];

    public static IEnumerable<CartaoCalculadoraMobile> Cartoes => Grupos.SelectMany(grupo => grupo.Cartoes);

    /// <summary>Formulário da tela comum de cálculo; exceção para as calculadoras com página própria.</summary>
    public static Type TipoViewModel(TipoCalculadoraMobile tipo) =>
        Cartoes.FirstOrDefault(cartao => cartao.Tipo == tipo)?.ViewModel
        ?? throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "A calculadora não usa a tela comum de cálculo.");
}
