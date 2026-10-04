using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Afastamento por doença ou acidente e licenças-maternidade e paternidade: quem paga, quanto e até quando.</summary>
public sealed class SimularAfastamentoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularAfastamentoRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularAfastamentoRequest r, CancellationToken cancellationToken)
    {
        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(new DateOnly(r.Inicio.Year, r.Inicio.Month, 1), cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var salarioMinimo = tabelas.ObterSalarioMinimo();
        if (salarioMinimo.Falhou)
            return salarioMinimo.Erro;

        var calculo = CalculadoraAfastamento.Calcular(r.Tipo, r.Inicio, r.Dias, r.Remuneracao, r.Media, r.EmpresaCidada, salarioMinimo.Valor, tabelas.TetoInss);
        if (calculo.Falhou)
            return calculo.Erro;
        var a = calculo.Valor;

        var proventos = new List<VerbaDto>();
        if (a.PagoPelaEmpresa > 0m)
            proventos.Add(new(RotuloEmpresa(a.Tipo), Formato.Dias(a.DiasEmpresa), a.PagoPelaEmpresa));
        if (a.PagoPelaPrevidencia > 0m)
            proventos.Add(new(RotuloPrevidencia(a.Tipo), Formato.Dias(a.DiasPrevidencia), a.PagoPelaPrevidencia));
        var informativos = new List<VerbaDto> { new(a.Tipo == TipoAfastamento.Doenca ? "FGTS dos dias pagos pela empresa" : "FGTS do período", "8%", a.DepositoFgts) };

        return new DemonstrativoDto(
            Titulo(a.Tipo),
            $"De {Formato.Data(a.Inicio)} a {Formato.Data(a.Fim)} • {Formato.Dias(a.Dias)}",
            [
                new("Retorno previsto", Formato.Data(a.Retorno), Formato.Dias(a.Dias) + " de afastamento"),
                new("Pago pela empresa", Formato.Moeda(a.PagoPelaEmpresa), a.DiasEmpresa > 0 ? Formato.Dias(a.DiasEmpresa) : "Nenhum dia"),
                new(a.Tipo == TipoAfastamento.Maternidade ? "Salário-maternidade" : "Pago pelo INSS", Formato.Moeda(a.PagoPelaPrevidencia),
                    a.DiasPrevidencia > 0 ? $"{Formato.Dias(a.DiasPrevidencia)} de {Formato.Moeda(a.BeneficioMensal)} por mês" : "Nenhum dia"),
                new("Estabilidade até", a.FimEstabilidade is { } fim ? Formato.Data(fim) : "Não há", Estabilidade(a.Tipo))
            ],
            proventos,
            [],
            informativos,
            [new GrupoMemoriaDto(Titulo(a.Tipo), $"Total: {Formato.Moeda(a.PagoPelaEmpresa + a.PagoPelaPrevidencia)}", Formulas(a, r))],
            Observacoes(a),
            RotuloProventos: "Valores do período (brutos)",
            RotuloResultado: "Total do período");
    }

    private static List<FormulaDto> Formulas(Afastamento a, SimularAfastamentoRequest r)
    {
        var diaria = $"{Formato.Moeda(r.Remuneracao)} ÷ 30";
        var formulas = new List<FormulaDto> { new("Período", $"De {Formato.Data(a.Inicio)} a {Formato.Data(a.Fim)}: {Formato.Dias(a.Dias)}; retorno em {Formato.Data(a.Retorno)}") };
        switch (a.Tipo)
        {
            case TipoAfastamento.Doenca or TipoAfastamento.AcidenteDeTrabalho:
                formulas.Add(new("15 primeiros dias", $"{diaria} x {Formato.Dias(a.DiasEmpresa)} = {Formato.Moeda(a.PagoPelaEmpresa)}, pagos pela empresa (Lei 8.213/1991, art. 60, § 3º)"));
                if (a.DiasPrevidencia > 0)
                {
                    formulas.Add(new("Benefício mensal estimado", $"{Formato.Moeda(r.Media > 0m ? r.Media : r.Remuneracao)} x 91% = {Formato.Moeda(a.BeneficioMensal)}, entre o salário mínimo e o teto do INSS (art. 61)"));
                    formulas.Add(new("A partir do 16º dia", $"{Formato.Moeda(a.BeneficioMensal)} ÷ 30 x {Formato.Dias(a.DiasPrevidencia)} = {Formato.Moeda(a.PagoPelaPrevidencia)}, pagos pelo INSS"));
                }
                break;
            case TipoAfastamento.Maternidade:
                formulas.Add(new("Salário-maternidade", $"{diaria} x 120 dias = {Formato.Moeda(a.PagoPelaPrevidencia)}: pago pela empresa e compensado nas contribuições ao INSS (Lei 8.213/1991, art. 72, § 1º)"));
                if (a.DiasEmpresa > 0)
                    formulas.Add(new("Prorrogação da Empresa Cidadã", $"{diaria} x 60 dias = {Formato.Moeda(a.PagoPelaEmpresa)}, pagos pela empresa e deduzidos do IRPJ no Lucro Real (Lei 11.770/2008)"));
                break;
            default:
                formulas.Add(new("Licença-paternidade", $"{CalculadoraAfastamento.DiasPaternidade(a.Inicio.Year)} dias em {a.Inicio.Year} (LC 229/2026){(a.DiasEmpresa > CalculadoraAfastamento.DiasPaternidade(a.Inicio.Year) ? " + 15 da Empresa Cidadã" : "")}: {diaria} x {Formato.Dias(a.Dias)} = {Formato.Moeda(a.PagoPelaEmpresa)}"));
                break;
        }
        formulas.Add(new("FGTS", a.Tipo == TipoAfastamento.Doenca
            ? $"8% dos dias pagos pela empresa = {Formato.Moeda(a.DepositoFgts)}; do 16º dia em diante, na doença comum, não há depósito"
            : $"8% da remuneração do período = {Formato.Moeda(a.DepositoFgts)}"));
        return formulas;
    }

    private static List<string> Observacoes(Afastamento a)
    {
        var observacoes = new List<string>();
        switch (a.Tipo)
        {
            case TipoAfastamento.Doenca or TipoAfastamento.AcidenteDeTrabalho:
                observacoes.Add("O benefício do INSS é estimado: o valor exato sai da média de todos os salários de contribuição desde 07/1994, e o auxílio não pode passar da média dos 12 últimos (Lei 8.213/1991, art. 29, § 10). Ele é pago pelo INSS, com o próprio desconto de IRRF, quando houver.");
                observacoes.Add("A remuneração dos 15 primeiros dias tem INSS, IRRF e FGTS, como salário. Atestados do mesmo motivo em até 60 dias se somam para contar os 15 dias.");
                observacoes.Add("No 13º, a empresa paga os meses com 15 dias ou mais de trabalho, e o INSS paga o abono anual do período do benefício.");
                if (a.Tipo == TipoAfastamento.AcidenteDeTrabalho)
                    observacoes.Add("No acidente de trabalho, o FGTS continua sendo depositado durante todo o afastamento, e, depois de receber o auxílio do INSS, o empregado tem 12 meses de estabilidade a partir do retorno (Lei 8.213/1991, art. 118). A empresa deve emitir a CAT.");
                if (a.PodePerderFerias)
                    observacoes.Add("Afastamento de mais de 6 meses, contínuos ou não, no mesmo período aquisitivo tira o direito às férias desse período; um novo período começa no retorno (CLT, art. 133, IV).");
                break;
            case TipoAfastamento.Maternidade:
                observacoes.Add("A licença pode começar até 28 dias antes do parto. A empregada tem estabilidade da confirmação da gravidez até 5 meses após o parto (ADCT, art. 10, II, b); a data acima considera o parto no início da licença.");
                observacoes.Add("O salário-maternidade tem desconto de INSS e de IRRF, como salário. Para a empregada doméstica e para quem não tem empregador, ele é pago diretamente pelo INSS. O valor é limitado ao subsídio dos ministros do STF.");
                observacoes.Add("A licença conta como tempo de serviço para as férias e o 13º, e o FGTS é depositado normalmente.");
                break;
            default:
                observacoes.Add("A LC 229/2026 amplia a licença-paternidade aos poucos: 5 dias até 2026, 10 em 2027, 15 em 2028 e 20 a partir de 2029, e cria o salário-paternidade da Previdência; confira na regulamentação como a empresa paga e compensa o benefício. A Empresa Cidadã acrescenta 15 dias (Lei 11.770/2008).");
                observacoes.Add("O pai tem garantia de emprego até um mês depois do fim da licença (LC 229/2026).");
                break;
        }
        return observacoes;
    }

    private static string Titulo(TipoAfastamento tipo) => tipo switch
    {
        TipoAfastamento.Doenca => "Afastamento por doença",
        TipoAfastamento.AcidenteDeTrabalho => "Afastamento por acidente de trabalho",
        TipoAfastamento.Maternidade => "Licença-maternidade",
        _ => "Licença-paternidade"
    };

    private static string RotuloEmpresa(TipoAfastamento tipo) => tipo switch
    {
        TipoAfastamento.Maternidade => "Prorrogação da Empresa Cidadã (empresa)",
        TipoAfastamento.Paternidade => "Licença-paternidade (empresa)",
        _ => "15 primeiros dias (empresa)"
    };

    private static string RotuloPrevidencia(TipoAfastamento tipo) => tipo == TipoAfastamento.Maternidade
        ? "Salário-maternidade (compensado pela empresa)"
        : tipo == TipoAfastamento.AcidenteDeTrabalho ? "Auxílio por incapacidade acidentário (INSS, estimado)" : "Auxílio por incapacidade temporária (INSS, estimado)";

    private static string Estabilidade(TipoAfastamento tipo) => tipo switch
    {
        TipoAfastamento.AcidenteDeTrabalho => "12 meses após o retorno",
        TipoAfastamento.Maternidade => "5 meses após o parto",
        TipoAfastamento.Paternidade => "1 mês após a licença",
        _ => "Sem estabilidade na doença comum"
    };
}
