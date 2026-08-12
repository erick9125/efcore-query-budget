using EfCoreQueryBudget;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EfCoreQueryBudget.Tests.Unit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_the_interceptor_as_a_singleton()
    {
        var provider = new ServiceCollection()
            .AddEfCoreQueryBudget()
            .BuildServiceProvider();

        var first = provider.GetRequiredService<QueryBudgetCommandInterceptor>();
        var second = provider.GetRequiredService<QueryBudgetCommandInterceptor>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Capture_is_enabled_by_default()
    {
        var provider = new ServiceCollection()
            .AddEfCoreQueryBudget()
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<QueryBudgetLibraryOptions>>()
            .Value.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Configuration_callback_reaches_the_resolved_options()
    {
        var provider = new ServiceCollection()
            .AddEfCoreQueryBudget(options => options.Enabled = false)
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<QueryBudgetLibraryOptions>>()
            .Value.Enabled.Should().BeFalse();
    }
}
