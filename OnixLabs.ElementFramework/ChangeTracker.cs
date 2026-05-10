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
/// Represents a per-context change tracker that holds the identity map and ordered pending operations for a single <see cref="GraphContext"/>.
/// </summary>
/// <remarks>
/// Flush is atomic. When no consumer-owned ambient transaction is active, an ambient transaction is auto-opened for the flush duration, every staged operation runs through it, and the transaction is committed on full success or rolled back on the first failure. When a consumer-owned ambient transaction is already active, operations execute within that transaction and lifecycle remains the consumer's responsibility. Pending operations are cleared only on success; on failure the original snapshot is preserved so a corrected retry can replay the full batch.
/// </remarks>
internal sealed class ChangeTracker : IChangeTracker
{
    /// <summary>The frozen graph model for the owning context's CLR type.</summary>
    private readonly IGraphModel model;

    /// <summary>The provider's statement emitter.</summary>
    private readonly IStatementEmitter emitter;

    /// <summary>The provider's raw statement executor.</summary>
    private readonly IRawStatementExecutor executor;

    /// <summary>The provider's transaction opener used to detect or auto-open the ambient transaction that wraps a flush.</summary>
    private readonly IGraphTransactionOpener opener;

    /// <summary>The logger this tracker writes diagnostic events to.</summary>
    private readonly ILogger<ChangeTracker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeTracker"/> class.
    /// </summary>
    /// <param name="model">The frozen <see cref="IGraphModel"/> for the owning context's CLR type.</param>
    /// <param name="emitter">The provider's <see cref="IStatementEmitter"/>.</param>
    /// <param name="executor">The provider's <see cref="IRawStatementExecutor"/>.</param>
    /// <param name="opener">The provider's <see cref="IGraphTransactionOpener"/> used to detect or auto-open the ambient transaction that wraps a flush atomically.</param>
    /// <param name="logger">The typed logger this tracker writes diagnostic events to. Pass <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}.Instance"/> to disable logging.</param>
    internal ChangeTracker(
        IGraphModel model,
        IStatementEmitter emitter,
        IRawStatementExecutor executor,
        IGraphTransactionOpener opener,
        ILogger<ChangeTracker> logger)
    {
        this.model = model;
        this.emitter = emitter;
        this.executor = executor;
        this.opener = opener;
        this.logger = logger;
    }

    /// <summary>
    /// The identity map of tracked instances keyed by CLR type and key value.
    /// </summary>
    private readonly Dictionary<(Type Type, object Key), object> identityMap = [];

    /// <summary>
    /// The ordered list of pending statement-emission delegates awaiting the next flush.
    /// </summary>
    private readonly List<Func<IStatementEmitter, IGraphModel, DataStatement>> pending = [];

    /// <inheritdoc/>
    public int Flush()
    {
        if (pending.Count == 0) return 0;

        List<Func<IStatementEmitter, IGraphModel, DataStatement>> snapshot = [.. pending];
        logger.LogDebug("Flushing {OperationCount} pending operation(s).", snapshot.Count);

        if (opener.Active is not null)
        {
            int activeCount = ExecuteAll(snapshot);
            pending.Clear();
            logger.LogDebug("Flushed {OperationCount} operation(s) within an ambient transaction.", activeCount);
            return activeCount;
        }

        IGraphTransaction transaction = opener.Open();
        try
        {
            int count;
            try
            {
                count = ExecuteAll(snapshot);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Flush failed; rolling back auto-opened transaction. {OperationCount} operation(s) remain pending for retry.", snapshot.Count);
                transaction.Rollback();
                throw;
            }

            transaction.Commit();
            pending.Clear();
            logger.LogDebug("Flushed {OperationCount} operation(s) under auto-opened transaction.", count);
            return count;
        }
        finally
        {
            transaction.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<int> FlushAsync(CancellationToken token = default)
    {
        if (pending.Count == 0) return 0;

        List<Func<IStatementEmitter, IGraphModel, DataStatement>> snapshot = [.. pending];
        logger.LogDebug("Flushing {OperationCount} pending operation(s) (async).", snapshot.Count);

        if (opener.Active is not null)
        {
            int activeCount = await ExecuteAllAsync(snapshot, token).ConfigureAwait(false);
            pending.Clear();
            logger.LogDebug("Flushed {OperationCount} operation(s) within an ambient transaction (async).", activeCount);
            return activeCount;
        }

        IGraphTransaction transaction = await opener.OpenAsync(token).ConfigureAwait(false);
        try
        {
            int count;
            try
            {
                count = await ExecuteAllAsync(snapshot, token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Flush failed (async); rolling back auto-opened transaction. {OperationCount} operation(s) remain pending for retry.", snapshot.Count);
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                throw;
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            pending.Clear();
            logger.LogDebug("Flushed {OperationCount} operation(s) under auto-opened transaction (async).", count);
            return count;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void TrackAdd<T>(T node) where T : class => StageNode(node, (type, key, instance) =>
    {
        identityMap[(type, key)] = instance;
        pending.Add((em, m) => em.EmitAdd(m, (T)instance));
    });

    /// <inheritdoc/>
    public void TrackUpdate<T>(T node) where T : class => StageNode(node, (type, key, instance) =>
    {
        identityMap[(type, key)] = instance;
        pending.Add((em, m) => em.EmitUpdate(m, (T)instance));
    });

    /// <inheritdoc/>
    public void TrackRemove<T>(T node) where T : class => StageNode(node, (type, key, instance) =>
    {
        identityMap.Remove((type, key));
        pending.Add((em, m) => em.EmitRemove(m, (T)instance));
    });

    /// <inheritdoc/>
    public void TrackMerge<T>(T node) where T : class => StageNode(node, (type, key, instance) =>
    {
        identityMap[(type, key)] = instance;
        pending.Add((em, m) => em.EmitMerge(m, (T)instance));
    });

    /// <inheritdoc/>
    public void TrackConnect<TStart, TEdge, TEnd>(TStart start, TEdge edge, TEnd end)
        where TStart : class
        where TEdge : class
        where TEnd : class
    {
        _ = model.GetRelationship(typeof(TEdge));
        pending.Add((em, m) => em.EmitConnect(m, start, edge, end));
    }

    /// <inheritdoc/>
    public void TrackDisconnect<TStart, TEdge, TEnd>(TStart start, TEnd end)
        where TStart : class
        where TEdge : class
        where TEnd : class
    {
        _ = model.GetRelationship(typeof(TEdge));
        pending.Add((em, m) => em.EmitDisconnect<TStart, TEdge, TEnd>(m, start, end));
    }

    /// <inheritdoc/>
    public T? Find<T>(object key) where T : class =>
        identityMap.TryGetValue((typeof(T), key), out object? value) && value is T typed ? typed : null;

    /// <inheritdoc/>
    public void Attach<T>(T node) where T : class => StageNode(node, (type, key, instance) =>
    {
        if (identityMap.TryGetValue((type, key), out object? existing) && !ReferenceEquals(existing, instance))
            throw new GraphContextException(
                $"A different instance of {type.FullName} with key '{key}' is already tracked.");
        identityMap[(type, key)] = instance;
    });

    /// <inheritdoc/>
    public void Reset()
    {
        identityMap.Clear();
        pending.Clear();
    }

    /// <summary>
    /// Resolves the key metadata and key value for the supplied node, then invokes <paramref name="stage"/> with the resolved type, key, and instance.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node being staged.</typeparam>
    /// <param name="node">The node instance to stage.</param>
    /// <param name="stage">The action that performs the actual identity-map and pending-list update.</param>
    /// <exception cref="ModelConfigurationException">Thrown when the node type has no key configured.</exception>
    /// <exception cref="GraphContextException">Thrown when the node has a <see langword="null"/> key value.</exception>
    private void StageNode<T>(T node, Action<Type, object, object> stage) where T : class
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        if (metadata.Key is null)
            throw new ModelConfigurationException(
                $"Node type {typeof(T).FullName} has no key configured. Call builder.HasKey(...) in the node's configuration.");

        object? key = metadata.Key.Getter(node);
        if (key is null)
            throw new GraphContextException(
                $"Node of type {typeof(T).FullName} has a null key value on property '{metadata.Key.Name}'. Set the key before tracking.");

        stage(typeof(T), key, node);
    }

    /// <summary>
    /// Executes the supplied pending operations synchronously and returns the number of statements executed.
    /// </summary>
    /// <param name="ops">The pending operations to execute.</param>
    /// <returns>Returns the number of statements executed.</returns>
    private int ExecuteAll(IReadOnlyList<Func<IStatementEmitter, IGraphModel, DataStatement>> ops)
    {
        int count = 0;
        foreach (Func<IStatementEmitter, IGraphModel, DataStatement> emit in ops)
        {
            DataStatement statement = emit(emitter, model);
            _ = executor.Execute(statement.Statement, statement.Parameters);
            count++;
        }
        return count;
    }

    /// <summary>
    /// Executes the supplied pending operations asynchronously and returns the number of statements executed.
    /// </summary>
    /// <param name="ops">The pending operations to execute.</param>
    /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>Returns a task that resolves to the number of statements executed.</returns>
    private async Task<int> ExecuteAllAsync(IReadOnlyList<Func<IStatementEmitter, IGraphModel, DataStatement>> ops, CancellationToken token)
    {
        int count = 0;
        foreach (Func<IStatementEmitter, IGraphModel, DataStatement> emit in ops)
        {
            DataStatement statement = emit(emitter, model);
            IAsyncEnumerable<IReadOnlyDictionary<string, object?>> result =
                executor.ExecuteAsync(statement.Statement, statement.Parameters, token);
            // Drive enumeration so each staged async statement actually executes; row content is discarded.
            await foreach (IReadOnlyDictionary<string, object?> _ in result.ConfigureAwait(false))
            {
            }
            count++;
        }
        return count;
    }
}
