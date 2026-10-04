using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraAdicionais : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularAdicionaisRequest> _simulador;
    private readonly CampoTextoViewModel _competencia = Competencia(dica: "Mês do cálculo, que define o salário mínimo e as tabelas de INSS e IRRF (MM/AAAA).");
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário-base, sem gratificações, prêmios ou outros adicionais.");
    private readonly CampoOpcaoViewModel _grau = new("Insalubridade",
        [
            new("Não há", GrauInsalubridade.Nenhum),
            new("Grau mínimo (10%)", GrauInsalubridade.Minimo),
            new("Grau médio (20%)", GrauInsalubridade.Medio),
            new("Grau máximo (40%)", GrauInsalubridade.Maximo)
        ], "Grau definido pelo laudo técnico, conforme a NR-15.");
    private readonly CampoOpcaoViewModel _base = new("Base da insalubridade",
        [
            new("Salário mínimo", BaseInsalubridade.SalarioMinimo),
            new("Salário", BaseInsalubridade.Salario),
            new("Valor informado", BaseInsalubridade.ValorInformado)
        ], "Salário mínimo, salvo previsão diferente em convenção coletiva, como o piso da categoria.");
    private readonly CampoTextoViewModel _valorBase = Moeda("Valor da base", "Piso da categoria ou outra base prevista em convenção coletiva.");
    private readonly CampoOpcaoViewModel _periculosidade = CampoOpcaoViewModel.SimNao("Periculosidade (30%)", false, "Atividade perigosa: inflamáveis, explosivos, energia elétrica, segurança, motocicleta e outras (CLT, art. 193).");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");

    public CalculadoraAdicionais(ISimularDemonstrativoUseCase<SimularAdicionaisRequest> simulador)
    {
        _simulador = simulador;
        _grau.Selecionada = _grau.Opcoes[2];
        _grau.AoAlterar = _base.AoAlterar = AjustarCampos;
        AjustarCampos();
    }

    public override string Titulo => "Insalubridade e periculosidade";
    public override string Descricao => "Calcule os adicionais de insalubridade e de periculosidade, com o salário líquido do mês.";
    public override string InstrucaoInicial => "Informe o salário, o grau de insalubridade ou a periculosidade e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _salario, _grau, _base, _valorBase, _periculosidade, _dependentes];
    public override string NomeArquivoPdf => $"adicionais-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularAdicionaisRequest(
            leitor.Competencia(_competencia), leitor.Moeda(_salario), _grau.Valor<GrauInsalubridade>(), _base.Valor<BaseInsalubridade>(),
            _valorBase.Visivel ? leitor.Moeda(_valorBase) : 0m, _periculosidade.Valor<bool>(), leitor.Inteiro(_dependentes)), cancellationToken);

    // A base só importa quando há insalubridade, e o valor só quando a base é informada.
    private void AjustarCampos()
    {
        var temInsalubridade = _grau.Valor<GrauInsalubridade>() != GrauInsalubridade.Nenhum;
        _base.Visivel = temInsalubridade;
        _valorBase.Visivel = temInsalubridade && _base.Valor<BaseInsalubridade>() == BaseInsalubridade.ValorInformado;
    }
}
