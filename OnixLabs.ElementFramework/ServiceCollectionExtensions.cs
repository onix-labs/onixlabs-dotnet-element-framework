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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Provides extension methods for registering an Element Framework graph context with a dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="GraphContext"/> subclass with the supplied service collection.
    /// </summary>
    /// <remarks>
    /// The <paramref name="configure"/> action configures a <see cref="GraphContextOptionsBuilder"/> for the registered context, typically by invoking a provider extension method (for example <c>builder.UseNeo4j(...)</c>) that supplies the provider-specific implementations. The default lifetime is <see cref="ServiceLifetime.Scoped"/> because graph contexts carry per-unit-of-work state (change tracker, identity map, ambient transaction); override to <see cref="ServiceLifetime.Singleton"/> only for single-threaded long-lived consumers such as CLI tools or background workers.
    /// </remarks>
    /// <typeparam name="TContext">The <see cref="GraphContext"/> subclass to register.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> with which to register the context.</param>
    /// <param name="configure">An optional action that configures the <see cref="GraphContextOptionsBuilder"/> for the context.</param>
    /// <param name="serviceKey">An optional key used to differentiate between multiple registrations of the same context type.</param>
    /// <param name="lifetime">The lifetime with which to register the context.</param>
    /// <returns>Returns the supplied <paramref name="services"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when one or more required provider services have not been supplied via <paramref name="configure"/>.</exception>
    public static IServiceCollection AddGraphContext<TContext>(
        this IServiceCollection services,
        Action<GraphContextOptionsBuilder>? configure = null,
        object? serviceKey = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) where TContext : GraphContext
    {
        ArgumentNullException.ThrowIfNull(services);

        GraphContextOptionsBuilder builder = new();
        configure?.Invoke(builder);
        GraphContextOptions options = builder.Build(BuildGraphContextServices);

        ServiceDescriptor descriptor = serviceKey is null
            ? new ServiceDescriptor(typeof(TContext), sp => ActivatorUtilities.CreateInstance<TContext>(sp, options), lifetime)
            : new ServiceDescriptor(
                typeof(TContext),
                serviceKey,
                (sp, _) => ActivatorUtilities.CreateInstance<TContext>(sp, options),
                lifetime);

        services.Add(descriptor);
        return services;
    }

    /// <summary>
    /// Registers a <see cref="GraphContext"/> subclass whose options are built per-resolve with access to the resolving <see cref="IServiceProvider"/>.
    /// </summary>
    /// <remarks>
    /// Use this overload when the options builder needs to pull services from the container — most commonly the host's
    /// <see cref="ILoggerFactory"/> to wire diagnostic logging:
    /// <code>
    /// services.AddGraphContext&lt;BlogGraphContext&gt;((sp, builder) =&gt;
    ///     builder
    ///         .UseLoggerFactory(sp.GetRequiredService&lt;ILoggerFactory&gt;())
    ///         .UseNeo4j(connectionString, authToken));
    /// </code>
    /// The configure delegate is invoked once per resolve, so it must be cheap. Provider <c>Use*</c> extensions read the
    /// configured <see cref="ILoggerFactory"/> from the builder at composition time, so <see cref="GraphContextOptionsBuilder.UseLoggerFactory"/>
    /// must be called before the provider's <c>Use*</c> extension.
    /// </remarks>
    /// <typeparam name="TContext">The <see cref="GraphContext"/> subclass to register.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> with which to register the context.</param>
    /// <param name="configure">The action that configures the <see cref="GraphContextOptionsBuilder"/> with access to the resolving service provider.</param>
    /// <param name="serviceKey">An optional key used to differentiate between multiple registrations of the same context type.</param>
    /// <param name="lifetime">The lifetime with which to register the context.</param>
    /// <returns>Returns the supplied <paramref name="services"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when one or more required provider services have not been supplied via <paramref name="configure"/>.</exception>
    public static IServiceCollection AddGraphContext<TContext>(
        this IServiceCollection services,
        Action<IServiceProvider, GraphContextOptionsBuilder> configure,
        object? serviceKey = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) where TContext : GraphContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        ServiceDescriptor descriptor = serviceKey is null
            ? new ServiceDescriptor(typeof(TContext), sp => Create(sp), lifetime)
            : new ServiceDescriptor(typeof(TContext), serviceKey, (sp, _) => Create(sp), lifetime);

        services.Add(descriptor);
        return services;

        TContext Create(IServiceProvider sp)
        {
            GraphContextOptionsBuilder builder = new();
            configure(sp, builder);
            GraphContextOptions options = builder.Build(BuildGraphContextServices);
            return (TContext)ActivatorUtilities.CreateInstance<TContext>(sp, options);
        }
    }

    /// <summary>
    /// Composes the per-context <see cref="GraphContextServices"/> bundle from the resolved model and configured provider services.
    /// </summary>
    /// <param name="context">The <see cref="GraphContext"/> being initialised.</param>
    /// <param name="options">The configured <see cref="GraphContextOptions"/> carrying the provider services.</param>
    /// <returns>Returns the composed <see cref="GraphContextServices"/> bundle for the supplied context.</returns>
    private static GraphContextServices BuildGraphContextServices(GraphContext context, GraphContextOptions options)
    {
        IGraphModel model = ModelSource.ModelFor(context);
        ILogger<ChangeTracker> changeTrackerLogger = options.LoggerFactory?.CreateLogger<ChangeTracker>() ?? NullLogger<ChangeTracker>.Instance;
        ChangeTracker changeTracker = new(
            model, options.StatementEmitter, options.RawStatementExecutor, options.GraphTransactionOpener, changeTrackerLogger);
        GraphSetFactory setFactory = new(
            model, changeTracker, options.StatementEmitter, options.RawStatementExecutor, options.ResultMaterializer);
        GraphTransactionFactory transactionFactory = new(options.GraphTransactionOpener, changeTracker);
        GraphTraversal traversal = new(model, options.TraversalTranslator);
        return new GraphContextServices(changeTracker, setFactory, transactionFactory, traversal, options.RawStatementExecutor);
    }
}
