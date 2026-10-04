using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Pagamento ao fim de cada período de convocação do trabalho intermitente: a remuneração, o DSR, as férias proporcionais
/// com 1/3 e o 13º proporcional, todos de uma vez (CLT, art. 452-A, § 6º), e o FGTS depositado sobre eles (§ 8º).
/// </summary>
public sealed class SimularIntermitenteUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularIntermitenteRequest>
{
    private const decimal HorasMensais = 220m;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularIntermitenteRequest r, CancellationToken cancellationToken)
    {
        if (r.ValorHora <= 0m || r.Horas <= 0m)
            return Erro.Validacao("Informe o valor da hora e as horas trabalhadas.");
        if (r.DiasTrabalhados < 1 || r.Descansos < 0)
            return Erro.Validacao("Informe os dias trabalhados; os descansos não podem ser negativos.");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var salarioMinimo = consultaTabelas.Valor.SalarioMinimo;

        var remuneracao = Arredondar(r.ValorHora * r.Horas);
        var dsr = Arredondar(remuneracao / r.DiasTrabalhados * r.Descansos);
        var baseMensal = remuneracao + dsr;
        var ferias = Arredondar(baseMensal / 12m);
        var terco = Arredondar(ferias / 3m);
        var decimoTerceiro = Arredondar(baseMensal / 12m);
        var fgts = Arredondar((baseMensal + decimoTerceiro) * .08m);

        var proventos = new List<VerbaDto> { new("Remuneração das horas", $"{Formato.Numero(r.Horas)} h", remuneracao) };
        if (dsr > 0m) proventos.Add(new("DSR", $"{r.Descansos} dia(s)", dsr));
        proventos.Add(new("Férias proporcionais", "1/12", ferias));
        proventos.Add(new("1/3 sobre as férias", "", terco));
        proventos.Add(new("13º salário proporcional", "1/12", decimoTerceiro));

        var observacoes = new List<string>
        {
            "No trabalho intermitente, ao fim de cada período de prestação de serviço o empregado recebe na hora a remuneração, as férias proporcionais com 1/3, o 13º proporcional, o DSR e os adicionais (CLT, art. 452-A, § 6º); o recibo deve discriminar cada parcela.",
            "O empregador recolhe o FGTS e a contribuição previdenciária sobre os valores pagos no mês (§ 8º). A cada 12 meses, o empregado tem direito a um mês de férias, sem convocação, já pagas a cada período.",
            "O INSS e o IRRF dependem de como cada verba é lançada na folha e não foram calculados aqui: some os valores do mês e use a Simulação tributária ou confira com a contabilidade. Para outros adicionais, como horas extras e noturno, use a calculadora Horas extras e adicionais."
        };
        if (salarioMinimo is { } minimo && r.ValorHora < Arredondar(minimo / HorasMensais))
            observacoes.Insert(0, $"O valor da hora é menor que o do salário mínimo ({Formato.Moeda(Arredondar(minimo / HorasMensais))} por hora): não é permitido (art. 452-A).");

        return new DemonstrativoDto(
            "Trabalho intermitente",
            $"Competência {Formato.Competencia(r.Competencia)} • {Formato.Numero(r.Horas)} horas em {Formato.Dias(r.DiasTrabalhados)}",
            [
                new("Total do período", Formato.Moeda(proventos.Sum(verba => verba.Valor)), "Pago ao fim da convocação"),
                new("Remuneração e DSR", Formato.Moeda(baseMensal), $"{Formato.Moeda(r.ValorHora)} por hora"),
                new("Férias + 1/3 e 13º", Formato.Moeda(ferias + terco + decimoTerceiro), "Proporcionais ao período"),
                new("FGTS", Formato.Moeda(fgts), "8%, depositado pelo empregador")
            ],
            proventos,
            [],
            [new("FGTS do período", "8%", fgts)],
            [new GrupoMemoriaDto("Pagamento do período", $"Total: {Formato.Moeda(proventos.Sum(verba => verba.Valor))}", [
                new("Remuneração", $"{Formato.Moeda(r.ValorHora)} x {Formato.Numero(r.Horas)} h = {Formato.Moeda(remuneracao)}"),
                new("DSR", $"{Formato.Moeda(remuneracao)} ÷ {Formato.Dias(r.DiasTrabalhados)} trabalhados x {r.Descansos} descanso(s) = {Formato.Moeda(dsr)}"),
                new("Férias proporcionais", $"{Formato.Moeda(baseMensal)} ÷ 12 = {Formato.Moeda(ferias)}, mais 1/3 = {Formato.Moeda(terco)}"),
                new("13º proporcional", $"{Formato.Moeda(baseMensal)} ÷ 12 = {Formato.Moeda(decimoTerceiro)}"),
                new("FGTS", $"({Formato.Moeda(baseMensal)} + {Formato.Moeda(decimoTerceiro)}) x 8% = {Formato.Moeda(fgts)}")])],
            observacoes,
            RotuloResultado: "Total do período (bruto)");
    }

    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
}
