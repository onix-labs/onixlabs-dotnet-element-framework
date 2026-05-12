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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the set of extension methods for binding a graph context to an Apache AGE endpoint.
/// </summary>
public static class GraphContextOptionsBuilderExtensions
{
    /// <summary>
    /// Binds the configured graph context to an Apache AGE endpoint by constructing and supplying the AGE-specific provider implementations to the <see cref="GraphContextOptionsBuilder"/>.
    /// </summary>
    /// <remarks>
    /// The underlying <see cref="NpgsqlDataSource"/> is cached process-wide per <c>(connection string, graph name)</c> pair. Each fresh physical connection runs <c>CREATE EXTENSION IF NOT EXISTS age</c>, <c>LOAD 'age'</c>, sets <c>search_path</c>, and creates the named graph if it doesn't exist. <b>Deadlock vector under captured sync contexts.</b> The provider bridges its sync surface to the async Npgsql driver via <c>GetAwaiter().GetResult()</c>; under hosts that capture a synchronization context (ASP.NET Classic, WinForms, WPF) prefer the async surface (<c>SaveChangesAsync</c>, <c>BeginTransactionAsync</c>, etc.). ASP.NET Core and console applications are unaffected.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="connectionString">The Postgres connection string for the AGE endpoint (e.g. <c>Host=...;Port=...;Username=...;Password=...;Database=...</c>).</param>
    /// <param name="graphName">The AGE graph name to target; created on first use if it does not already exist.</param>
    /// <returns>Returns the same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> or <paramref name="graphName"/> is null, empty, or whitespace.</exception>
    public static GraphContextOptionsBuilder UseAge(
        this GraphContextOptionsBuilder builder,
        string connectionString,
        string graphName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        return builder.UseAge(() => connectionString, graphName);
    }

    /// <summary>
    /// Binds the configured graph context to an Apache AGE endpoint, deferring connection-string resolution until the data source is first used.
    /// </summary>
    /// <remarks>
    /// Use this overload when the connection string is not known at <c>AddGraphContext</c> registration time — for example when the endpoint is provided by a Testcontainers instance whose port is only mapped after the container starts. Production wiring typically uses the eager overload because the connection string is known up front. <b>Deadlock vector under captured sync contexts.</b> See the eager overload's remarks.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="connectionStringFactory">A factory that returns the Postgres connection string on demand. Invoked once on first <see cref="NpgsqlDataSource"/> resolution; the result is cached process-wide.</param>
    /// <param name="graphName">The AGE graph name to target; created on first use if it does not already exist.</param>
    /// <returns>Returns the same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionStringFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="graphName"/> is null, empty, or whitespace.</exception>
    public static GraphContextOptionsBuilder UseAge(
        this GraphContextOptionsBuilder builder,
        Func<string> connectionStringFactory,
        string graphName)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);

        ILoggerFactory? loggerFactory = builder.LoggerFactory;
        ILogger<AgeRawStatementExecutor> executorLogger = loggerFactory?.CreateLogger<AgeRawStatementExecutor>() ?? NullLogger<AgeRawStatementExecutor>.Instance;
        ILogger<AgeGraphTransactionOpener> openerLogger = loggerFactory?.CreateLogger<AgeGraphTransactionOpener>() ?? NullLogger<AgeGraphTransactionOpener>.Instance;
        ILogger<AgeGraphTransaction> transactionLogger = loggerFactory?.CreateLogger<AgeGraphTransaction>() ?? NullLogger<AgeGraphTransaction>.Instance;
        ILogger dataSourceLogger = loggerFactory?.CreateLogger(typeof(AgeDataSourceCache)) ?? NullLogger.Instance;

        Lazy<NpgsqlDataSource> dataSource = new(
            () => AgeDataSourceCache.GetOrCreate(connectionStringFactory(), graphName, dataSourceLogger),
            LazyThreadSafetyMode.ExecutionAndPublication);

        AgeCypherEmitter emitter = new(graphName);
        AgeResultMaterializer materializer = new();
        AgeGraphTransactionOpener opener = new(dataSource.Value, openerLogger, transactionLogger);
        AgeRawStatementExecutor executor = new(dataSource.Value, opener, executorLogger);
        AgeTraversalTranslator translator = new(emitter, executor, materializer);

        return builder
            .UseStatementEmitter(emitter)
            .UseResultMaterializer(materializer)
            .UseRawStatementExecutor(executor)
            .UseGraphTransactionOpener(opener)
            .UseTraversalTranslator(translator);
    }
}
