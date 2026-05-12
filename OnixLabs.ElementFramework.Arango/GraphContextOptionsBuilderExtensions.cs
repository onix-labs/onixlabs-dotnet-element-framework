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

using ArangoDBNetStandard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the set of extension methods for binding a graph context to an ArangoDB endpoint.
/// </summary>
public static class GraphContextOptionsBuilderExtensions
{
    /// <summary>
    /// Binds the configured graph context to an ArangoDB endpoint.
    /// </summary>
    /// <remarks>
    /// <b>Deadlock vector under captured sync contexts.</b> The provider bridges its sync surface to the async ArangoDB HTTP client via <c>GetAwaiter().GetResult()</c>; under hosts that capture a synchronization context (ASP.NET Classic, WinForms, WPF) prefer the async surface (<c>SaveChangesAsync</c>, <c>BeginTransactionAsync</c>, etc.). ASP.NET Core and console applications are unaffected.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="endpoint">The ArangoDB endpoint URI (e.g. <c>http://localhost:8529</c>).</param>
    /// <param name="databaseName">The database name to target. The database must exist; this provider does not create databases.</param>
    /// <param name="username">The ArangoDB username.</param>
    /// <param name="password">The ArangoDB password.</param>
    /// <returns>Returns the same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="endpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="databaseName"/>, <paramref name="username"/>, or <paramref name="password"/> is null, empty, or whitespace.</exception>
    public static GraphContextOptionsBuilder UseArango(
        this GraphContextOptionsBuilder builder,
        Uri endpoint,
        string databaseName,
        string username,
        string password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        return builder.UseArango(() => endpoint, databaseName, username, password);
    }

    /// <summary>
    /// Binds the configured graph context to an ArangoDB endpoint, deferring endpoint resolution until the client is first used.
    /// </summary>
    /// <remarks>
    /// Use this overload when the endpoint is not known at <c>AddGraphContext</c> registration time — for example when the endpoint is provided by a Testcontainers instance whose port is only mapped after the container starts.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="endpointFactory">A factory that returns the endpoint URI on demand. Invoked once on first client resolution; the result is cached process-wide.</param>
    /// <param name="databaseName">The database name to target.</param>
    /// <param name="username">The ArangoDB username.</param>
    /// <param name="password">The ArangoDB password.</param>
    /// <returns>Returns the same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="endpointFactory"/>, or <paramref name="password"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="databaseName"/> or <paramref name="username"/> is null, empty, or whitespace.</exception>
    public static GraphContextOptionsBuilder UseArango(
        this GraphContextOptionsBuilder builder,
        Func<Uri> endpointFactory,
        string databaseName,
        string username,
        string password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        ILoggerFactory? loggerFactory = builder.LoggerFactory;
        ILogger<ArangoRawStatementExecutor> executorLogger = loggerFactory?.CreateLogger<ArangoRawStatementExecutor>() ?? NullLogger<ArangoRawStatementExecutor>.Instance;
        ILogger<ArangoGraphTransactionOpener> openerLogger = loggerFactory?.CreateLogger<ArangoGraphTransactionOpener>() ?? NullLogger<ArangoGraphTransactionOpener>.Instance;
        ILogger<ArangoGraphTransaction> transactionLogger = loggerFactory?.CreateLogger<ArangoGraphTransaction>() ?? NullLogger<ArangoGraphTransaction>.Instance;

        Lazy<ArangoDBClient> client = new(
            () => ArangoClientCache.GetOrCreate(endpointFactory(), databaseName, username, password),
            LazyThreadSafetyMode.ExecutionAndPublication);

        ArangoStatementEmitter emitter = new();
        ArangoResultMaterializer materializer = new();
        ArangoGraphTransactionOpener opener = new(client.Value, openerLogger, transactionLogger);
        ArangoRawStatementExecutor executor = new(client.Value, opener, executorLogger);
        ArangoTraversalTranslator translator = new(emitter, executor, materializer);

        return builder
            .UseStatementEmitter(emitter)
            .UseResultMaterializer(materializer)
            .UseRawStatementExecutor(executor)
            .UseGraphTransactionOpener(opener)
            .UseTraversalTranslator(translator);
    }
}
