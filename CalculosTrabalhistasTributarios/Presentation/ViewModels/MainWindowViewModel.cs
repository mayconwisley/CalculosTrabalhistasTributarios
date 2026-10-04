using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Reflection;
using System.Runtime.Versioning;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Tela inicial: só os atalhos para as calculadoras e as tabelas; cada cálculo tem a sua janela.</summary>
[SupportedOSPlatform("windows")]
public sealed class MainWindowViewModel : ViewModelBase
{
    private ThemeMode _temaSelecionado = ThemeManager.CurrentMode;
    private bool _historicoAberto;

    public MainWindowViewModel(IWindowNavigator navegador, HistoricoViewModel historico)
    {
        Historico = historico;
        AtalhoViewModel Calculadora(string titulo, string descricao, TipoCalculadora tipo) => Atalho(titulo, descricao, () => navegador.AbrirCalculadora(tipo));
        AtalhoViewModel Tabela(string titulo, string descricao, TipoTabelaTributaria tipo) => Atalho(titulo, descricao, () => navegador.AbrirTabela(tipo));

        Calculadoras =
        [
            new("Impostos e salário",
            [
                Atalho("Simulação tributária", "IRRF nas duas modalidades, INSS por faixas, salário líquido e FGTS.", navegador.AbrirSimulacaoTributaria),
                Calculadora("Salário bruto pelo líquido", "O salário bruto necessário para chegar a um líquido desejado.", TipoCalculadora.SalarioPeloLiquido),
                Calculadora("PLR (participação nos lucros)", "IRRF pela tabela anual exclusiva, sem INSS e sem FGTS.", TipoCalculadora.Plr),
                Calculadora("Pró-labore e autônomo", "Líquido do pró-labore ou do RPA e o custo para a empresa.", TipoCalculadora.ProLaboreAutonomo),
                Calculadora("CLT x PJ", "O que sobra para o profissional e o custo para a empresa nos dois regimes.", TipoCalculadora.CltPj),
                Calculadora("IRPF anual", "Declaração completa ou simplificada, redução anual e tributação mínima.", TipoCalculadora.IrpfAnual),
                Calculadora("Dividendos", "Retenção de 10% acima de R$ 50 mil no mês, desde 2026.", TipoCalculadora.Dividendos)
            ]),
            new("Pensão e débitos judiciais",
            [
                Atalho("Pensão alimentícia", "Um ou mais beneficiários, com a pensão deduzida da base do IRRF.", navegador.AbrirPensao),
                Calculadora("Revisão de pensão", "A pensão atual e a proposta lado a lado, com o efeito em quem paga.", TipoCalculadora.RevisaoPensao),
                Atalho("Pensão em atraso", "Débito corrigido, com juros e a separação entre prisão e penhora.", navegador.AbrirPensaoAtraso),
                Atalho("Débitos judiciais", "Atualização trabalhista e cível pelas fases do STF, do TST e da Lei 14.905/2024.", navegador.AbrirDebitoJudicial)
            ]),
            new("Remuneração e custos",
            [
                Calculadora("Holerite do mês", "Salário, horas, adicionais, faltas, vale-transporte, pensão e salário-família.", TipoCalculadora.Holerite),
                Atalho("Jornada pelo ponto", "Horas extras, noturnas, faltas e intervalos pelas marcações do mês.", navegador.AbrirJornada),
                Calculadora("Horas extras e adicionais", "Horas extras, adicional noturno e reflexo no DSR.", TipoCalculadora.HorasExtras),
                Calculadora("Insalubridade e periculosidade", "Adicionais pelo grau de insalubridade ou pela periculosidade.", TipoCalculadora.Adicionais),
                Calculadora("Salário-família", "Direito e valor das cotas pela remuneração e pelos filhos.", TipoCalculadora.SalarioFamilia),
                Calculadora("Custo do funcionário", "Encargos, provisões e benefícios pagos pela empresa.", TipoCalculadora.CustoFuncionario)
            ]),
            new("Férias, 13º e desligamento",
            [
                Calculadora("13º salário", "1ª e 2ª parcelas, com o INSS e o IRRF de dezembro.", TipoCalculadora.DecimoTerceiro),
                Calculadora("Férias", "Terço constitucional, venda de dias e adiantamento do 13º.", TipoCalculadora.Ferias),
                Calculadora("Rescisão", "Verbas pelo motivo do desligamento, com aviso prévio e multa do FGTS.", TipoCalculadora.Rescisao),
                Calculadora("Seguro-desemprego", "Parcelas e valor do benefício pela média dos últimos salários.", TipoCalculadora.SeguroDesemprego),
                Atalho("Estabilidade", "Indenização do período restante de estabilidade.", navegador.AbrirEstabilidade)
            ])
        ];

        Tabelas =
        [
            new("INSS e salário",
            [
                Tabela("Tabela INSS", "Faixas e alíquotas da contribuição do empregado.", TipoTabelaTributaria.Inss),
                Tabela("Salário mínimo", "Valor nacional, base do adicional de insalubridade.", TipoTabelaTributaria.SalarioMinimo),
                Tabela("Salário-família", "Limite de remuneração e cota por filho.", TipoTabelaTributaria.SalarioFamilia),
                Tabela("Seguro-desemprego", "Faixas da média salarial e valor máximo da parcela.", TipoTabelaTributaria.SeguroDesemprego)
            ]),
            new("Imposto de renda",
            [
                Tabela("Tabela IRRF", "Faixas mensais, alíquotas e parcelas a deduzir.", TipoTabelaTributaria.Irrf),
                Tabela("Redução mensal do IRRF", "Redução do imposto conforme os rendimentos do mês.", TipoTabelaTributaria.ReducaoMensalIrrf),
                Tabela("Tabela PLR", "Tabela anual exclusiva da participação nos lucros.", TipoTabelaTributaria.Plr),
                Tabela("Desconto mínimo", "IRRF até este valor não é retido (Lei 9.430/1996).", TipoTabelaTributaria.DescontoMinimo)
            ]),
            new("Deduções do IRRF",
            [
                Tabela("Valor simplificado", "Desconto que substitui as deduções legais.", TipoTabelaTributaria.Simplificado),
                Tabela("Dedução por dependente", "Valor deduzido da base do IRRF por dependente.", TipoTabelaTributaria.Dependente)
            ]),
            new("Índices econômicos",
            [
                Tabela("INPC", "Inflação mensal do IBGE, usada na correção monetária.", TipoTabelaTributaria.Inpc),
                Tabela("IPCA", "Inflação mensal oficial do IBGE, usada na correção monetária.", TipoTabelaTributaria.Ipca),
                Tabela("Taxa legal", "Juros de mora mensais desde 30/08/2024, publicados pelo Banco Central.", TipoTabelaTributaria.TaxaLegal),
                Tabela("Selic", "Taxa mensal do Banco Central, dos débitos judiciais até 29/08/2024.", TipoTabelaTributaria.Selic),
                Tabela("IPCA-E", "IPCA-15 mensal do IBGE, da fase pré-judicial trabalhista.", TipoTabelaTributaria.IpcaE),
                Tabela("TR", "Taxa referencial mensal, juros da fase pré-judicial trabalhista.", TipoTabelaTributaria.Tr)
            ])
        ];
    }

    public IReadOnlyList<GrupoAtalhosViewModel> Calculadoras { get; }
    public IReadOnlyList<GrupoAtalhosViewModel> Tabelas { get; }
    public HistoricoViewModel Historico { get; }

    /// <summary>Aba Histórico ativa: a lista é lida de novo cada vez que ela abre.</summary>
    public bool HistoricoAberto
    {
        get => _historicoAberto;
        set
        {
            if (SetProperty(ref _historicoAberto, value) && value)
                _ = Historico.CarregarAsync();
        }
    }
    public IReadOnlyList<ThemeMode> Temas { get; } = [ThemeMode.Automatico, ThemeMode.Claro, ThemeMode.Escuro];
    public string Versao { get; } = ObterVersao();

    public ThemeMode TemaSelecionado
    {
        get => _temaSelecionado;
        set
        {
            if (SetProperty(ref _temaSelecionado, value))
                ThemeManager.Apply(value);
        }
    }

    private static AtalhoViewModel Atalho(string titulo, string descricao, Action abrir) => new(titulo, descricao, new RelayCommand(_ => abrir()));

    // A versão vem da tag usada na publicação; o SDK acrescenta "+<commit>" à versão informativa, que não interessa ao usuário.
    private static string ObterVersao()
    {
        var versao = typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        var indiceCommit = versao.IndexOf('+');
        return indiceCommit >= 0 ? versao[..indiceCommit] : versao;
    }
}
