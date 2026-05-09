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

using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the provider configuration bound to a <see cref="GraphContext"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GraphContextOptions
{
    /// <summary>
    /// Gets the <see cref="IStatementEmitter"/> that translates change-tracker operations into provider-specific statements.
    /// </summary>
    /// <value>The provider-supplied <see cref="IStatementEmitter"/>.</value>
    public required IStatementEmitter StatementEmitter { get; init; }

    /// <summary>
    /// Gets the <see cref="IResultMaterializer"/> that projects executor result rows into CLR node and edge instances.
    /// </summary>
    /// <value>The provider-supplied <see cref="IResultMaterializer"/>.</value>
    public required IResultMaterializer ResultMaterializer { get; init; }

    /// <summary>
    /// Gets the <see cref="IRawStatementExecutor"/> that runs raw provider statements against the underlying store.
    /// </summary>
    /// <value>The provider-supplied <see cref="IRawStatementExecutor"/>.</value>
    public required IRawStatementExecutor RawStatementExecutor { get; init; }

    /// <summary>
    /// Gets the <see cref="IGraphTransactionOpener"/> that opens provider-specific transactions.
    /// </summary>
    /// <value>The provider-supplied <see cref="IGraphTransactionOpener"/>.</value>
    public required IGraphTransactionOpener GraphTransactionOpener { get; init; }

    /// <summary>
    /// Gets the <see cref="ITraversalTranslator"/> that translates a <see cref="TraversalAst"/> into an executable provider query.
    /// </summary>
    /// <value>The provider-supplied <see cref="ITraversalTranslator"/>.</value>
    public required ITraversalTranslator TraversalTranslator { get; init; }

    /// <summary>
    /// Gets the optional <see cref="ILoggerFactory"/> used to produce loggers for diagnostic output.
    /// </summary>
    /// <value>The <see cref="ILoggerFactory"/>, or <see langword="null"/> to disable logging.</value>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>
    /// Gets the bootstrap delegate that composes the per-context <see cref="GraphContextServices"/> bundle.
    /// </summary>
    /// <value>The bootstrap delegate, supplied by the dependency-injection registration extension.</value>
    internal Func<GraphContext, GraphContextOptions, GraphContextServices> CreateServices { get; init; } = null!;
}
