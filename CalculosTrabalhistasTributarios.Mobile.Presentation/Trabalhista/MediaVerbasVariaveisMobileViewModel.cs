using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class MediaVerbasVariaveisMobileViewModel : CalculadoraTrabalhistaViewModel
{
    /// <summary>Mesmo limite do domínio e da grade do desktop.</summary>
    private const int LimiteMeses = 24;
    private readonly ISimularDemonstrativoUseCase<SimularMediaVerbasVariaveisRequest> _simulador;

    public override string Titulo => "Média de verbas variáveis";
    public override string Descricao => "Apure a média mensal de comissões, DSR, horas extras e adicionais por competência.";

    public MediaVerbasVariaveisMobileViewModel(ISimularDemonstrativoUseCase<SimularMediaVerbasVariaveisRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<MediaVerbasVariaveisMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("inicio", "Início (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura));
        Adicionar("fim", "Fim (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura));
        Adicionar("divisor", "Divisor da média (meses)", TipoCampoMobile.Inteiro, "12",
            "De 1 a 24. Na primeira geração recebe a quantidade de meses; meses zerados também contam.");
        var grade = AdicionarGrade("meses", "Valores por competência",
            "Somente parcelas variáveis salariais efetivamente recebidas no período. Gerar meses preserva os valores já digitados.",
            "Mês", LimiteMeses,
            [
                new("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia),
                new("comissoes", "Comissões (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("dsr", "DSR (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("horasExtras", "Horas extras (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("adicionais", "Adicionais (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("outras", "Outras variáveis (R$)", TipoCampoMobile.Moeda, "0,00")
            ]);
        grade.GerarCommand = new RelayCommand(GerarMeses);
    }

    private void GerarMeses()
    {
        var grade = Grade("meses");
        try
        {
            var inicio = Competencia("inicio");
            var fim = Competencia("fim");
            if (inicio > fim)
                throw new EntradaInvalidaException("Fim (mm/aaaa): informe uma competência igual ou posterior ao início.");
            var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
            if (quantidade > grade.Limite)
                throw new EntradaInvalidaException($"O período tem {quantidade} meses; a média aceita até {grade.Limite}.");
            var primeiroPreenchimento = grade.Linhas.Count == 0;
            string[] chaves = [.. Enumerable.Range(0, quantidade).Select(indice => inicio.AddMonths(indice).ToString("MM/yyyy", Cultura))];
            grade.Gerar(chaves, "competencia");
            var geradas = grade.Linhas.Select(linha => linha.Campo("competencia").Valor.Trim()).ToHashSet();
            if (!chaves.All(geradas.Contains))
                return;
            var aviso = grade.Aviso;
            // Como no desktop, o divisor acompanha o período só na primeira geração; depois é decisão de quem simula.
            if (primeiroPreenchimento)
                Campo("divisor").Valor = quantidade.ToString(Cultura);
            grade.Aviso = $"{aviso} Confira o divisor; meses zerados também contam.";
        }
        catch (EntradaInvalidaException erro) { grade.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var grade = Grade("meses");
        if (grade.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere as competências antes de calcular a média.");
        var meses = grade.Ler(linha => new VerbasVariaveisDoMes(
            LeituraCampoMobile.Competencia(linha.Campo("competencia")),
            LeituraCampoMobile.Numero(linha.Campo("comissoes")),
            LeituraCampoMobile.Numero(linha.Campo("dsr")),
            LeituraCampoMobile.Numero(linha.Campo("horasExtras")),
            LeituraCampoMobile.Numero(linha.Campo("adicionais")),
            LeituraCampoMobile.Numero(linha.Campo("outras"))));
        return _simulador.ExecutarAsync(new SimularMediaVerbasVariaveisRequest(meses, Inteiro("divisor")), cancellationToken);
    }
}
