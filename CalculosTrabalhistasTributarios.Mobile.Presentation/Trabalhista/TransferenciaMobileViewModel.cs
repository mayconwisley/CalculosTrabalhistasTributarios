using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class TransferenciaMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularTransferenciaRequest> _simulador;

    public override string Titulo => "Adicional de transferência";
    public override string Descricao => "Calcule o adicional de no mínimo 25% da transferência provisória por competência, com 13º, férias, aviso e FGTS.";

    public TransferenciaMobileViewModel(ISimularDemonstrativoUseCase<SimularTransferenciaRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<TransferenciaMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("percentual", "Percentual do adicional (%)", TipoCampoMobile.Numero, "25",
            "No mínimo 25% do salário da origem (CLT, art. 469, § 3º); informe mais se a norma coletiva ou o contrato previr.");
        Adicionar("situacao", "Situação do contrato", TipoCampoMobile.Opcao,
            dica: "Na dispensa durante a transferência entram o reflexo no aviso prévio indenizado e a multa de 40% do FGTS; férias não gozadas são indenizadas, sem FGTS (OJ 195 do TST).",
            opcoes: ["Contrato ativo", "Dispensa sem justa causa"]);
        Adicionar("admissao", "Admissão (dd/MM/yyyy, opcional)", TipoCampoMobile.Data,
            dica: "Separa as férias por período aquisitivo. Sem ela, todos os meses formam um só período.");
        Adicionar("diasAviso", "Dias de aviso prévio", TipoCampoMobile.Inteiro, "30",
            "De 30 a 90 dias: 30 mais 3 por ano completo de serviço (Lei 12.506/2011).");
        Adicionar("despesas", "Despesas da mudança (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Transporte e frete pagos pelo empregador (CLT, art. 470): reembolso, sem FGTS nem reflexos. 0,00 se não houver.");
        Secao("gerarSecao", "Gerar meses");
        Adicionar("inicio", "Início (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura));
        Adicionar("fim", "Fim (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura));
        Adicionar("salarioMensal", "Salário por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Salário da origem sugerido nos meses novos; ajuste cada mês na grade.");
        var grade = AdicionarGrade("meses", "Competências da transferência",
            "Salário da origem e dias transferido em cada mês. Gerar meses preserva os valores já digitados.",
            "Mês", MotorReflexos.MaximoDeMeses,
            [
                new("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia),
                new("salario", "Salário da origem (R$)", TipoCampoMobile.Moeda, "0,00", "Base do adicional (CLT, art. 469, § 3º)."),
                new("dias", "Dias transferido", TipoCampoMobile.Inteiro, "30", "De 0 a 30; 30 é o mês inteiro, inclusive em meses de 31 dias.")
            ]);
        grade.GerarCommand = new RelayCommand(GerarMeses);
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "situacao") Atualizar();
    }

    private void Atualizar() => Campo("diasAviso").Visivel = Campo("situacao").Indice == 1;

    private void GerarMeses()
    {
        var grade = Grade("meses");
        try
        {
            var inicio = Competencia("inicio");
            var fim = Competencia("fim");
            var salario = Numero("salarioMensal");
            if (inicio > fim)
                throw new EntradaInvalidaException("Fim (MM/yyyy): informe uma competência igual ou posterior ao início.");
            if (salario < 0m)
                throw new EntradaInvalidaException("Salário por mês (R$): informe um valor positivo ou zero, como 4.000,00.");
            var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
            if (quantidade > grade.Limite)
                throw new EntradaInvalidaException($"O período tem {quantidade} meses; a simulação aceita até {grade.Limite}.");
            var anteriores = grade.Linhas.ToHashSet();
            grade.Gerar([.. Enumerable.Range(0, quantidade).Select(indice => inicio.AddMonths(indice).ToString("MM/yyyy", Cultura))], "competencia");
            var aviso = grade.Aviso;
            foreach (var linha in grade.Linhas.Where(linha => !anteriores.Contains(linha)))
                linha.Campo("salario").Valor = salario.ToString("N2", Cultura);
            grade.Aviso = aviso;
        }
        catch (EntradaInvalidaException erro) { grade.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var grade = Grade("meses");
        if (grade.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere os meses ou adicione as competências da transferência.");
        var meses = grade.Ler(linha => new MesTransferencia(
            LeituraCampoMobile.Competencia(linha.Campo("competencia")),
            LeituraCampoMobile.Numero(linha.Campo("salario")),
            LeituraCampoMobile.Inteiro(linha.Campo("dias"))));
        if (meses.GroupBy(mes => mes.Competencia).FirstOrDefault(grupo => grupo.Count() > 1) is { } repetida)
            throw new EntradaInvalidaException($"A competência {repetida.Key.ToString("MM/yyyy", Cultura)} aparece mais de uma vez; some os valores do mês na mesma linha.");
        var dispensa = Opcao("situacao") == 1;
        var incidencias = IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts
            | (dispensa ? IncidenciasReflexo.AvisoPrevio | IncidenciasReflexo.MultaFgts : IncidenciasReflexo.Nenhuma);
        return _simulador.ExecutarAsync(new SimularTransferenciaRequest(new EntradaTransferencia(
            meses, Numero("percentual"), incidencias, Numero("despesas"),
            dispensa ? Inteiro("diasAviso") : 0, FeriasGozadas: !dispensa, Admissao: DataOpcional("admissao"))), cancellationToken);
    }
}
