using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSimplesNacional : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularSimplesNacionalRequest> _simulador;
    private readonly CampoTextoViewModel _periodo = Competencia("Período de apuração", "Mês do DAS (MM/AAAA). Define a tabela e os meses que formam a receita e a folha de 12 meses.");
    private readonly CampoOpcaoViewModel _atividade = new("Atividade",
        [
            new("Serviços sujeitos ao fator r (Anexo III ou V)", AtividadeSimples.ServicosFatorR),
            new("Serviços do Anexo III", AtividadeSimples.ServicosAnexoIII),
            new("Serviços do Anexo IV", AtividadeSimples.ServicosAnexoIV),
            new("Comércio (Anexo I)", AtividadeSimples.Comercio),
            new("Indústria (Anexo II)", AtividadeSimples.Industria)
        ],
        "O anexo depende da atividade do CNAE e do serviço prestado (Resolução CGSN 140/2018, art. 25). Confira o enquadramento da empresa.") { Largura = 330 };
    private readonly CampoTextoViewModel _inicio = new("Início das atividades (opcional)", TipoCampo.Competencia, string.Empty,
        "Mês de abertura, só se a empresa tem menos de 14 meses de atividade (MM/AAAA). Deixe vazio nos demais casos.", permiteVazio: true);
    private readonly CampoTextoViewModel _receitaMes = Moeda("Receita bruta do mês", "Receita do período de apuração, sobre a qual incide a alíquota efetiva.");
    private readonly CampoTextoViewModel _folhaMes = Moeda("Folha do mês com encargos", "Usada só no 1º mês de atividade até 2026, para o fator r do próprio mês.");
    private readonly CampoMesesSimplesViewModel _meses;
    private readonly CampoTextoViewModel _proLabore = Moeda("Pró-labore mensal atual", "Pró-labore do sócio hoje, para estimar o INSS e o IRRF de um aumento que leve o fator r a 28%.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes do sócio", 0, "Dependentes do IRRF sobre o pró-labore.");
    private readonly CampoTextoViewModel _remuneracoes = Moeda("Salários e pró-labore do mês", "Base da CPP de 20% recolhida fora do DAS no Anexo IV.");

    public CalculadoraSimplesNacional(ISimularDemonstrativoUseCase<SimularSimplesNacionalRequest> simulador)
    {
        _simulador = simulador;
        _meses = new CampoMesesSimplesViewModel(_periodo, _inicio);
        _atividade.AoAlterar = () =>
        {
            var atividade = _atividade.Valor<AtividadeSimples>();
            _proLabore.Visivel = _dependentes.Visivel = atividade == AtividadeSimples.ServicosFatorR;
            _remuneracoes.Visivel = atividade == AtividadeSimples.ServicosAnexoIV;
        };
        _atividade.AoAlterar();
    }

    public override string Titulo => "Simples Nacional e fator r";
    public override string Descricao => "Calcule o DAS pela receita e pela folha de 12 meses, o fator r e o anexo, e o efeito do pró-labore no custo do PJ.";
    public override string InstrucaoInicial => "Informe o período, a atividade e a receita do mês, gere os meses anteriores, preencha receita e folha e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_periodo, _atividade, _inicio, _receitaMes, _folhaMes, _meses, _proLabore, _dependentes, _remuneracoes];
    public override string NomeArquivoPdf => $"simples-nacional-{_periodo.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _periodo;

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_meses.Rotulo] = _meses.Exportar();
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        base.ImportarCampos(valores);
        if (valores.TryGetValue(_meses.Rotulo, out var json))
            _meses.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var meses = _meses.Ler();
        if (meses.Falhou)
            return meses.Erro;
        var atividade = _atividade.Valor<AtividadeSimples>();
        return await LerECalcularAsync(_simulador, leitor => new SimularSimplesNacionalRequest(
            new EntradaSimples(leitor.Competencia(_periodo), atividade, leitor.Moeda(_receitaMes), leitor.Moeda(_folhaMes),
                string.IsNullOrWhiteSpace(_inicio.Valor) ? null : leitor.Competencia(_inicio), meses.Valor,
                atividade == AtividadeSimples.ServicosAnexoIV ? leitor.Moeda(_remuneracoes) : 0m),
            atividade == AtividadeSimples.ServicosFatorR ? leitor.Moeda(_proLabore) : 0m,
            atividade == AtividadeSimples.ServicosFatorR ? leitor.Inteiro(_dependentes) : 0), cancellationToken);
    }
}
