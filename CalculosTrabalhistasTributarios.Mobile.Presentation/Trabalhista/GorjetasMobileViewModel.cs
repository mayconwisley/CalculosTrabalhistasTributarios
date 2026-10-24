using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class GorjetasMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularGorjetasRequest> _simulador;

    public override string Titulo => "Gorjetas";
    public override string Descricao => "Calcule as gorjetas repassadas por competência, a retenção da empresa, a média de 12 meses e os reflexos no 13º, nas férias e no FGTS.";

    public GorjetasMobileViewModel(ISimularDemonstrativoUseCase<SimularGorjetasRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<GorjetasMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("regime", "Regime da empresa", TipoCampoMobile.Opcao,
            dica: "Limite da retenção: até 20% da arrecadação no Simples Nacional e até 33% nos demais regimes (CLT, art. 457, § 6º).",
            opcoes: ["Simples Nacional (até 20%)", "Demais regimes (até 33%)"]);
        Adicionar("retencao", "Retenção da empresa (%)", TipoCampoMobile.Numero, "0",
            "Só com previsão em convenção ou acordo coletivo; 0 sem retenção.");
        Adicionar("situacao", "Situação do contrato", TipoCampoMobile.Opcao,
            dica: "Na dispensa sem justa causa entra a multa de 40% do FGTS, e as férias não gozadas são indenizadas, sem FGTS. Gorjetas não entram no aviso prévio (Súmula 354 do TST).",
            opcoes: ["Contrato ativo", "Dispensa sem justa causa"]);
        Adicionar("admissao", "Admissão (dd/mm/aaaa, opcional)", TipoCampoMobile.Data,
            dica: "Separa as férias por período aquisitivo. Sem ela, todos os meses formam um só período.");
        Secao("gerarSecao", "Gerar meses");
        Adicionar("inicio", "Início (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura));
        Adicionar("fim", "Fim (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura));
        Adicionar("valorMensal", "Gorjetas por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor sugerido nos meses novos; ajuste cada mês na grade.");
        var grade = AdicionarGrade("meses", "Gorjetas por competência",
            "Parte do empregado no rateio das gorjetas de cada mês, antes da retenção. Gerar meses preserva os valores já digitados.",
            "Mês", MotorReflexos.MaximoDeMeses,
            [
                new("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia),
                new("valor", "Gorjetas (R$)", TipoCampoMobile.Moeda, "0,00")
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
            var valor = Numero("valorMensal");
            if (inicio > fim)
                throw new EntradaInvalidaException("Fim (mm/aaaa): informe uma competência igual ou posterior ao início.");
            if (valor < 0m)
                throw new EntradaInvalidaException("Gorjetas por mês (R$): informe um valor positivo ou zero, como 1.500,00.");
            var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
            if (quantidade > grade.Limite)
                throw new EntradaInvalidaException($"O período tem {quantidade} meses; a simulação aceita até {grade.Limite}.");
            var anteriores = grade.Linhas.ToHashSet();
            grade.Gerar([.. Enumerable.Range(0, quantidade).Select(indice => inicio.AddMonths(indice).ToString("MM/yyyy", Cultura))], "competencia");
            var aviso = grade.Aviso;
            foreach (var linha in grade.Linhas.Where(linha => !anteriores.Contains(linha)))
                linha.Campo("valor").Valor = valor.ToString("N2", Cultura);
            grade.Aviso = aviso;
        }
        catch (EntradaInvalidaException erro) { grade.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var grade = Grade("meses");
        if (grade.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere os meses ou adicione as competências de gorjetas.");
        var meses = grade.Ler(linha => new MesGorjeta(
            LeituraCampoMobile.Competencia(linha.Campo("competencia")), LeituraCampoMobile.Numero(linha.Campo("valor"))));
        if (meses.GroupBy(mes => mes.Competencia).FirstOrDefault(grupo => grupo.Count() > 1) is { } repetida)
            throw new EntradaInvalidaException($"A competência {repetida.Key.ToString("MM/yyyy", Cultura)} aparece mais de uma vez; some os valores do mês na mesma linha.");
        var dispensa = Opcao("situacao") == 1;
        var incidencias = IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts
            | (dispensa ? IncidenciasReflexo.MultaFgts : IncidenciasReflexo.Nenhuma);
        return _simulador.ExecutarAsync(new SimularGorjetasRequest(new EntradaGorjetas(meses, Opcao("regime") == 0,
            Numero("retencao"), incidencias, FeriasGozadas: !dispensa, Admissao: DataOpcional("admissao"))), cancellationToken);
    }
}
