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
using Neo4j.Driver;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the set of extension methods for binding a graph context to a Neo4j endpoint.
/// </summary>
public static class GraphContextOptionsBuilderExtensions
{
    /// <summary>
    /// Binds the configured graph context to a Neo4j endpoint by constructing and supplying the Neo4j-specific provider implementations to the <see cref="GraphContextOptionsBuilder"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="IDriver"/> for the supplied endpoint is shared process-wide. <b>Deadlock vector under captured sync contexts.</b> The Neo4j .NET driver (v6+) is async-only; this provider bridges the sync surface (<c>SaveChanges</c>, <c>BeginTransaction</c>, <c>FindById</c>, <c>AsEnumerable</c>, <c>RawStatement().Execute(...)</c>, etc.) to the async driver via <c>GetAwaiter().GetResult()</c>. Under hosts that capture a synchronization context — ASP.NET Classic, WinForms, WPF — calling these sync members can deadlock. Use the async surface (<c>SaveChangesAsync</c>, <c>BeginTransactionAsync</c>, <c>FindByIdAsync</c>, <c>AsAsyncEnumerable</c>, <c>RawStatement().ExecuteAsync(...)</c>) in those hosts. ASP.NET Core, console applications, and modern hosted-service consumers do not capture a synchronization context and are unaffected.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="connectionString">The Bolt connection URI (e.g. <c>bolt://localhost:7687</c> or <c>neo4j://...</c>).</param>
    /// <param name="authToken">The auth token to bind the driver with, or <see langword="null"/> for unauthenticated.</param>
    /// <returns>Returns the same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.</exception>
    public static GraphContextOptionsBuilder UseNeo4j(
        this GraphContextOptionsBuilder builder,
        string connectionString,
        IAuthToken? authToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return builder.UseNeo4j(() => connectionString, authToken);
    }

    /// <summary>
    /// Binds the configured graph context to a Neo4j endpoint, deferring connection-string resolution and <see cref="IDriver"/> creation until the driver is first used.
    /// </summary>
    /// <remarks>
    /// Use this overload when the connection string is not known at <c>AddGraphContext</c> registration time — for example when the endpoint is provided by a Testcontainers instance whose port is only mapped after the container starts. Production wiring typically uses the eager overload because the connection string is known up front. <b>Deadlock vector under captured sync contexts.</b> See the eager overload's remarks.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="connectionStringFactory">A factory that returns the Bolt connection URI on demand. Invoked once on first <see cref="IDriver"/> resolution; the result is cached process-wide.</param>
    /// <param name="authToken">The auth token to bind the driver with, or <see langword="null"/> for unauthenticated.</param>
    /// <returns>Returns the same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionStringFactory"/> is <see langword="null"/>.</exception>
    public static GraphContextOptionsBuilder UseNeo4j(
        this GraphContextOptionsBuilder builder,
        Func<string> connectionStringFactory,
        IAuthToken? authToken = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory);

        ILoggerFactory? loggerFactory = builder.LoggerFactory;
        ILogger<Neo4jCypherExecutor> executorLogger = loggerFactory?.CreateLogger<Neo4jCypherExecutor>() ?? NullLogger<Neo4jCypherExecutor>.Instance;
        ILogger<Neo4jGraphTransactionOpener> openerLogger = loggerFactory?.CreateLogger<Neo4jGraphTransactionOpener>() ?? NullLogger<Neo4jGraphTransactionOpener>.Instance;
        ILogger<Neo4jGraphTransaction> transactionLogger = loggerFactory?.CreateLogger<Neo4jGraphTransaction>() ?? NullLogger<Neo4jGraphTransaction>.Instance;
        ILogger driverCacheLogger = loggerFactory?.CreateLogger(typeof(Neo4jDriverCache)) ?? NullLogger.Instance;

        Lazy<IDriver> driver = new(DriverFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        CypherEmitter emitter = new();
        Neo4jResultMaterializer materializer = new();
        Neo4jGraphTransactionOpener opener = new(driver, openerLogger, transactionLogger);
        Neo4jCypherExecutor executor = new(driver, opener, executorLogger);
        Neo4jTraversalTranslator translator = new(emitter, executor, materializer);

        return builder
            .UseStatementEmitter(emitter)
            .UseResultMaterializer(materializer)
            .UseRawStatementExecutor(executor)
            .UseGraphTransactionOpener(opener)
            .UseTraversalTranslator(translator);

        IDriver DriverFactory() => Neo4jDriverCache.GetOrCreate(connectionStringFactory(), authToken, driverCacheLogger);
    }
}
