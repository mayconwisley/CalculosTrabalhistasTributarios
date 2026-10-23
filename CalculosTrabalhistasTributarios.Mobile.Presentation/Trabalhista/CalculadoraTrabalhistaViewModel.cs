using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public abstract partial class CalculadoraTrabalhistaViewModel(ICompartilhamentoPdf compartilhamento, ILogger logger) : ObservableObject, IFormularioHistoricoMobile
{
    protected static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly Dictionary<string, CampoMobile> _campos = [];
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResultado))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    private DemonstrativoDto? _demonstrativo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalcularCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    private bool _ocupado;

    public abstract string Titulo { get; }
    public long? HistoricoId { get; set; }
    public abstract string Descricao { get; }
    public IReadOnlyList<CampoMobile> Campos => _campos.Values.ToArray();
    public ObservableCollection<LinhaDepositoFgtsMobile> DepositosFgts { get; } = [];
    public PaginaLinhas<LinhaDepositoFgtsMobile> PaginaDepositosFgts => field ??= new(DepositosFgts);
    public virtual bool ExibirHistoricoFgts => false;
    public virtual bool TemDepositosFgts => false;
    public virtual ICommand? AdicionarDepositoCommand => null;
    public virtual ICommand? RemoverDepositoCommand => null;
    public virtual ICommand? GerarDepositosCommand => null;
    public virtual bool ExibirConsultaCotacoes => false;
    public virtual ICommand? ConsultaCotacoesCommand => null;
    public virtual bool PodeCalcularRra => false;
    public virtual ICommand? CalcularRraCommand => null;
    public virtual bool PodeLevarFgtsRescisao => false;
    public virtual ICommand? LevarFgtsRescisaoCommand => null;
    public virtual bool PodeTransferirJornada => false;
    public virtual ICommand? JornadaParaHoleriteCommand => null;
    public virtual ICommand? JornadaParaHorasExtrasCommand => null;
    public virtual string? AvisoHistorico => null;
    public ObservableCollection<LinhaBeneficioMobile> BeneficiosDetalhados { get; } = [];
    public virtual bool ExibirBeneficiosDetalhados => false;
    public virtual bool TemBeneficiosDetalhados => false;
    public virtual ICommand? AdicionarBeneficioCommand => null;
    public virtual ICommand? RemoverBeneficioCommand => null;
    public ObservableCollection<GradeMobile> Grades { get; } = [];
    public ObservableCollection<LinhaLancamentoBancoHorasMobile> LancamentosBancoHoras { get; } = [];
    public PaginaLinhas<LinhaLancamentoBancoHorasMobile> PaginaLancamentosBancoHoras => field ??= new(LancamentosBancoHoras);
    public virtual bool ExibirLancamentosBancoHoras => false;
    public virtual bool TemLancamentosBancoHoras => false;
    public virtual ICommand? AdicionarLancamentoCommand => null;
    public virtual ICommand? RemoverLancamentoCommand => null;
    public virtual ICommand? UsarNoHoleriteCommand => null;
    public virtual ICommand? UsarNaRescisaoCommand => null;
    public bool PodeUsarNoHolerite => UsarNoHoleriteCommand is not null && Demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Fechamento;
    public bool PodeUsarNaRescisao => UsarNaRescisaoCommand is not null && Demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Rescisao;
    public bool TemQuitacaoBancoHoras => QuitacaoBancoHoras is not null;
    public string QuitacaoBancoHorasTexto => QuitacaoBancoHoras is not { } quitacao ? string.Empty : string.Join("\n",
        [
            $"Fim do ciclo: {quitacao.FimCiclo.ToString("dd/MM/yyyy", Cultura)}",
            $"Salário de referência: {quitacao.SalarioReferencia.ToString("C2", Cultura)} • divisor {quitacao.Divisor.ToString("N2", Cultura)}",
            .. quitacao.Parcelas.Select(parcela => $"{parcela.Minutos / 60}:{parcela.Minutos % 60:00} h a {parcela.Adicional.ToString("N2", Cultura)}%: {parcela.Valor.ToString("C2", Cultura)}"),
            $"Total transferido: {quitacao.Total.ToString("C2", Cultura)} (INSS, IRRF e FGTS: sim)"
        ]);
    public bool TemResultado => Demonstrativo is not null;

    public DadosFormularioMobile ExportarDados() => new(1,
        _campos.Values.Where(campo => campo.EhEntrada).ToDictionary(campo => campo.Chave, Capturar),
        Grades.ToDictionary(grade => grade.Chave, grade => (IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>)
            grade.Linhas.Select(linha => (IReadOnlyDictionary<string, ValorCampoMobile>)linha.Campos.ToDictionary(campo => campo.Chave, Capturar)).ToArray()),
        DepositosFgts.Select(linha => new DepositoFgtsSalvoMobile(linha.Competencia, linha.Valor)).ToArray(),
        BeneficiosDetalhados.Select(linha => new BeneficioSalvoMobile(linha.Descricao, linha.Custo, linha.Participacao)).ToArray(),
        LancamentosBancoHoras.Select(linha => new LancamentoBancoSalvoMobile(linha.Data, linha.Tipo, linha.Horas, linha.Adicional, linha.Descricao)).ToArray(),
        QuitacaoBancoHoras, QuitacaoConfirmada);

    public Result TentarImportarDados(DadosFormularioMobile dados)
    {
        if (dados.Versao != 1 || dados.Campos is null || dados.Grades is null)
            return Erro.Validacao("O formato do formulário salvo não é compatível com esta versão.");
        foreach (var (chave, valor) in dados.Campos)
        {
            if (!_campos.TryGetValue(chave, out var campo) || campo.EhSecao || !ValorValido(campo, valor))
                return Erro.Validacao($"Campo {chave}: o valor salvo não é compatível com este formulário.");
        }
        foreach (var (chave, linhas) in dados.Grades)
        {
            var grade = Grades.FirstOrDefault(item => item.Chave == chave);
            if (grade is null || linhas is null || linhas.Count > grade.Limite)
                return Erro.Validacao($"Grade {chave}: as linhas salvas excedem o limite ou não pertencem ao formulário.");
            var colunas = grade.Colunas.ToDictionary(coluna => coluna.Chave,
                coluna => new CampoMobile(coluna.Chave, coluna.Rotulo, coluna.Dica, coluna.Tipo, coluna.Valor, coluna.Opcoes));
            foreach (var linha in linhas)
                if (linha is null || linha.Any(par => !colunas.TryGetValue(par.Key, out var campo) || !ValorValido(campo, par.Value)))
                    return Erro.Validacao($"Grade {chave}: uma linha contém campo ou opção inválida.");
        }
        if ((dados.DepositosFgts?.Count ?? 0) > 720 || (dados.Beneficios?.Count ?? 0) > 30 || (dados.LancamentosBanco?.Count ?? 0) > 500
            || (dados.LancamentosBanco?.Any(linha => linha is null || linha.Tipo is < 0 or > 1 || !TextoValido(linha.Data, 20)
                || !TextoValido(linha.Horas, 20) || !TextoValido(linha.Adicional, 40) || !TextoValido(linha.Descricao, 120)) ?? false)
            || (dados.DepositosFgts?.Any(linha => linha is null || !TextoValido(linha.Competencia, 20) || !TextoValido(linha.Valor, 40)) ?? false)
            || (dados.Beneficios?.Any(linha => linha is null || !TextoValido(linha.Descricao, 120) || !TextoValido(linha.Custo, 40)
                || !TextoValido(linha.Participacao, 40)) ?? false))
            return Erro.Validacao("Linhas salvas: há valores ou quantidades fora dos limites do formulário.");

        if (dados.QuitacaoBancoHoras is { } quitacao)
        {
            var chaveData = this is HoleriteMobileViewModel ? "competencia" : "desligamento";
            if (this is not (HoleriteMobileViewModel or RescisaoMobileViewModel)
                || !dados.Campos.TryGetValue(chaveData, out var campoData)
                || !DateOnly.TryParseExact(this is HoleriteMobileViewModel ? "01/" + campoData.Valor : campoData.Valor,
                    "dd/MM/yyyy", Cultura, System.Globalization.DateTimeStyles.None, out var dataDestino))
                return Erro.Validacao("Quitação do banco de horas: confira a competência ou a data de desligamento salva.");
            var validacao = quitacao.Validar(this is HoleriteMobileViewModel ? SituacaoBancoHoras.Fechamento : SituacaoBancoHoras.Rescisao, dataDestino);
            if (validacao.Falhou) return validacao;
        }

        foreach (var (chave, valor) in dados.Campos) Restaurar(_campos[chave], valor);
        foreach (var grade in Grades)
        {
            while (grade.Linhas.Count > 0) grade.Linhas.RemoveAt(grade.Linhas.Count - 1);
            if (!dados.Grades.TryGetValue(grade.Chave, out var linhas)) continue;
            foreach (var valores in linhas)
            {
                var linha = grade.AdicionarLinha();
                if (linha is null) break;
                foreach (var (chave, valor) in valores) Restaurar(linha.Campo(chave), valor);
            }
        }
        DepositosFgts.Clear();
        foreach (var linha in dados.DepositosFgts ?? []) DepositosFgts.Add(new() { Competencia = linha.Competencia, Valor = linha.Valor });
        BeneficiosDetalhados.Clear();
        foreach (var linha in dados.Beneficios ?? []) BeneficiosDetalhados.Add(new() { Descricao = linha.Descricao, Custo = linha.Custo, Participacao = linha.Participacao });
        LancamentosBancoHoras.Clear();
        foreach (var linha in dados.LancamentosBanco ?? []) LancamentosBancoHoras.Add(new()
        { Data = linha.Data, Tipo = linha.Tipo, Horas = linha.Horas, Adicional = linha.Adicional, Descricao = linha.Descricao });
        QuitacaoBancoHoras = dados.QuitacaoBancoHoras;
        QuitacaoConfirmada = dados.QuitacaoConfirmada && QuitacaoBancoHoras is not null;
        Demonstrativo = null;
        Mensagem = null;
        return Result.Ok();
    }

    private static ValorCampoMobile Capturar(CampoMobile campo) => new(campo.Valor, campo.Indice, campo.Marcado);
    private static bool TextoValido(string? texto, int limite) => texto is not null && texto.Length <= limite;
    private static bool ValorValido(CampoMobile campo, ValorCampoMobile? valor) => valor is not null
        && TextoValido(valor.Valor, 500)
        && (campo.Tipo != TipoCampoMobile.Opcao || valor.Indice >= 0 && valor.Indice < campo.Opcoes.Count);
    private static void Restaurar(CampoMobile campo, ValorCampoMobile valor)
    {
        campo.Valor = valor.Valor;
        campo.Indice = valor.Indice;
        campo.Marcado = valor.Marcado;
    }
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);
    partial void OnMensagemChanged(string? value) => OnPropertyChanged(nameof(TemMensagem));
    partial void OnDemonstrativoChanged(DemonstrativoDto? value)
    {
        OnPropertyChanged(nameof(PodeUsarNoHolerite));
        OnPropertyChanged(nameof(PodeUsarNaRescisao));
        OnPropertyChanged(nameof(PodeCalcularRra));
        OnPropertyChanged(nameof(PodeLevarFgtsRescisao));
        OnPropertyChanged(nameof(PodeTransferirJornada));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemQuitacaoBancoHoras))]
    [NotifyPropertyChangedFor(nameof(QuitacaoBancoHorasTexto))]
    private QuitacaoBancoHoras? _quitacaoBancoHoras;
    [ObservableProperty] private bool _quitacaoConfirmada;
    partial void OnQuitacaoBancoHorasChanged(QuitacaoBancoHoras? value) => Demonstrativo = null;
    partial void OnQuitacaoConfirmadaChanged(bool value)
    {
        Demonstrativo = null;
        Mensagem = null;
    }

    /// <summary>Recebe a quitação apurada no banco de horas; o usuário confirma as incidências antes de calcular.</summary>
    public void ReceberQuitacao(QuitacaoBancoHoras quitacao)
    {
        QuitacaoBancoHoras = quitacao;
        QuitacaoConfirmada = false;
        PreencherComQuitacao(quitacao);
    }

    protected virtual void PreencherComQuitacao(QuitacaoBancoHoras quitacao) { }

    protected QuitacaoBancoHoras? LerQuitacao() => QuitacaoBancoHoras switch
    {
        null => null,
        { } quitacao when QuitacaoConfirmada => quitacao,
        _ => throw new EntradaInvalidaException("Confira as incidências de INSS, IRRF e FGTS da quitação do banco de horas e marque a confirmação, ou descarte a transferência.")
    };

    [RelayCommand]
    private void DescartarQuitacao()
    {
        QuitacaoBancoHoras = null;
        QuitacaoConfirmada = false;
    }

    protected CampoMobile Adicionar(string chave, string rotulo, TipoCampoMobile tipo, string valor = "", string dica = "", IReadOnlyList<string>? opcoes = null)
    {
        var campo = new CampoMobile(chave, rotulo, dica, tipo, valor, opcoes);
        _campos.Add(chave, campo);
        campo.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CampoMobile.Valor) or nameof(CampoMobile.Indice) or nameof(CampoMobile.Marcado))
            {
                Demonstrativo = null;
                Mensagem = null;
                AoAlterar(campo);
            }
        };
        return campo;
    }

    /// <summary>Título que agrupa os campos seguintes; não é lido no cálculo.</summary>
    protected CampoMobile Secao(string chave, string titulo) => Adicionar(chave, titulo, TipoCampoMobile.Secao);

    protected virtual void AoAlterar(CampoMobile campo) { }
    protected CampoMobile Campo(string chave) => _campos[chave];
    protected string Texto(string chave) => Campo(chave).Valor.Trim();
    protected bool Sim(string chave) => Campo(chave).Marcado;
    protected int Opcao(string chave) => LeituraCampoMobile.Opcao(Campo(chave));
    protected decimal Numero(string chave) => LeituraCampoMobile.Numero(Campo(chave));
    protected int Inteiro(string chave) => LeituraCampoMobile.Inteiro(Campo(chave));
    protected decimal Horas(string chave) => LeituraCampoMobile.Horas(Campo(chave));
    protected DateOnly Data(string chave) => LeituraCampoMobile.Data(Campo(chave));
    protected DateOnly? DataOpcional(string chave) => LeituraCampoMobile.DataOpcional(Campo(chave));
    protected DateOnly Competencia(string chave) => LeituraCampoMobile.Competencia(Campo(chave));

    protected GradeMobile AdicionarGrade(string chave, string titulo, string descricao, string nomeLinha, int limite,
        IReadOnlyList<ColunaGradeMobile> colunas, Action<LinhaGradeMobile>? aoAlterarLinha = null)
    {
        var grade = new GradeMobile(chave, titulo, descricao, nomeLinha, limite, colunas, aoAlterarLinha);
        grade.Alterada += (_, _) =>
        {
            Demonstrativo = null;
            Mensagem = null;
        };
        Grades.Add(grade);
        return grade;
    }

    protected GradeMobile Grade(string chave) => Grades.First(grade => grade.Chave == chave);
    protected void AdicionarPensao()
    {
        Adicionar("pensaoBase", "Pensão alimentícia", TipoCampoMobile.Opcao, dica: "Selecione a base definida na decisão ou no acordo.",
            opcoes: ["Não informar", "Rendimentos líquidos", "Rendimentos brutos", "Salário mínimo", "Valor fixo"]);
        Adicionar("pensaoPercentual", "Pensão (%)", TipoCampoMobile.Numero, "0", "Percentual da base escolhida.");
        Adicionar("pensaoValor", "Pensão fixa (R$)", TipoCampoMobile.Moeda, "0,00", "Valor fixo definido na decisão ou no acordo.");
        AtualizarPensao();
    }

    protected RegraPensao? Pensao() => Opcao("pensaoBase") switch
    {
        1 => new(BasePensao.RendimentosLiquidos, Numero("pensaoPercentual"), 0m),
        2 => new(BasePensao.RendimentosBrutos, Numero("pensaoPercentual"), 0m),
        3 => new(BasePensao.SalarioMinimo, Numero("pensaoPercentual"), 0m),
        4 => new(BasePensao.ValorFixo, 0m, Numero("pensaoValor")),
        _ => null
    };

    protected void AtualizarPensao()
    {
        var baseSelecionada = Campo("pensaoBase").Indice;
        Campo("pensaoPercentual").Visivel = baseSelecionada is >= 1 and <= 3;
        Campo("pensaoValor").Visivel = baseSelecionada == 4;
    }

    protected abstract Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken);
    private bool PodeCalcular() => !Ocupado;
    private bool PodeCompartilhar() => !Ocupado && TemResultado;

    [RelayCommand(CanExecute = nameof(PodeCalcular))]
    private async Task CalcularAsync(CancellationToken cancellationToken)
    {
        Demonstrativo = null;
        Mensagem = null;
        try
        {
            Ocupado = true;
            var resultado = await ApurarAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (resultado.Falhou) Mensagem = resultado.Erro.Mensagem;
            else Demonstrativo = resultado.Valor;
        }
        catch (EntradaInvalidaException erro) { Mensagem = erro.Message; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha na calculadora móvel {Calculadora}.", Titulo);
            Mensagem = "Não foi possível calcular. Confira os dados e tente novamente.";
        }
        finally { Ocupado = false; }
    }

    [RelayCommand(CanExecute = nameof(PodeCompartilhar))]
    private async Task CompartilharPdfAsync(CancellationToken cancellationToken)
    {
        if (Demonstrativo is not { } demonstrativo) return;
        Ocupado = true;
        Mensagem = null;
        try { await compartilhamento.CompartilharAsync(demonstrativo, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao compartilhar PDF da calculadora {Calculadora}.", Titulo);
            Mensagem = "Não foi possível compartilhar o PDF. Tente novamente.";
        }
        finally { Ocupado = false; }
    }
}
