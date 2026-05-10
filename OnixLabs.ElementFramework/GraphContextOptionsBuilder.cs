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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the fluent builder that binds provider implementations to a <see cref="GraphContext"/>.
/// </summary>
public sealed class GraphContextOptionsBuilder
{
    /// <summary>
    /// The configured <see cref="IStatementEmitter"/>, or <see langword="null"/> when not yet supplied.
    /// </summary>
    private IStatementEmitter? statementEmitter;

    /// <summary>
    /// The configured <see cref="IResultMaterializer"/>, or <see langword="null"/> when not yet supplied.
    /// </summary>
    private IResultMaterializer? resultMaterializer;

    /// <summary>
    /// The configured <see cref="IRawStatementExecutor"/>, or <see langword="null"/> when not yet supplied.
    /// </summary>
    private IRawStatementExecutor? rawStatementExecutor;

    /// <summary>
    /// The configured <see cref="IGraphTransactionOpener"/>, or <see langword="null"/> when not yet supplied.
    /// </summary>
    private IGraphTransactionOpener? graphTransactionOpener;

    /// <summary>
    /// The configured <see cref="ITraversalTranslator"/>, or <see langword="null"/> when not yet supplied.
    /// </summary>
    private ITraversalTranslator? traversalTranslator;

    /// <summary>
    /// The configured <see cref="ILoggerFactory"/>, or <see langword="null"/> when logging has not been opted in to.
    /// </summary>
    private ILoggerFactory? loggerFactory;

    /// <summary>
    /// Sets the <see cref="IStatementEmitter"/> implementation that translates change-tracker operations into provider-specific statements.
    /// </summary>
    /// <param name="emitter">The provider's statement emitter.</param>
    /// <returns>Returns this <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="emitter"/> is <see langword="null"/>.</exception>
    public GraphContextOptionsBuilder UseStatementEmitter(IStatementEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        statementEmitter = emitter;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IResultMaterializer"/> implementation that projects executor result rows into CLR node and edge instances.
    /// </summary>
    /// <param name="materializer">The provider's result materializer.</param>
    /// <returns>Returns this <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="materializer"/> is <see langword="null"/>.</exception>
    public GraphContextOptionsBuilder UseResultMaterializer(IResultMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(materializer);
        resultMaterializer = materializer;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IRawStatementExecutor"/> implementation that runs raw provider statements against the underlying store.
    /// </summary>
    /// <param name="executor">The provider's raw statement executor.</param>
    /// <returns>Returns this <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="executor"/> is <see langword="null"/>.</exception>
    public GraphContextOptionsBuilder UseRawStatementExecutor(IRawStatementExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        rawStatementExecutor = executor;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IGraphTransactionOpener"/> implementation that opens provider-specific transactions.
    /// </summary>
    /// <param name="opener">The provider's transaction opener.</param>
    /// <returns>Returns this <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="opener"/> is <see langword="null"/>.</exception>
    public GraphContextOptionsBuilder UseGraphTransactionOpener(IGraphTransactionOpener opener)
    {
        ArgumentNullException.ThrowIfNull(opener);
        graphTransactionOpener = opener;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="ITraversalTranslator"/> implementation that translates a <see cref="TraversalAst"/> into an executable provider query.
    /// </summary>
    /// <param name="translator">The provider's traversal translator.</param>
    /// <returns>Returns this <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="translator"/> is <see langword="null"/>.</exception>
    public GraphContextOptionsBuilder UseTraversalTranslator(ITraversalTranslator translator)
    {
        ArgumentNullException.ThrowIfNull(translator);
        traversalTranslator = translator;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="ILoggerFactory"/> used to produce loggers for diagnostic output. Optional; when not configured, logging is disabled.
    /// </summary>
    /// <remarks>
    /// Call this <b>before</b> the provider's <c>Use*</c> extension method. Provider <c>Use*</c> extensions read the
    /// configured factory at the point they construct their services; if the factory is supplied afterwards the
    /// provider services will already have been constructed with logging disabled.
    /// </remarks>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>Returns this <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
    public GraphContextOptionsBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        this.loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Gets the <see cref="ILoggerFactory"/> configured via <see cref="UseLoggerFactory"/>, or <see langword="null"/> when none has been supplied.
    /// </summary>
    /// <remarks>
    /// Exposed for provider <c>Use*</c> extension methods that need to inject loggers into their provider services
    /// at composition time. Consumer code should not need to read this.
    /// </remarks>
    /// <value>The configured <see cref="ILoggerFactory"/>, or <see langword="null"/> when none has been supplied.</value>
    public ILoggerFactory? LoggerFactory => loggerFactory;

    /// <summary>
    /// Builds an immutable <see cref="GraphContextOptions"/> containing the configured provider services and the supplied bootstrap delegate.
    /// </summary>
    /// <param name="createServices">The delegate that composes the per-context <see cref="GraphContextServices"/> bundle.</param>
    /// <returns>Returns an immutable <see cref="GraphContextOptions"/> carrying the configured provider implementations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="createServices"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when one or more required provider services have not been supplied.</exception>
    internal GraphContextOptions Build(Func<GraphContext, GraphContextOptions, GraphContextServices> createServices)
    {
        ArgumentNullException.ThrowIfNull(createServices);
        return new GraphContextOptions
        {
            StatementEmitter = Required(statementEmitter, nameof(UseStatementEmitter)),
            ResultMaterializer = Required(resultMaterializer, nameof(UseResultMaterializer)),
            RawStatementExecutor = Required(rawStatementExecutor, nameof(UseRawStatementExecutor)),
            GraphTransactionOpener = Required(graphTransactionOpener, nameof(UseGraphTransactionOpener)),
            TraversalTranslator = Required(traversalTranslator, nameof(UseTraversalTranslator)),
            LoggerFactory = loggerFactory,
            CreateServices = createServices,
        };
    }

    /// <summary>
    /// Returns <paramref name="value"/> when it is not <see langword="null"/>; otherwise throws an <see cref="InvalidOperationException"/> naming the missing configuration method.
    /// </summary>
    /// <typeparam name="T">The reference type of the configured service.</typeparam>
    /// <param name="value">The configured service value.</param>
    /// <param name="method">The name of the configuration method that should have supplied <paramref name="value"/>.</param>
    /// <returns>Returns the non-<see langword="null"/> value of <paramref name="value"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    private static T Required<T>(T? value, string method) where T : class =>
        value ?? throw new InvalidOperationException($"Required service has not been configured. Call {method}(...) before building.");
}
