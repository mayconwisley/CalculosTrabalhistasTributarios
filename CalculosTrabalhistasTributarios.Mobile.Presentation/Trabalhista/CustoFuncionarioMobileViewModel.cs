using System.Windows.Input;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class CustoFuncionarioMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularCustoFuncionarioRequest> _simulador;
    private readonly RelayCommand _adicionarBeneficio;

    public override string Titulo => "Custo do funcionário";
    public override string Descricao => "Estime o custo da empresa com encargos, provisões e benefícios.";
    public override bool ExibirBeneficiosDetalhados => true;
    public override bool TemBeneficiosDetalhados => BeneficiosDetalhados.Count > 0;
    public override ICommand AdicionarBeneficioCommand => _adicionarBeneficio;
    public override ICommand RemoverBeneficioCommand { get; }

    public CustoFuncionarioMobileViewModel(ISimularDemonstrativoUseCase<SimularCustoFuncionarioRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<CustoFuncionarioMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("salario", "Salário mensal (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("horas", "Divisor mensal (horas)", TipoCampoMobile.Numero, "220",
            "Maior que zero e até 744 horas. O custo por hora usa a média anualizada.");
        Adicionar("regime", "Regime da empresa", TipoCampoMobile.Opcao,
            dica: "Define a contribuição patronal sobre a folha; o doméstico recolhe 8% + 0,8% + 3,2% no DAE.",
            opcoes: ["Lucro Real ou Presumido", "Simples (anexos I a III e V)", "Simples (anexo IV)", "Empregador doméstico"]);
        Adicionar("rat", "RAT", TipoCampoMobile.Opcao,
            dica: "Risco ambiental do trabalho, conforme a atividade da empresa.",
            opcoes: ["1% (risco leve)", "2% (risco médio)", "3% (risco grave)"]);
        Adicionar("fap", "FAP", TipoCampoMobile.Numero, "1,0000", "Fator acidentário anual da empresa, de 0,5 a 2.");
        Adicionar("terceiros", "Terceiros (%)", TipoCampoMobile.Numero, "5,8",
            "Sistema S, salário-educação e Incra; confirme a alíquota com a contabilidade.");
        Adicionar("aprendiz", "Jovem aprendiz", TipoCampoMobile.SimNao, dica: "FGTS de 2% em vez de 8%.");
        Adicionar("provisoes", "Incluir provisões", TipoCampoMobile.SimNao,
            dica: "Distribui o 13º e as férias com 1/3 mês a mês.").Marcado = true;
        Adicionar("beneficios", "Benefícios agregados (R$)", TipoCampoMobile.Moeda, "0,00",
            "Custo mensal líquido da parte paga pelo empregado. Não duplique benefícios detalhados abaixo.");
        Campo("rat").Indice = 1;
        _adicionarBeneficio = new RelayCommand(AdicionarLinha, () => BeneficiosDetalhados.Count < CalculadoraCustoFuncionario.LimiteBeneficios);
        RemoverBeneficioCommand = new RelayCommand<LinhaBeneficioMobile>(linha =>
        {
            if (linha is not null)
                RemocaoLinhas.Remover(BeneficiosDetalhados, linha, string.IsNullOrWhiteSpace(linha.Descricao) ? "Benefício" : $"Benefício {linha.Descricao.Trim()}",
                    CalculadoraCustoFuncionario.LimiteBeneficios);
        });
        BeneficiosDetalhados.CollectionChanged += (_, args) =>
        {
            if (args.OldItems is not null)
                foreach (LinhaBeneficioMobile linha in args.OldItems) linha.PropertyChanged -= LinhaAlterada;
            if (args.NewItems is not null)
                foreach (LinhaBeneficioMobile linha in args.NewItems) linha.PropertyChanged += LinhaAlterada;
            Demonstrativo = null;
            Mensagem = null;
            _adicionarBeneficio.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(TemBeneficiosDetalhados));
        };
        Atualizar();
    }

    private void AdicionarLinha() => BeneficiosDetalhados.Add(new LinhaBeneficioMobile());
    private void LinhaAlterada(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        Demonstrativo = null;
        Mensagem = null;
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "regime") Atualizar();
    }

    private void Atualizar()
    {
        var regime = (RegimeTributario)Opcao("regime");
        Campo("rat").Visivel = Campo("fap").Visivel = regime is RegimeTributario.LucroRealOuPresumido or RegimeTributario.SimplesNacionalAnexoIV;
        Campo("terceiros").Visivel = regime == RegimeTributario.LucroRealOuPresumido;
        Campo("aprendiz").Visivel = regime != RegimeTributario.EmpregadorDomestico;
    }

    private IReadOnlyList<BeneficioFuncionario> LerBeneficios()
    {
        if (BeneficiosDetalhados.Count > CalculadoraCustoFuncionario.LimiteBeneficios)
            throw new EntradaInvalidaException("Benefícios detalhados: informe no máximo 30 linhas.");
        return BeneficiosDetalhados.Select((linha, indice) => new BeneficioFuncionario(linha.Descricao.Trim(),
            NumeroLinha(linha.Custo, indice, "custo"), NumeroLinha(linha.Participacao, indice, "participação"))).ToArray();
    }

    private static decimal NumeroLinha(string texto, int indice, string campo)
    {
        if (LeituraNumerica.TentarLer(texto, out var valor)) return valor;
        throw new EntradaInvalidaException($"Benefício {indice + 1}: informe o {campo} em reais, por exemplo 350,00.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var regime = (RegimeTributario)Opcao("regime");
        return _simulador.ExecutarAsync(new SimularCustoFuncionarioRequest(Numero("salario"), regime,
            Opcao("rat") + 1m, Campo("fap").Visivel ? Numero("fap") : 1m,
            Campo("terceiros").Visivel ? Numero("terceiros") : 0m, Numero("beneficios"),
            Sim("provisoes"), Campo("aprendiz").Visivel && Sim("aprendiz"), Numero("horas"), LerBeneficios()), cancellationToken);
    }
}
