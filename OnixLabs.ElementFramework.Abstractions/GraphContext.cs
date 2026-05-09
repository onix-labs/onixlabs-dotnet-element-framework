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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the base class for consumer-extensible graph contexts.
/// </summary>
public abstract class GraphContext : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The bundle of internal coordination services resolved for this context.
    /// </summary>
    private readonly GraphContextServices services;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphContext"/> class.
    /// </summary>
    /// <param name="options">The provider configuration bound to this context.</param>
    protected GraphContext(GraphContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        services = options.CreateServices(this, options);
    }

    /// <summary>
    /// Gets the fluent traversal entry point for this context.
    /// </summary>
    /// <value>The <see cref="IGraphTraversal"/> bound to this context's model and the provider's translator.</value>
    public IGraphTraversal Traversal => services.Traversal;

    /// <summary>
    /// Gets the raw statement executor that runs provider-native statements against the underlying store.
    /// </summary>
    /// <value>The <see cref="IRawStatementExecutor"/> bound to this context.</value>
    public IRawStatementExecutor RawStatement => services.RawStatementExecutor;

    /// <summary>
    /// Override to declare the graph domain model for this context: which CLR types are nodes, which are edges, their endpoints, key configuration, and property mappings.
    /// </summary>
    /// <param name="modelBuilder">The fluent builder to populate with node and relationship registrations.</param>
    protected internal virtual void OnModelCreating(IGraphModelBuilder modelBuilder)
    {
    }

    /// <summary>
    /// Returns the typed accessor for nodes of the specified type.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <returns>Returns the cached <see cref="INodeSet{T}"/> for this context and node type.</returns>
    public INodeSet<T> Nodes<T>() where T : class => services.SetFactory.GetNodesOfType<T>();

    /// <summary>
    /// Returns the typed accessor for edges of the specified type.
    /// </summary>
    /// <typeparam name="T">The edge type. Must be a registered edge in the model.</typeparam>
    /// <returns>Returns the cached <see cref="IEdgeSet{T}"/> for this context and edge type.</returns>
    public IEdgeSet<T> Edges<T>() where T : class => services.SetFactory.GetEdgesOfType<T>();

    /// <summary>
    /// Begins a new graph transaction.
    /// </summary>
    /// <returns>Returns the newly opened <see cref="IGraphTransaction"/>.</returns>
    /// <exception cref="GraphTransactionAlreadyActiveException">Thrown when another transaction is already active.</exception>
    /// <exception cref="GraphTransactionException">Thrown when the transaction cannot be opened.</exception>
    public IGraphTransaction BeginTransaction() => services.TransactionFactory.Open();

    /// <summary>
    /// Asynchronously begins a new graph transaction.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>Returns a task that resolves to the newly opened <see cref="IGraphTransaction"/>.</returns>
    /// <exception cref="GraphTransactionAlreadyActiveException">Thrown when another transaction is already active.</exception>
    /// <exception cref="GraphTransactionException">Thrown when the transaction cannot be opened.</exception>
    public Task<IGraphTransaction> BeginTransactionAsync(CancellationToken token = default) => services.TransactionFactory.OpenAsync(token);

    /// <summary>
    /// Flushes all pending tracked changes to the underlying store.
    /// </summary>
    /// <returns>Returns the number of changes that were written.</returns>
    /// <exception cref="GraphContextException">Thrown when the flush fails.</exception>
    public int SaveChanges() => services.ChangeTracker.Flush();

    /// <summary>
    /// Asynchronously flushes all pending tracked changes to the underlying store.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>Returns a task that resolves to the number of changes that were written.</returns>
    /// <exception cref="GraphContextException">Thrown when the flush fails.</exception>
    public Task<int> SaveChangesAsync(CancellationToken token = default) => services.ChangeTracker.FlushAsync(token);

    /// <inheritdoc/>
    public void Dispose()
    {
        DisposeAmbientTransaction();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAmbientTransactionAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the ambient transaction, if any, synchronously.
    /// </summary>
    private void DisposeAmbientTransaction()
    {
        services.TransactionFactory.Active?.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the ambient transaction, if any.
    /// </summary>
    /// <returns>Returns a task that represents the asynchronous dispose operation.</returns>
    private async ValueTask DisposeAmbientTransactionAsync()
    {
        IGraphTransaction? active = services.TransactionFactory.Active;
        if (active is null) return;
        await active.DisposeAsync().ConfigureAwait(false);
    }
}
