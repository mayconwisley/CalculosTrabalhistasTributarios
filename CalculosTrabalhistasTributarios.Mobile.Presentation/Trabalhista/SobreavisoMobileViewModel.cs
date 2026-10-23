using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class SobreavisoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularSobreavisoRequest> _simulador;

    public override string Titulo => "Sobreaviso e prontidão";
    public override string Descricao => "Calcule as horas de sobreaviso (1/3) e de prontidão (2/3) por competência, com DSR, 13º, férias, aviso e FGTS.";

    public SobreavisoMobileViewModel(ISimularDemonstrativoUseCase<SimularSobreavisoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<SobreavisoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("divisor", "Divisor do salário-hora", TipoCampoMobile.Inteiro, "220",
            "Salário mensal ÷ divisor = salário-hora: 220 para 44 h semanais, 200 para 40, 180 para 36.");
        Adicionar("habitualidade", "Pagamento das horas", TipoCampoMobile.Opcao,
            dica: "Horas habituais integram a remuneração e refletem no DSR, no 13º, nas férias e no aviso; as eventuais têm só o FGTS do mês.",
            opcoes: ["Habitual (com reflexos)", "Eventual (só o FGTS)"]);
        Adicionar("situacao", "Situação do contrato", TipoCampoMobile.Opcao,
            dica: "Na dispensa sem justa causa entram o reflexo no aviso prévio indenizado e a multa de 40% do FGTS; férias não gozadas são indenizadas, sem FGTS (OJ 195 do TST).",
            opcoes: ["Contrato ativo", "Dispensa sem justa causa"]);
        Adicionar("admissao", "Admissão (dd/MM/yyyy, opcional)", TipoCampoMobile.Data,
            dica: "Separa as férias por período aquisitivo. Sem ela, todos os meses formam um só período.");
        Adicionar("dsr", "DSR sobre as horas?", TipoCampoMobile.SimNao,
            dica: "Repouso sobre as horas habituais: valor do mês ÷ dias úteis × domingos e feriados (Lei 605/1949, art. 7º).");
        Adicionar("dsrNosReflexos", "DSR entra nos reflexos?", TipoCampoMobile.SimNao,
            dica: "Premissa: se o DSR das horas entra na base do 13º, das férias e do aviso. O marco de 20/03/2023 do TST (Tema 9) trata das horas extras.");
        Adicionar("diasAviso", "Dias de aviso prévio", TipoCampoMobile.Inteiro, "30",
            "De 30 a 90 dias: 30 mais 3 por ano completo de serviço (Lei 12.506/2011).");
        Secao("gerarSecao", "Gerar meses");
        Adicionar("inicio", "Início (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura));
        Adicionar("fim", "Fim (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura));
        Adicionar("salarioMensal", "Salário por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor sugerido nos meses novos; sem o adicional de periculosidade (Súmula 132, II, do TST).");
        Adicionar("sobreavisoMensal", "Sobreaviso por mês (h)", TipoCampoMobile.Horas, "0:00",
            "Horas sugeridas nos meses novos, como 40:00 ou 40,5.");
        Adicionar("prontidaoMensal", "Prontidão por mês (h)", TipoCampoMobile.Horas, "0:00",
            "Horas sugeridas nos meses novos, como 12:00 ou 12,5.");
        var grade = AdicionarGrade("meses", "Competências de sobreaviso",
            "Salário, horas de sobreaviso e de prontidão e feriados de cada mês. Gerar meses preserva os valores já digitados.",
            "Mês", MotorReflexos.MaximoDeMeses,
            [
                new("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia),
                new("salario", "Salário (R$)", TipoCampoMobile.Moeda, "0,00", "Salário normal do mês, base do salário-hora."),
                new("sobreaviso", "Sobreaviso (h)", TipoCampoMobile.Horas, "0:00", "Horas aguardando chamado fora da empresa, sem as horas trabalhadas."),
                new("prontidao", "Prontidão (h)", TipoCampoMobile.Horas, "0:00", "Horas aguardando ordens nas dependências da empresa."),
                new("feriados", "Feriados em dias úteis", TipoCampoMobile.Inteiro, "0", "Para o DSR do mês.")
            ]);
        grade.GerarCommand = new RelayCommand(GerarMeses);
        Campo("dsr").Marcado = true;
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "habitualidade" or "situacao" or "dsr") Atualizar();
    }

    private void Atualizar()
    {
        var habitual = Campo("habitualidade").Indice == 0;
        Campo("dsr").Visivel = Campo("admissao").Visivel = habitual;
        Campo("dsrNosReflexos").Visivel = habitual && Sim("dsr");
        Campo("diasAviso").Visivel = habitual && Campo("situacao").Indice == 1;
    }

    private void GerarMeses()
    {
        var grade = Grade("meses");
        try
        {
            var inicio = Competencia("inicio");
            var fim = Competencia("fim");
            var salario = Numero("salarioMensal");
            var sobreaviso = HorasNaoNegativas(Campo("sobreavisoMensal"));
            var prontidao = HorasNaoNegativas(Campo("prontidaoMensal"));
            if (inicio > fim)
                throw new EntradaInvalidaException("Fim (MM/yyyy): informe uma competência igual ou posterior ao início.");
            if (salario < 0m)
                throw new EntradaInvalidaException("Salário por mês (R$): informe um valor positivo ou zero, como 3.300,00.");
            var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
            if (quantidade > grade.Limite)
                throw new EntradaInvalidaException($"O período tem {quantidade} meses; a simulação aceita até {grade.Limite}.");
            var anteriores = grade.Linhas.ToHashSet();
            grade.Gerar([.. Enumerable.Range(0, quantidade).Select(indice => inicio.AddMonths(indice).ToString("MM/yyyy", Cultura))], "competencia");
            var aviso = grade.Aviso;
            foreach (var linha in grade.Linhas.Where(linha => !anteriores.Contains(linha)))
            {
                linha.Campo("salario").Valor = salario.ToString("N2", Cultura);
                linha.Campo("sobreaviso").Valor = FormatarHoras(sobreaviso);
                linha.Campo("prontidao").Valor = FormatarHoras(prontidao);
            }
            grade.Aviso = aviso;
        }
        catch (EntradaInvalidaException erro) { grade.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var grade = Grade("meses");
        if (grade.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere os meses ou adicione as competências de sobreaviso e prontidão.");
        var meses = grade.Ler(linha => new MesSobreaviso(
            LeituraCampoMobile.Competencia(linha.Campo("competencia")),
            LeituraCampoMobile.Numero(linha.Campo("salario")),
            HorasNaoNegativas(linha.Campo("sobreaviso")),
            HorasNaoNegativas(linha.Campo("prontidao")),
            FeriadosNaoNegativos(linha.Campo("feriados"))));
        if (meses.GroupBy(mes => mes.Competencia).FirstOrDefault(grupo => grupo.Count() > 1) is { } repetida)
            throw new EntradaInvalidaException($"A competência {repetida.Key.ToString("MM/yyyy", Cultura)} aparece mais de uma vez; some os valores do mês na mesma linha.");
        var habitual = Opcao("habitualidade") == 0;
        var dispensa = Opcao("situacao") == 1;
        var dsr = habitual && Sim("dsr");
        var incidencias = IncidenciasReflexo.Fgts | (dispensa ? IncidenciasReflexo.MultaFgts : IncidenciasReflexo.Nenhuma);
        if (habitual)
            incidencias |= IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias
                | (dsr ? IncidenciasReflexo.Dsr : IncidenciasReflexo.Nenhuma)
                | (dispensa ? IncidenciasReflexo.AvisoPrevio : IncidenciasReflexo.Nenhuma);
        return _simulador.ExecutarAsync(new SimularSobreavisoRequest(new EntradaSobreaviso(
            meses, Inteiro("divisor"), habitual, incidencias,
            dsr && Sim("dsrNosReflexos"),
            habitual && dispensa ? Inteiro("diasAviso") : 0,
            FeriasGozadas: !dispensa, Admissao: habitual ? DataOpcional("admissao") : null)), cancellationToken);
    }

    private static decimal HorasNaoNegativas(CampoMobile campo)
    {
        var horas = LeituraCampoMobile.Horas(campo);
        return horas >= 0m ? horas : throw new EntradaInvalidaException($"{campo.Rotulo}: informe horas positivas ou zero, como 40:00.");
    }

    private static int FeriadosNaoNegativos(CampoMobile campo)
    {
        var feriados = LeituraCampoMobile.Inteiro(campo);
        return feriados >= 0 ? feriados : throw new EntradaInvalidaException($"{campo.Rotulo}: informe um número inteiro positivo ou zero, como 1.");
    }

    private static string FormatarHoras(decimal horas)
    {
        var minutos = (long)Math.Round(horas * 60m, MidpointRounding.AwayFromZero);
        return $"{minutos / 60}:{minutos % 60:00}";
    }
}
