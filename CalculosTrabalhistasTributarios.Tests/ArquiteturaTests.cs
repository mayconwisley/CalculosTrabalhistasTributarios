using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Presentation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Regras da arquitetura em camadas: dependências só para dentro e a composição completa dos serviços.</summary>
public class ArquiteturaTests
{
    private static readonly Assembly Domain = typeof(Domain.Comum.Result).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Composicao_monta_todos_os_servicos_das_camadas()
    {
        // ValidateOnBuild confere, sem criar as janelas, que cada serviço registrado tem todas as dependências.
        var servicos = new ServiceCollection().AddApplication().AddInfrastructure().AddPresentation();
        using var provedor = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.NotEmpty(servicos);
    }

    [Fact]
    public void Domain_nao_depende_de_outra_camada()
    {
        var referencias = Domain.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain(Application.GetName().Name, referencias);
        Assert.DoesNotContain(Infrastructure.GetName().Name, referencias);
        Assert.DoesNotContain("CalculosTrabalhistasTributarios", referencias);
    }

    [Fact]
    public void Application_depende_so_do_domain()
    {
        var referencias = Application.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.Contains(Domain.GetName().Name, referencias);
        Assert.DoesNotContain(Infrastructure.GetName().Name, referencias);
        Assert.DoesNotContain("CalculosTrabalhistasTributarios", referencias);
    }

    [Theory]
    [InlineData("CalculosTrabalhistasTributarios.Domain")]
    [InlineData("CalculosTrabalhistasTributarios.Application")]
    [InlineData("CalculosTrabalhistasTributarios.Infrastructure")]
    [InlineData("CalculosTrabalhistasTributarios")]
    public void Interfaces_ficam_na_pasta_Interfaces(string nomeAssembly)
    {
        var assembly = Assembly.Load(nomeAssembly);
        var foraDoLugar = assembly.GetTypes()
            .Where(tipo => tipo.IsInterface && tipo.Namespace is { } ns && ns.StartsWith("CalculosTrabalhistasTributarios", StringComparison.Ordinal) && !ns.EndsWith(".Interfaces", StringComparison.Ordinal))
            .Select(tipo => tipo.FullName)
            .ToArray();
        Assert.Empty(foraDoLugar);
    }
}
