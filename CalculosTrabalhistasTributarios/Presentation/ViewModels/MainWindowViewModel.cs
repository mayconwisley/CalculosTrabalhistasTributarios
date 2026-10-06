using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Tela inicial: só os atalhos para as calculadoras e as tabelas; cada cálculo tem a sua janela.</summary>
[SupportedOSPlatform("windows")]
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IVerificarAtualizacoesUseCase _verificarAtualizacoes;
    private readonly IRegistroDeErros _registroDeErros;
    private readonly IWindowNavigator _navegador;
    private readonly IBaixadorDeAtualizacao _baixador;
    private readonly IExecutorInstalador _executor;
    private readonly IUserNotifier _notificador;
    private ThemeMode _temaSelecionado = ThemeManager.CurrentMode;
    private bool _historicoAberto;

    public MainWindowViewModel(IWindowNavigator navegador, HistoricoViewModel historico, IVerificarAtualizacoesUseCase verificarAtualizacoes, IRegistroDeErros registroDeErros,
        IBaixadorDeAtualizacao baixador, IExecutorInstalador executor, IUserNotifier notificador)
    {
        _baixador = baixador;
        _executor = executor;
        _notificador = notificador;
        Historico = historico;
        _navegador = navegador;
        _verificarAtualizacoes = verificarAtualizacoes;
        _registroDeErros = registroDeErros;
        AtalhoViewModel Calculadora(string titulo, string descricao, TipoCalculadora tipo) => Atalho(titulo, descricao, () => navegador.AbrirCalculadora(tipo));
        AtalhoViewModel Tabela(string titulo, string descricao, TipoTabelaTributaria tipo) => Atalho(titulo, descricao, () => navegador.AbrirTabela(tipo));

        Calculadoras =
        [
            new("Impostos e salário",
            [
                Atalho("Simulação tributária", "IRRF nas duas modalidades, INSS por faixas, salário líquido e FGTS.", navegador.AbrirSimulacaoTributaria),
                Calculadora("INSS em múltiplos vínculos", "Distribuição do desconto entre empregos e serviços, respeitando o teto mensal.", TipoCalculadora.InssMultiplosVinculos),
                Calculadora("Salário bruto pelo líquido", "O salário bruto necessário para chegar a um líquido desejado.", TipoCalculadora.SalarioPeloLiquido),
                Calculadora("PLR (participação nos lucros)", "IRRF pela tabela anual exclusiva, sem INSS e sem FGTS.", TipoCalculadora.Plr),
                Calculadora("Pró-labore e autônomo", "Líquido do pró-labore ou do RPA e o custo para a empresa.", TipoCalculadora.ProLaboreAutonomo),
                Calculadora("CLT x PJ", "O que sobra para o profissional e o custo para a empresa nos dois regimes.", TipoCalculadora.CltPj),
                Calculadora("IRPF anual", "Declaração completa ou simplificada, redução anual e tributação mínima.", TipoCalculadora.IrpfAnual),
                Calculadora("Carnê-leão", "IR mensal de honorários e aluguéis recebidos de pessoas físicas.", TipoCalculadora.CarneLeao),
                Calculadora("Ganho de capital", "IR na venda de imóvel ou outro bem, com isenções e fatores de redução.", TipoCalculadora.GanhoCapital),
                Calculadora("Dividendos", "Retenção de 10% acima de R$ 50 mil no mês, desde 2026.", TipoCalculadora.Dividendos),
                Calculadora("Tributo em atraso", "Multa e juros pela Selic de DARF, DAS, DAE ou GPS pago depois do vencimento.", TipoCalculadora.TributoAtraso)
            ]),
            new("Pensão e débitos judiciais",
            [
                Atalho("Pensão alimentícia", "Um ou mais beneficiários, com a pensão deduzida da base do IRRF.", navegador.AbrirPensao),
                Calculadora("Revisão de pensão", "A pensão atual e a proposta lado a lado, com o efeito em quem paga.", TipoCalculadora.RevisaoPensao),
                Atalho("Pensão em atraso", "Débito corrigido, com juros e a separação entre prisão e penhora.", navegador.AbrirPensaoAtraso),
                Atalho("Débitos judiciais", "Atualização trabalhista e cível pelas fases do STF, do TST e da Lei 14.905/2024.", navegador.AbrirDebitoJudicial),
                Calculadora("Correção de valores", "Valor atualizado pelo IPCA, INPC, Selic ou outro índice, com juros e multa.", TipoCalculadora.CorrecaoValor)
            ]),
            new("Remuneração e custos",
            [
                Calculadora("Holerite do mês", "Salário, horas, adicionais, faltas, vale-transporte, pensão e salário-família.", TipoCalculadora.Holerite),
                Calculadora("Comissões e DSR", "Repouso sobre comissões e impacto no INSS, IRRF, FGTS e líquido.", TipoCalculadora.Comissoes),
                Calculadora("Diferenças de reajuste retroativo", "Salários pagos e devidos por competência, com 13º, férias e FGTS.", TipoCalculadora.ReajusteRetroativo),
                Atalho("Jornada pelo ponto", "Horas extras, noturnas, faltas e intervalos pelas marcações do mês.", navegador.AbrirJornada),
                Calculadora("Horas extras e adicionais", "Horas extras, adicional noturno e reflexo no DSR.", TipoCalculadora.HorasExtras),
                Calculadora("Insalubridade e periculosidade", "Adicionais pelo grau de insalubridade ou pela periculosidade.", TipoCalculadora.Adicionais),
                Calculadora("Salário-família", "Direito e valor das cotas pela remuneração e pelos filhos.", TipoCalculadora.SalarioFamilia),
                Calculadora("Custo do funcionário", "Encargos, provisões e benefícios pagos pela empresa.", TipoCalculadora.CustoFuncionario),
                Calculadora("Empregado doméstico (DAE)", "Salário líquido do doméstico e o DAE do mês, com os encargos do empregador.", TipoCalculadora.Domestico),
                Calculadora("Estágio", "Bolsa líquida do estagiário, com o IRRF, e o recesso proporcional.", TipoCalculadora.Estagio),
                Calculadora("Trabalho intermitente", "Pagamento de cada convocação: horas, DSR, férias, 13º e FGTS.", TipoCalculadora.Intermitente)
            ]),
            new("Férias, 13º e desligamento",
            [
                Calculadora("13º salário", "1ª e 2ª parcelas, com o INSS e o IRRF de dezembro.", TipoCalculadora.DecimoTerceiro),
                Calculadora("Férias", "Terço constitucional, venda de dias e adiantamento do 13º.", TipoCalculadora.Ferias),
                Calculadora("Rescisão", "Verbas pelo motivo do desligamento, com aviso prévio e multa do FGTS.", TipoCalculadora.Rescisao),
                Calculadora("Seguro-desemprego", "Parcelas e valor do benefício pela média dos últimos salários.", TipoCalculadora.SeguroDesemprego),
                Atalho("Estabilidade", "Indenização do período restante de estabilidade.", navegador.AbrirEstabilidade)
            ]),
            new("FGTS, afastamentos e benefícios",
            [
                Calculadora("Afastamentos e licenças", "Doença, acidente de trabalho e licenças: quem paga, quanto e até quando.", TipoCalculadora.Afastamento),
                Calculadora("Saque-aniversário do FGTS", "O valor que pode ser sacado no aniversário e o efeito numa dispensa.", TipoCalculadora.SaqueAniversario),
                Calculadora("Abono salarial (PIS/Pasep)", "Direito e valor do abono pelos meses trabalhados no ano-base.", TipoCalculadora.AbonoSalarial)
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
    public int QuantidadeCalculadoras => Calculadoras.Sum(grupo => grupo.Atalhos.Count);

    /// <summary>Versão nova publicada e tabelas do ano ainda não cadastradas, verificadas ao abrir.</summary>
    public ObservableCollection<AvisoInicioViewModel> Avisos { get; } = [];

    /// <summary>Desligada, a abertura não acessa a internet; a verificação das tabelas, local, continua.</summary>
    public bool VerificarNovasVersoes
    {
        get => ConfiguracoesUsuario.Atuais.VerificarNovasVersoes;
        set
        {
            if (value == VerificarNovasVersoes) return;
            ConfiguracoesUsuario.Alterar(configuracoes => configuracoes.VerificarNovasVersoes = value);
            OnPropertyChanged();
        }
    }
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

    /// <summary>Chamado pela janela ao abrir. Um aviso que não pôde ser verificado simplesmente não aparece.</summary>
    public async Task CarregarAvisosAsync()
    {
        try
        {
            var avisos = await _verificarAtualizacoes.ExecutarAsync(Versao, VerificarNovasVersoes, DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
            Avisos.Clear();
            foreach (var aviso in avisos)
                Avisos.Add(Criar(aviso));
        }
        catch (Exception exception)
        {
            _registroDeErros.Registrar(exception, "Verificação de atualizações ao abrir");
        }
    }

    private AvisoInicioViewModel Criar(AvisoAtualizacaoDto aviso)
    {
        AvisoInicioViewModel? criado = null;
        var dispensar = new RelayCommand(_ => Avisos.Remove(criado!));
        if (aviso.Tipo != TipoAvisoAtualizacao.NovaVersao || aviso.Endereco is not { } pagina)
            return criado = new(aviso.Mensagem, "Abrir a tabela do INSS", new RelayCommand(_ => _navegador.AbrirTabela(TipoTabelaTributaria.Inss)), dispensar);

        var abrirPagina = new RelayCommand(_ => Process.Start(new ProcessStartInfo(pagina.AbsoluteUri) { UseShellExecute = true }));
        // Sem o instalador com o hash publicado, não há como conferir o download: o aviso só abre a página do release.
        if (aviso.Versao is not { PodeAtualizarPeloAplicativo: true } versao)
            return criado = new(aviso.Mensagem, "Baixar a nova versão", abrirPagina, dispensar);
        return criado = new(aviso.Mensagem, "Atualizar agora", new AsyncRelayCommand(() => AtualizarAsync(criado!, versao)), dispensar, "Ver novidades", abrirPagina);
    }

    /// <summary>
    /// Baixa o instalador, confere o hash e o executa; o aplicativo fecha para os arquivos serem substituídos e é aberto
    /// de novo no fim da instalação. As tabelas cadastradas ficam preservadas.
    /// </summary>
    private async Task AtualizarAsync(AvisoInicioViewModel aviso, VersaoPublicada versao)
    {
        if (!_notificador.Confirmar(
                $"Baixar e instalar a versão {versao.Numero}?\n\nO aplicativo será fechado durante a instalação e aberto de novo no fim. As tabelas cadastradas e o histórico são preservados. Salve os cálculos abertos antes de continuar.",
                "Atualizar o aplicativo"))
            return;

        var textoOriginal = aviso.Texto;
        var progresso = new Progress<int>(percentual => aviso.Texto = $"Baixando a versão {versao.Numero}: {percentual}%...");
        aviso.Texto = $"Baixando a versão {versao.Numero}...";
        var download = await _baixador.BaixarAsync(versao, progresso, CancellationToken.None);
        if (download.Falhou)
        {
            aviso.Texto = textoOriginal;
            _notificador.MostrarFalha(download.Erro, "Não foi possível atualizar o aplicativo.");
            return;
        }
        aviso.Texto = $"Instalando a versão {versao.Numero}...";
        _executor.InstalarEFechar(download.Valor);
    }

    // A versão vem da tag usada na publicação; o SDK acrescenta "+<commit>" à versão informativa, que não interessa ao usuário.
    private static string ObterVersao()
    {
        var versao = typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        var indiceCommit = versao.IndexOf('+');
        return indiceCommit >= 0 ? versao[..indiceCommit] : versao;
    }
}
