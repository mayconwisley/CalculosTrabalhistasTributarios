using CalculosTrabalhistasTributarios.Application.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Tabelas;

/// <summary>Registro da lista da tabela, com os valores já formatados para a tela.</summary>
public sealed partial class LinhaRegistroTabelaMobile(RegistroTabelaDto registro, string competencia, string faixa, string valor, string aliquota, string deducao)
    : ObservableObject
{
    [ObservableProperty] private bool _selecionada;

    public RegistroTabelaDto Registro { get; } = registro;
    public string Competencia { get; } = competencia;
    public string Faixa { get; } = faixa;
    public string Valor { get; } = valor;
    public string Aliquota { get; } = aliquota;
    public string Deducao { get; } = deducao;
}
