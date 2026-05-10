// MIT License

// Copyright (c) 2020 ONIXLabs

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OnixLabs.ElementFramework.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddGraphContext throws ArgumentNullException when services is null")]
    public void AddGraphContextThrowsForNullServices()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddGraphContext<RegistrationContext>(
            services: null!,
            configure: ConfigureFakes));
    }

    [Fact(DisplayName = "AddGraphContext with the SP-aware overload throws ArgumentNullException when configure is null")]
    public void AddGraphContextSpOverloadThrowsForNullConfigure()
    {
        ServiceCollection services = new();
        Assert.Throws<ArgumentNullException>(() => services.AddGraphContext<RegistrationContext>(
            configure: (Action<IServiceProvider, GraphContextOptionsBuilder>)null!));
    }

    [Fact(DisplayName = "AddGraphContext with the SP-aware overload invokes configure with the resolving service provider")]
    public void AddGraphContextSpOverloadPassesServiceProviderToConfigure()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        IServiceProvider? captured = null;
        services.AddGraphContext<RegistrationContext>((sp, builder) =>
        {
            captured = sp;
            ConfigureFakes(builder);
        });

        _ = services.BuildServiceProvider().GetRequiredService<RegistrationContext>();
        Assert.NotNull(captured);
        Assert.Same(NullLoggerFactory.Instance, captured.GetRequiredService<ILoggerFactory>());
    }

    [Fact(DisplayName = "AddGraphContext with the SP-aware overload resolves a usable context wired with the host's ILoggerFactory")]
    public void AddGraphContextSpOverloadWiresLoggerFactoryFromContainer()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddGraphContext<RegistrationContext>((sp, builder) =>
        {
            builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
            ConfigureFakes(builder);
        });

        RegistrationContext context = services.BuildServiceProvider().GetRequiredService<RegistrationContext>();
        Assert.NotNull(context);
        Assert.NotNull(context.Traversal);
    }

    [Fact(DisplayName = "AddGraphContext with the SP-aware overload invokes configure once per resolve")]
    public void AddGraphContextSpOverloadInvokesConfigurePerResolve()
    {
        ServiceCollection services = new();
        int invocations = 0;
        services.AddGraphContext<RegistrationContext>((_, builder) =>
        {
            invocations++;
            ConfigureFakes(builder);
        }, lifetime: ServiceLifetime.Transient);

        ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<RegistrationContext>();
        _ = provider.GetRequiredService<RegistrationContext>();
        Assert.Equal(2, invocations);
    }

    private static void ConfigureFakes(GraphContextOptionsBuilder builder) => _ = builder
        .UseStatementEmitter(new FakeStatementEmitter())
        .UseResultMaterializer(new FakeResultMaterializer())
        .UseRawStatementExecutor(new FakeRawStatementExecutor())
        .UseGraphTransactionOpener(new FakeGraphTransactionOpener())
        .UseTraversalTranslator(new FakeTraversalTranslator());
}

internal sealed class RegistrationContext(GraphContextOptions options) : GraphContext(options)
{
    protected internal override void OnModelCreating(IGraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<Author>().HasKey(a => a.Id);
    }
}
