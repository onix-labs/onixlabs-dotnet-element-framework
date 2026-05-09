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

using System.Runtime.CompilerServices;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the typed accessor over nodes of a single registered type for a <see cref="GraphContext"/>.
/// </summary>
/// <remarks>
/// Mutation operations forward to the owning context's <see cref="IChangeTracker"/>; read operations route the per-type read statement through the provider's <see cref="IRawStatementExecutor"/> and project rows via the <see cref="IResultMaterializer"/>. <see cref="FindById"/> consults the identity map first and only queries the store on a miss.
/// </remarks>
/// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
/// <param name="model">The frozen <see cref="IGraphModel"/> used for property mapping.</param>
/// <param name="tracker">The owning context's <see cref="IChangeTracker"/> that mutations forward to.</param>
/// <param name="emitter">The provider's <see cref="IStatementEmitter"/>.</param>
/// <param name="executor">The provider's <see cref="IRawStatementExecutor"/>.</param>
/// <param name="materializer">The provider's <see cref="IResultMaterializer"/>.</param>
internal sealed class NodeSet<T>(
    IGraphModel model,
    IChangeTracker tracker,
    IStatementEmitter emitter,
    IRawStatementExecutor executor,
    IResultMaterializer materializer
) : INodeSet<T> where T : class
{
    /// <inheritdoc/>
    public IEnumerable<T> AsEnumerable()
    {
        DataStatement statement = emitter.EmitAsEnumerableNodes<T>(model);
        IEnumerable<IReadOnlyDictionary<string, object?>> rows = executor.Execute(statement.Statement, statement.Parameters);
        return rows.Select(row => materializer.MaterializeNode<T>(model, row, "n")).ToList();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> AsAsyncEnumerable([EnumeratorCancellation] CancellationToken token = default)
    {
        DataStatement statement = emitter.EmitAsEnumerableNodes<T>(model);
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows =
            executor.ExecuteAsync(statement.Statement, statement.Parameters, token);
        await foreach (IReadOnlyDictionary<string, object?> row in rows.ConfigureAwait(false))
            yield return materializer.MaterializeNode<T>(model, row, "n");
    }

    /// <inheritdoc/>
    public bool Exists(object id)
    {
        DataStatement statement = emitter.EmitExists<T>(model, id);
        IReadOnlyDictionary<string, object?> row = executor.Execute(statement.Statement, statement.Parameters).First();
        return row["count"] is long and > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(object id, CancellationToken token = default)
    {
        DataStatement statement = emitter.EmitExists<T>(model, id);
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows =
            executor.ExecuteAsync(statement.Statement, statement.Parameters, token);
        await foreach (IReadOnlyDictionary<string, object?> row in rows.ConfigureAwait(false))
            return row["count"] is long and > 0;
        return false;
    }

    /// <inheritdoc/>
    public T? FindById(object id)
    {
        T? tracked = tracker.Find<T>(id);
        if (tracked is not null) return tracked;

        DataStatement statement = emitter.EmitFindById<T>(model, id);
        IReadOnlyDictionary<string, object?>? row = executor.Execute(statement.Statement, statement.Parameters).FirstOrDefault();
        if (row is null) return null;

        T instance = materializer.MaterializeNode<T>(model, row, "n");
        tracker.Attach(instance);
        return instance;
    }

    /// <inheritdoc/>
    public async Task<T?> FindByIdAsync(object id, CancellationToken token = default)
    {
        T? tracked = tracker.Find<T>(id);
        if (tracked is not null) return tracked;

        DataStatement statement = emitter.EmitFindById<T>(model, id);
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows =
            executor.ExecuteAsync(statement.Statement, statement.Parameters, token);
        await foreach (IReadOnlyDictionary<string, object?> row in rows.ConfigureAwait(false))
        {
            T instance = materializer.MaterializeNode<T>(model, row, "n");
            tracker.Attach(instance);
            return instance;
        }

        return null;
    }

    /// <inheritdoc/>
    public void Add(T node) => tracker.TrackAdd(node);

    /// <inheritdoc/>
    public Task AddAsync(T node, CancellationToken token = default)
    {
        tracker.TrackAdd(node);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Update(T node) => tracker.TrackUpdate(node);

    /// <inheritdoc/>
    public Task UpdateAsync(T node, CancellationToken token = default)
    {
        tracker.TrackUpdate(node);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Remove(T node) => tracker.TrackRemove(node);

    /// <inheritdoc/>
    public Task RemoveAsync(T node, CancellationToken token = default)
    {
        tracker.TrackRemove(node);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Merge(T node) => tracker.TrackMerge(node);

    /// <inheritdoc/>
    public Task MergeAsync(T node, CancellationToken token = default)
    {
        tracker.TrackMerge(node);
        return Task.CompletedTask;
    }
}
