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
/// Represents the typed accessor over edges of a single registered type for a <see cref="GraphContext"/>.
/// </summary>
/// <remarks>
/// Mutation operations forward to the owning context's <see cref="IChangeTracker"/>; read operations route the per-type read statement through the provider's <see cref="IRawStatementExecutor"/> and project rows via the <see cref="IResultMaterializer"/>.
/// </remarks>
/// <typeparam name="T">The edge type. Must be a registered edge in the model.</typeparam>
/// <param name="model">The frozen <see cref="IGraphModel"/> used for property mapping.</param>
/// <param name="tracker">The owning context's <see cref="IChangeTracker"/> that mutations forward to.</param>
/// <param name="emitter">The provider's <see cref="IStatementEmitter"/>.</param>
/// <param name="executor">The provider's <see cref="IRawStatementExecutor"/>.</param>
/// <param name="materializer">The provider's <see cref="IResultMaterializer"/>.</param>
internal sealed class EdgeSet<T>(
    IGraphModel model,
    IChangeTracker tracker,
    IStatementEmitter emitter,
    IRawStatementExecutor executor,
    IResultMaterializer materializer
) : IEdgeSet<T> where T : class
{
    /// <inheritdoc/>
    public IEnumerable<T> AsEnumerable()
    {
        DataStatement statement = emitter.EmitAsEnumerableEdges<T>(model);
        IEnumerable<IReadOnlyDictionary<string, object?>> rows = executor.Execute(statement.Statement, statement.Parameters);
        return rows.Select(row => materializer.MaterializeEdge<T>(model, row, "r")).ToList();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> AsAsyncEnumerable([EnumeratorCancellation] CancellationToken token = default)
    {
        DataStatement statement = emitter.EmitAsEnumerableEdges<T>(model);
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows =
            executor.ExecuteAsync(statement.Statement, statement.Parameters, token);
        await foreach (IReadOnlyDictionary<string, object?> row in rows.ConfigureAwait(false))
            yield return materializer.MaterializeEdge<T>(model, row, "r");
    }

    /// <inheritdoc/>
    public void Connect<TStart, TEnd>(TStart start, T edge, TEnd end) where TStart : class where TEnd : class =>
        tracker.TrackConnect(start, edge, end);

    /// <inheritdoc/>
    public Task ConnectAsync<TStart, TEnd>(TStart start, T edge, TEnd end, CancellationToken token = default) where TStart : class where TEnd : class
    {
        tracker.TrackConnect(start, edge, end);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Disconnect<TStart, TEnd>(TStart start, TEnd end) where TStart : class where TEnd : class =>
        tracker.TrackDisconnect<TStart, T, TEnd>(start, end);

    /// <inheritdoc/>
    public Task DisconnectAsync<TStart, TEnd>(TStart start, TEnd end, CancellationToken token = default) where TStart : class where TEnd : class
    {
        tracker.TrackDisconnect<TStart, T, TEnd>(start, end);
        return Task.CompletedTask;
    }
}
