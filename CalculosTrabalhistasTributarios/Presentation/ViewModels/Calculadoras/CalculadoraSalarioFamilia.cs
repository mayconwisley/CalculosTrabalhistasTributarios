using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSalarioFamilia(ISimularDemonstrativoUseCase<SimularSalarioFamiliaRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia(dica: "Mês do pagamento, que define o limite e a cota do salário-família (MM/AAAA).");
    private readonly CampoTextoViewModel _remuneracao = Moeda("Remuneração do mês cheio", "Salário de contribuição do mês inteiro, com horas extras e adicionais, sem o 13º e o 1/3 de férias. Na admissão e no desligamento, informe a remuneração do mês completo, não a proporcional.");
    private readonly CampoTextoViewModel _filhos = Inteiro("Filhos com direito", 1, "Filhos ou equiparados de até 14 anos, ou inválidos de qualquer idade.");
    private readonly CampoTextoViewModel _dias = Inteiro("Dias trabalhados", 30, "30 no mês completo; nos meses de admissão e desligamento, os dias trabalhados.");

    public override string Titulo => "Salário-família";
    public override string Descricao => "Verifique o direito e calcule o salário-família pela remuneração e pela quantidade de filhos.";
    public override string InstrucaoInicial => "Informe a competência, a remuneração do mês e os filhos com direito e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _remuneracao, _filhos, _dias];
    public override string NomeArquivoPdf => $"salario-familia-{_competencia.Valor.Replace('/', '-')}.pdf";

    // A remuneração comparada ao limite inclui horas extras e adicionais: o salário-base das outras calculadoras não serve.
    protected override CampoTextoViewModel CampoCompetencia => _competencia;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularSalarioFamiliaRequest(leitor.Competencia(_competencia), leitor.Moeda(_remuneracao), leitor.Inteiro(_filhos), leitor.Inteiro(_dias)), cancellationToken);
}
