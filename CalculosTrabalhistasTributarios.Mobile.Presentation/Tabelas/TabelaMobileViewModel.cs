using System.Collections.ObjectModel;
using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Tabelas;

/// <summary>
/// Consulta e manutenção de uma tabela local, com a atualização pela internet, como a tela de tabela do desktop. O banco
/// é lido fora da thread da tela, porque o SQLite trabalha de forma síncrona por dentro.
/// </summary>
public sealed partial class TabelaMobileViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    // Até seis casas: o multiplicador da redução mensal (ex.: 0,133145) e a taxa legal não podem ser arredondados ao serem editados.
    private const string FormatoDecimal = "#,##0.00####";
    // Índices desde 1994 passam de 300 meses: a lista mostra uma página por vez, sem rolagem própria.
    private const int LinhasPorPagina = 20;
    private readonly ITabelaTributariaService _service;
    private readonly IAtualizadorTabelas _atualizador;
    private readonly IPlataformaMobile _plataforma;
    private readonly ILogger<TabelaMobileViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoSalvar))]
    [NotifyPropertyChangedFor(nameof(TemSelecionado))]
    [NotifyCanExecuteChangedFor(nameof(ExcluirCommand))]
    private LinhaRegistroTabelaMobile? _selecionado;
    [ObservableProperty] private string _competencia = string.Empty;
    [ObservableProperty] private string _faixa = "1";
    [ObservableProperty] private string _valor = "0,00";
    [ObservableProperty] private string _aliquota = "0,00";
    [ObservableProperty] private string _deducao = "0,00";
    [ObservableProperty] private string _statusAtualizacao = "Dados exibidos localmente.";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemRegistros))]
    private bool _carregando = true;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SalvarCommand), nameof(ExcluirCommand), nameof(AtualizarPelaInternetCommand), nameof(CarregarCommand))]
    private bool _ocupado;

    public TabelaMobileViewModel(TipoTabelaTributaria tipo, ITabelaTributariaService service, IAtualizadorTabelas atualizador, IPlataformaMobile plataforma,
        ILogger<TabelaMobileViewModel> logger)
    {
        Tipo = tipo;
        _service = service;
        _atualizador = atualizador;
        _plataforma = plataforma;
        _logger = logger;
        Pagina = new PaginaLinhas<LinhaRegistroTabelaMobile>(Registros, LinhasPorPagina);
        Registros.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SemRegistros));
        LimparFormulario();
    }

    public TipoTabelaTributaria Tipo { get; }
    public string Titulo => CatalogoTabelasMobile.Cartao(Tipo).Titulo;
    public string Descricao => Tipo switch
    {
        TipoTabelaTributaria.ReducaoMensalIrrf => "Redução aplicada ao imposto depois da tabela progressiva.",
        TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca => "Variação mensal do índice, usada na correção monetária.",
        TipoTabelaTributaria.SeguroDesemprego => "Faixas da média salarial: percentual sobre o excedente e valor fixo; a última é o valor máximo. Reajustada todo ano em janeiro.",
        TipoTabelaTributaria.TaxaLegal => "Taxa de cada mês publicada pelo Banco Central (Selic menos IPCA-15), para os juros de mora desde 30/08/2024.",
        TipoTabelaTributaria.Selic => "Selic acumulada em cada mês, do Banco Central, para os débitos judiciais até 29/08/2024 (correção e juros juntos).",
        TipoTabelaTributaria.IpcaE => "Variação mensal do IPCA-15 (IBGE), usada como IPCA-E na fase pré-judicial dos débitos trabalhistas.",
        TipoTabelaTributaria.Tr => "TR do primeiro dia de cada mês, do Banco Central, para os juros da fase pré-judicial dos débitos trabalhistas.",
        _ => ExibeFaixa ? "Cada faixa vale a partir da sua competência, até a próxima competência cadastrada." : "Cada valor vale a partir da sua competência, até a próxima competência cadastrada."
    };
    public string TituloLista => ExibeFaixa ? "Faixas cadastradas" : "Valores cadastrados";
    public string LabelValor => Tipo switch
    {
        TipoTabelaTributaria.ReducaoMensalIrrf => "Limite de rendimentos (R$)",
        TipoTabelaTributaria.Inss or TipoTabelaTributaria.Irrf => "Limite da base (R$)",
        TipoTabelaTributaria.Plr => "Limite da PLR anual (R$)",
        TipoTabelaTributaria.SalarioFamilia => "Limite de remuneração (R$)",
        TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca or TipoTabelaTributaria.IpcaE => "Variação no mês (%)",
        TipoTabelaTributaria.TaxaLegal or TipoTabelaTributaria.Selic or TipoTabelaTributaria.Tr => "Taxa no mês (%)",
        TipoTabelaTributaria.SeguroDesemprego => "Limite da média (R$)",
        _ => "Valor (R$)"
    };
    public string LabelAliquota => Tipo switch
    {
        TipoTabelaTributaria.ReducaoMensalIrrf => "Multiplicador",
        TipoTabelaTributaria.SeguroDesemprego => "% sobre o excedente",
        _ => "Alíquota (%)"
    };
    public string LabelDeducao => Tipo switch
    {
        TipoTabelaTributaria.ReducaoMensalIrrf => "Valor-base da redução (R$)",
        TipoTabelaTributaria.SalarioFamilia => "Cota por filho (R$)",
        TipoTabelaTributaria.SeguroDesemprego => "Valor fixo (R$)",
        _ => "Parcela a deduzir (R$)"
    };
    public bool ExibeFaixa => Tipo is TipoTabelaTributaria.Inss or TipoTabelaTributaria.Irrf or TipoTabelaTributaria.ReducaoMensalIrrf or TipoTabelaTributaria.Plr
        or TipoTabelaTributaria.SalarioFamilia or TipoTabelaTributaria.SeguroDesemprego;
    // O salário-família tem faixas, mas cada uma é um limite de remuneração com uma cota, sem alíquota.
    public bool ExibeAliquota => Tipo is TipoTabelaTributaria.Inss or TipoTabelaTributaria.Irrf or TipoTabelaTributaria.ReducaoMensalIrrf or TipoTabelaTributaria.Plr
        or TipoTabelaTributaria.SeguroDesemprego;
    public bool ExibeDeducao => Tipo is TipoTabelaTributaria.Irrf or TipoTabelaTributaria.ReducaoMensalIrrf or TipoTabelaTributaria.Plr
        or TipoTabelaTributaria.SalarioFamilia or TipoTabelaTributaria.SeguroDesemprego;
    public bool EhIndice => DescricaoAtualizacaoTabelas.EhIndice(Tipo);
    public string TextoFonteOficial => _atualizador.NomeFonteOficial(Tipo) switch
    {
        "Receita Federal" => "Abrir a página oficial da Receita Federal",
        var nome => $"Abrir a página oficial do {nome}"
    };

    public ObservableCollection<LinhaRegistroTabelaMobile> Registros { get; } = [];
    public PaginaLinhas<LinhaRegistroTabelaMobile> Pagina { get; }
    public bool SemRegistros => !Carregando && Registros.Count == 0;
    public bool TemSelecionado => Selecionado is not null;
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    // Sem linha selecionada, salvar cria um registro; o rótulo deixa claro qual das duas operações será feita.
    public string TextoSalvar => Selecionado is null ? "Incluir registro" : "Salvar alteração";

    /// <summary>Chamado ao abrir a tela.</summary>
    public Task CarregarAsync() => CarregarAsync(null);

    [RelayCommand]
    private void Selecionar(LinhaRegistroTabelaMobile linha)
    {
        if (Selecionado is not null) Selecionado.Selecionada = false;
        Selecionado = ReferenceEquals(Selecionado, linha) ? null : linha;
        if (Selecionado is null)
        {
            LimparFormulario();
            return;
        }
        Selecionado.Selecionada = true;
        Preencher(Selecionado.Registro);
    }

    [RelayCommand]
    private void Novo()
    {
        if (Selecionado is not null) Selecionado.Selecionada = false;
        Selecionado = null;
        Mensagem = null;
        LimparFormulario();
    }

    private bool Livre() => !Ocupado;
    private bool PodeExcluir() => !Ocupado && Selecionado is not null;

    [RelayCommand(CanExecute = nameof(Livre))]
    private Task Carregar() => ExecutarAsync("Não foi possível ler os registros da tabela.", () => CarregarAsync(Selecionado?.Registro.Id));

    [RelayCommand(CanExecute = nameof(Livre))]
    private Task Salvar() => ExecutarAsync("Não foi possível salvar o registro.", async () =>
    {
        if (!Ler(out var request))
        {
            Mensagem = "Informe a competência em mm/aaaa e valores numéricos válidos (sem negativos, exceto na variação de um índice).";
            return;
        }
        var salvo = await Task.Run(() => _service.SalvarAsync(Tipo, request, CancellationToken.None));
        if (salvo.Falhou)
        {
            Mensagem = salvo.Erro.Mensagem;
            return;
        }
        await CarregarAsync(request.Id == 0 ? null : request.Id);
        // Após incluir, o registro criado (o de maior Id com a mesma competência e faixa) fica selecionado.
        if (request.Id == 0 && Registros.Where(linha => linha.Registro.Competencia == request.Competencia && linha.Registro.Faixa == request.Faixa)
                .MaxBy(linha => linha.Registro.Id) is { } criado)
        {
            Selecionar(criado);
            Pagina.Exibir(criado);
        }
        StatusAtualizacao = request.Id == 0 ? "Registro incluído." : "Alteração salva.";
    });

    [RelayCommand(CanExecute = nameof(PodeExcluir))]
    private Task Excluir() => ExecutarAsync("Não foi possível excluir o registro.", async () =>
    {
        if (Selecionado is not { } linha) return;
        var descricao = ExibeFaixa ? $"a faixa {linha.Faixa} de {linha.Competencia}" : $"o valor de {linha.Competencia}";
        if (!await _plataforma.ConfirmarAsync("Excluir registro", $"Excluir {descricao}? Os cálculos dessa competência passam a usar o registro anterior.", "Excluir", "Cancelar"))
            return;
        var excluido = await Task.Run(() => _service.ExcluirAsync(Tipo, linha.Registro.Id, CancellationToken.None));
        if (excluido.Falhou)
            Mensagem = excluido.Erro.Mensagem;
        await CarregarAsync(null);
        if (excluido.Sucesso)
            StatusAtualizacao = "Registro excluído.";
    });

    [RelayCommand(CanExecute = nameof(Livre))]
    private Task AtualizarPelaInternet() => ExecutarAsync("Não foi possível atualizar a tabela pela internet.", async () =>
    {
        StatusAtualizacao = "Consultando a fonte oficial e as fontes alternativas...";
        try
        {
            var atualizacao = await Task.Run(() => _atualizador.AtualizarAsync(Tipo, CancellationToken.None));
            if (atualizacao.Falhou)
            {
                StatusAtualizacao = "A atualização não foi concluída; os dados locais foram preservados.";
                Mensagem = atualizacao.Erro.Mensagem;
                return;
            }
            await CarregarAsync(Selecionado?.Registro.Id);
            StatusAtualizacao = DescricaoAtualizacaoTabelas.DescreverAtualizacao(Tipo, atualizacao.Valor);
        }
        catch
        {
            StatusAtualizacao = "A atualização não foi concluída; os dados locais foram preservados.";
            throw;
        }
    });

    [RelayCommand]
    private Task AbrirFonteOficial() => _plataforma.AbrirEnderecoAsync(_atualizador.FonteOficial(Tipo));

    private async Task ExecutarAsync(string falha, Func<Task> acao)
    {
        Ocupado = true;
        Mensagem = null;
        try { await acao(); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha na tabela {Tabela}.", Tipo);
            Mensagem = $"{falha} Tente novamente.";
        }
        finally { Ocupado = false; }
    }

    /// <summary>Recarrega a lista mantendo selecionado o registro informado; se ele não existir mais, volta ao modo de inclusão.</summary>
    private async Task CarregarAsync(int? idSelecionado)
    {
        Carregando = true;
        IReadOnlyList<RegistroTabelaDto> registros;
        try
        {
            registros = await Task.Run(() => _service.ListarAsync(Tipo, CancellationToken.None));
        }
        finally
        {
            Carregando = false;
        }
        Selecionado = null;
        Registros.Clear();
        foreach (var registro in registros) Registros.Add(Criar(registro));
        if (Registros.FirstOrDefault(linha => linha.Registro.Id == idSelecionado) is { } linha)
        {
            Selecionar(linha);
            Pagina.Exibir(linha);
        }
        else
            LimparFormulario();
    }

    private static LinhaRegistroTabelaMobile Criar(RegistroTabelaDto registro) => new(registro,
        registro.Competencia.ToString("MM/yyyy", Cultura),
        registro.Faixa?.ToString(Cultura) ?? string.Empty,
        registro.Valor.ToString(FormatoDecimal, Cultura),
        registro.Aliquota?.ToString(FormatoDecimal, Cultura) ?? string.Empty,
        registro.Deducao?.ToString("N2", Cultura) ?? string.Empty);

    private void LimparFormulario()
    {
        Competencia = DateTime.Today.ToString("MM/yyyy", Cultura);
        Faixa = "1";
        Valor = Aliquota = Deducao = "0,00";
    }

    private void Preencher(RegistroTabelaDto registro)
    {
        Competencia = registro.Competencia.ToString("MM/yyyy", Cultura);
        Faixa = registro.Faixa?.ToString(Cultura) ?? "1";
        Valor = registro.Valor.ToString(FormatoDecimal, Cultura);
        Aliquota = registro.Aliquota?.ToString(FormatoDecimal, Cultura) ?? "0,00";
        Deducao = registro.Deducao?.ToString("N2", Cultura) ?? "0,00";
    }

    private bool Ler(out SalvarRegistroTabelaRequest request)
    {
        request = null!;
        var valido = DateOnly.TryParseExact(Competencia?.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var competencia)
            & LeituraNumerica.TentarLer(Valor, out var valor);
        valido &= valor >= 0m || Tipo is TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca or TipoTabelaTributaria.IpcaE;
        int? faixa = null;
        decimal? aliquota = null, deducao = null;
        if (ExibeFaixa) { valido &= int.TryParse(Faixa, NumberStyles.None, Cultura, out var f) && f > 0; faixa = f; }
        if (ExibeAliquota) { valido &= LeituraNumerica.TentarLer(Aliquota, out var a) && a >= 0m; aliquota = a; }
        if (ExibeDeducao) { valido &= LeituraNumerica.TentarLer(Deducao, out var d) && d >= 0m; deducao = d; }
        if (!valido) return false;
        request = new SalvarRegistroTabelaRequest(Selecionado?.Registro.Id ?? 0, competencia, faixa, valor, aliquota, deducao);
        return true;
    }
}
