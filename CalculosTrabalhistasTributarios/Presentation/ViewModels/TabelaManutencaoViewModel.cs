using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class TabelaManutencaoViewModel : ViewModelBase
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    // Até seis casas: o multiplicador da redução mensal (ex.: 0,133145) não pode ser arredondado ao ser editado.
    private const string FormatoAliquota = "#,##0.00####";
    // A taxa legal tem seis casas decimais; os valores em reais continuam com duas.
    private const string FormatoValor = "#,##0.00####";
    private readonly ITabelaTributariaService _service;
    private readonly IAtualizadorTabelas _atualizador;
    private readonly IUserNotifier _notificador;
    private RegistroTabelaDto? _selecionado;
    private string _competencia = DateTime.Today.ToString("MM/yyyy", Cultura);
    private string _faixa = "1";
    private string _valor = "0,00";
    private string _aliquota = "0,00";
    private string _deducao = "0,00";
    private string _statusAtualizacao = "Dados exibidos localmente.";

    public TabelaManutencaoViewModel(
        TipoTabelaTributaria tipo,
        ITabelaTributariaService service,
        IAtualizadorTabelas atualizador,
        IUserNotifier notificador)
    {
        Tipo = tipo; _service = service; _atualizador = atualizador; _notificador = notificador;
        SalvarCommand = new AsyncRelayCommand(SalvarAsync);
        ExcluirCommand = new AsyncRelayCommand(ExcluirAsync, () => Selecionado is not null);
        CarregarCommand = new AsyncRelayCommand(() => CarregarAsync(Selecionado?.Id));
        NovoCommand = new RelayCommand(_ => NovoRegistro());
        AtualizarPelaInternetCommand = new AsyncRelayCommand(AtualizarPelaInternetAsync);
        AbrirFonteOficialCommand = new RelayCommand(_ => AbrirFonteOficial());
    }

    public TipoTabelaTributaria Tipo { get; }
    public string Titulo => Tipo switch
    {
        TipoTabelaTributaria.Inss => "Tabela INSS",
        TipoTabelaTributaria.Irrf => "Tabela IRRF",
        TipoTabelaTributaria.Simplificado => "Valor simplificado",
        TipoTabelaTributaria.Dependente => "Dedução por dependente",
        TipoTabelaTributaria.DescontoMinimo => "Desconto mínimo",
        TipoTabelaTributaria.SalarioMinimo => "Salário mínimo",
        TipoTabelaTributaria.SalarioFamilia => "Salário-família",
        TipoTabelaTributaria.Plr => "Tabela PLR",
        TipoTabelaTributaria.Inpc => "INPC",
        TipoTabelaTributaria.Ipca => "IPCA",
        TipoTabelaTributaria.TaxaLegal => "Taxa legal",
        TipoTabelaTributaria.Selic => "Selic",
        TipoTabelaTributaria.IpcaE => "IPCA-E",
        TipoTabelaTributaria.Tr => "TR",
        TipoTabelaTributaria.SeguroDesemprego => "Seguro-desemprego",
        _ => "Redução mensal do IRRF"
    };
    public string Descricao => Tipo switch
    {
        TipoTabelaTributaria.ReducaoMensalIrrf => "Configure a redução aplicada ao imposto após a tabela progressiva.",
        TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca => "Variação mensal do índice, usada na correção monetária da pensão em atraso.",
        TipoTabelaTributaria.SeguroDesemprego => "Faixas da média salarial: percentual sobre o excedente e valor fixo; a última é o valor máximo. Reajustada todo ano em janeiro.",
        TipoTabelaTributaria.TaxaLegal => "Taxa de cada mês publicada pelo Banco Central (Selic menos IPCA-15), para os juros de mora desde 30/08/2024.",
        TipoTabelaTributaria.Selic => "Selic acumulada em cada mês, do Banco Central, para os débitos judiciais até 29/08/2024 (correção e juros juntos).",
        TipoTabelaTributaria.IpcaE => "Variação mensal do IPCA-15 (IBGE), usada como IPCA-E na fase pré-judicial dos débitos trabalhistas.",
        TipoTabelaTributaria.Tr => "TR do primeiro dia de cada mês, do Banco Central, para os juros da fase pré-judicial dos débitos trabalhistas.",
        _ => ExibeFaixa ? "Cadastre, consulte e mantenha as faixas por competência." : "Cadastre, consulte e mantenha os valores por competência."
    };
    public string TituloLista => ExibeFaixa ? "Faixas cadastradas" : "Valores cadastrados";
    public string LabelValor => Tipo switch
    {
        TipoTabelaTributaria.ReducaoMensalIrrf => "Limite de rendimentos (R$)",
        TipoTabelaTributaria.Inss or TipoTabelaTributaria.Irrf => "Limite da base (R$)",
        TipoTabelaTributaria.Plr => "Limite da PLR anual (R$)",
        TipoTabelaTributaria.SalarioFamilia => "Limite de remuneração (R$)",
        TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca => "Variação no mês (%)",
        TipoTabelaTributaria.TaxaLegal or TipoTabelaTributaria.Selic or TipoTabelaTributaria.Tr => "Taxa no mês (%)",
        TipoTabelaTributaria.IpcaE => "Variação no mês (%)",
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
    public bool ExibeFaixa => Tipo is TipoTabelaTributaria.Inss or TipoTabelaTributaria.Irrf or TipoTabelaTributaria.ReducaoMensalIrrf or TipoTabelaTributaria.Plr or TipoTabelaTributaria.SalarioFamilia or TipoTabelaTributaria.SeguroDesemprego;
    // O salário-família tem faixas, mas cada uma é um limite de remuneração com uma cota, sem alíquota.
    public bool ExibeAliquota => Tipo is TipoTabelaTributaria.Inss or TipoTabelaTributaria.Irrf or TipoTabelaTributaria.ReducaoMensalIrrf or TipoTabelaTributaria.Plr or TipoTabelaTributaria.SeguroDesemprego;
    public bool ExibeDeducao => Tipo is TipoTabelaTributaria.Irrf or TipoTabelaTributaria.ReducaoMensalIrrf or TipoTabelaTributaria.Plr or TipoTabelaTributaria.SalarioFamilia or TipoTabelaTributaria.SeguroDesemprego;
    public string TextoLinkFonteOficial => _atualizador.NomeFonteOficial(Tipo) switch
    {
        "Receita Federal" => "Abrir página oficial da Receita Federal ↗",
        var nome => $"Abrir página oficial do {nome} ↗"
    };

    /// <summary>Índices mensais: a variação da inflação pode ser negativa, e a atualização importa meses, não faixas.</summary>
    public bool EhIndice => Tipo is TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca or TipoTabelaTributaria.TaxaLegal or TipoTabelaTributaria.Selic or TipoTabelaTributaria.IpcaE or TipoTabelaTributaria.Tr;
    public string FonteOficial => _atualizador.FonteOficial(Tipo).AbsoluteUri;
    public ObservableCollection<RegistroTabelaDto> Registros { get; } = [];
    public RegistroTabelaDto? Selecionado
    {
        get => _selecionado;
        set
        {
            if (SetProperty(ref _selecionado, value) && value is not null) Preencher(value);
            ((AsyncRelayCommand)ExcluirCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(TextoSalvar));
        }
    }
    // Sem linha selecionada, salvar cria um registro; o rótulo deixa claro qual das duas operações será feita.
    public string TextoSalvar => Selecionado is null ? "Incluir registro" : "Salvar alteração";
    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public string Faixa { get => _faixa; set => SetProperty(ref _faixa, value); }
    public string Valor { get => _valor; set => SetProperty(ref _valor, value); }
    public string Aliquota { get => _aliquota; set => SetProperty(ref _aliquota, value); }
    public string Deducao { get => _deducao; set => SetProperty(ref _deducao, value); }
    public string StatusAtualizacao { get => _statusAtualizacao; private set => SetProperty(ref _statusAtualizacao, value); }
    public ICommand SalvarCommand { get; }
    public ICommand ExcluirCommand { get; }
    public ICommand CarregarCommand { get; }
    public ICommand NovoCommand { get; }
    public ICommand AtualizarPelaInternetCommand { get; }
    public ICommand AbrirFonteOficialCommand { get; }

    /// <summary>Recarrega a lista mantendo selecionado o registro informado; se ele não existir mais, volta ao modo de inclusão.</summary>
    private async Task CarregarAsync(int? idSelecionado)
    {
        var registros = await _service.ListarAsync(Tipo, CancellationToken.None);
        Registros.Clear();
        foreach (var item in registros) Registros.Add(item);
        // Os registros são records (igualdade por valor): limpar antes garante que o formulário seja preenchido de novo com o que está no banco.
        Selecionado = null;
        Selecionado = Registros.FirstOrDefault(item => item.Id == idSelecionado);
        if (Selecionado is null) LimparFormulario();
    }
    private void NovoRegistro()
    {
        Selecionado = null;
        LimparFormulario();
    }
    private void LimparFormulario()
    {
        Competencia = DateTime.Today.ToString("MM/yyyy", Cultura); Faixa = "1"; Valor = "0,00"; Aliquota = "0,00"; Deducao = "0,00";
    }
    private async Task AtualizarPelaInternetAsync()
    {
        try
        {
            StatusAtualizacao = "Consultando a fonte oficial e as fontes alternativas...";
            var atualizacao = await _atualizador.AtualizarAsync(Tipo, CancellationToken.None);
            if (atualizacao.Falhou)
            {
                StatusAtualizacao = "A atualização não foi concluída; os dados locais foram preservados.";
                _notificador.MostrarFalha(atualizacao.Erro, "Não foi possível atualizar a tabela pela internet.");
                return;
            }
            await CarregarAsync(Selecionado?.Id);
            StatusAtualizacao = EhIndice ? DescreverAtualizacaoIndice(atualizacao.Valor) : DescreverAtualizacao(atualizacao.Valor);
        }
        catch (Exception ex)
        {
            StatusAtualizacao = "A atualização não foi concluída; os dados locais foram preservados.";
            _notificador.MostrarErro("Não foi possível atualizar a tabela pela internet.", ex);
        }
    }
    private static string DescreverAtualizacao(AtualizacaoTabelaResultado resultado)
    {
        var origem = resultado.Oficial ? "pela fonte oficial" : $"por {string.Join(" e ", resultado.Fontes)}, que já publicaram a tabela";
        var texto = resultado.QuantidadeFaixas switch
        {
            // O desconto mínimo é só conferido: quando o banco já tem o valor da lei, nada é gravado.
            0 => $"Dados de {resultado.Competencia:MM/yyyy} conferidos {origem}: os valores cadastrados já estavam corretos.",
            1 => $"Dados de {resultado.Competencia:MM/yyyy} atualizados {origem} (1 valor importado).",
            var faixas => $"Dados de {resultado.Competencia:MM/yyyy} atualizados {origem} ({faixas} faixas tributárias importadas)."
        };
        return resultado.Observacoes.Count == 0 ? texto : $"{texto} {string.Join(" ", resultado.Observacoes)}";
    }
    private static string DescreverAtualizacaoIndice(AtualizacaoTabelaResultado resultado)
    {
        var meses = resultado.QuantidadeFaixas == 1 ? "1 mês incluído ou corrigido" : $"{resultado.QuantidadeFaixas} meses incluídos ou corrigidos";
        var texto = $"Índices até {resultado.Competencia:MM/yyyy} conferidos com {string.Join(" e ", resultado.Fontes)} ({meses}).";
        return resultado.Observacoes.Count == 0 ? texto : $"{texto} {string.Join(" ", resultado.Observacoes)}";
    }
    private void AbrirFonteOficial()
    {
        Process.Start(new ProcessStartInfo(FonteOficial) { UseShellExecute = true });
    }
    private async Task SalvarAsync()
    {
        if (!Ler(out var request)) return;
        try
        {
            var salvo = await _service.SalvarAsync(Tipo, request, CancellationToken.None);
            if (salvo.Falhou)
            {
                _notificador.MostrarFalha(salvo.Erro, "Não foi possível salvar o registro.");
                return;
            }
            await CarregarAsync(request.Id == 0 ? null : request.Id);
            // Após incluir, o registro criado (o de maior Id com a mesma competência e faixa) fica selecionado.
            if (request.Id == 0)
                Selecionado = Registros.Where(item => item.Competencia == request.Competencia && item.Faixa == request.Faixa).MaxBy(item => item.Id);
        }
        catch (Exception ex) { _notificador.MostrarErro("Não foi possível salvar o registro.", ex); }
    }
    private async Task ExcluirAsync()
    {
        if (Selecionado is null) return;
        try
        {
            var excluido = await _service.ExcluirAsync(Tipo, Selecionado.Id, CancellationToken.None);
            if (excluido.Falhou)
                _notificador.MostrarFalha(excluido.Erro, "Não foi possível excluir o registro.");
            await CarregarAsync(null);
        }
        catch (Exception ex) { _notificador.MostrarErro("Não foi possível excluir o registro.", ex); }
    }
    private bool Ler(out SalvarRegistroTabelaRequest request)
    {
        request = default!;
        var competencia = default(DateTime);
        var valor = 0m;
        var valido = DateTime.TryParseExact(Competencia, "MM/yyyy", Cultura, DateTimeStyles.None, out competencia) && LeituraNumerica.TentarLer(Valor, out valor)
            && (valor >= 0m || Tipo is TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca);
        var f = 0; var a = 0m; var d = 0m;
        int? faixa = null; decimal? aliquota = null; decimal? deducao = null;
        if (ExibeFaixa) { valido &= int.TryParse(Faixa, out f) && f > 0; faixa = f; }
        if (ExibeAliquota) { valido &= LeituraNumerica.TentarLer(Aliquota, out a) && a >= 0m; aliquota = a; }
        if (ExibeDeducao) { valido &= LeituraNumerica.TentarLer(Deducao, out d) && d >= 0m; deducao = d; }
        if (!valido) { _notificador.MostrarAviso("Informe valores válidos para competência e campos numéricos."); return false; }
        request = new SalvarRegistroTabelaRequest(Selecionado?.Id ?? 0, DateOnly.FromDateTime(competencia), faixa, valor, aliquota, deducao); return true;
    }
    private void Preencher(RegistroTabelaDto item)
    {
        Competencia = item.Competencia.ToString("MM/yyyy", Cultura); Faixa = item.Faixa?.ToString() ?? "1"; Valor = item.Valor.ToString(FormatoValor, Cultura); Aliquota = item.Aliquota?.ToString(FormatoAliquota, Cultura) ?? "0,00"; Deducao = item.Deducao?.ToString("N2", Cultura) ?? "0,00";
    }
}
