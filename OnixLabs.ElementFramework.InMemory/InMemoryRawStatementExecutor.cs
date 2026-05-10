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
/// Represents the in-memory provider's <see cref="IRawStatementExecutor"/> implementation.
/// </summary>
/// <remarks>
/// Interprets the op-coded <see cref="DataStatement"/> values produced by <see cref="InMemoryStatementEmitter"/>
/// against the active in-memory store. When an ambient transaction is open, all reads and writes route through the
/// transaction's cloned store; otherwise they target the canonical store directly.
/// </remarks>
/// <param name="opener">The opener whose ambient slot decides which store reads and writes target.</param>
internal sealed class InMemoryRawStatementExecutor(InMemoryGraphTransactionOpener opener) : IRawStatementExecutor
{
    /// <summary>
    /// The alias used to project node payloads into result rows. Paired with <see cref="InMemoryResultMaterializer.NodeAlias"/>.
    /// </summary>
    private const string NodeAlias = InMemoryResultMaterializer.NodeAlias;

    /// <summary>
    /// The alias used to project edge payloads into result rows. Paired with <see cref="InMemoryResultMaterializer.EdgeAlias"/>.
    /// </summary>
    private const string EdgeAlias = InMemoryResultMaterializer.EdgeAlias;

    /// <summary>
    /// The alias used to project existence counts into result rows. Paired with <see cref="InMemoryResultMaterializer.CountAlias"/>.
    /// </summary>
    private const string CountAlias = InMemoryResultMaterializer.CountAlias;

    /// <inheritdoc/>
    public IEnumerable<IReadOnlyDictionary<string, object?>> Execute(string statement, IReadOnlyDictionary<string, object?> parameters)
    {
        try
        {
            return RunStatement(statement, parameters).ToList();
        }
        catch (RawStatementException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new RawStatementException(
                $"Failed to execute the supplied in-memory statement '{statement}'.", exception);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        string statement,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        foreach (IReadOnlyDictionary<string, object?> row in Execute(statement, parameters))
        {
            token.ThrowIfCancellationRequested();
            yield return row;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the in-memory store reads and writes should target, honouring any ambient transaction.
    /// </summary>
    /// <value>The transaction-scoped clone when a transaction is active; otherwise the canonical store.</value>
    private InMemoryStore Target =>
        opener.Active is InMemoryGraphTransaction transaction ? transaction.Store : opener.Canonical;

    /// <summary>
    /// Dispatches the supplied op-coded statement against the in-memory store.
    /// </summary>
    /// <param name="statement">The op-code from <see cref="InMemoryStatementEmitter"/>.</param>
    /// <param name="parameters">The op-code's parameter bag.</param>
    /// <returns>Returns the rows produced by the statement.</returns>
    /// <exception cref="RawStatementException">Thrown when the op-code is unknown.</exception>
    private IEnumerable<IReadOnlyDictionary<string, object?>> RunStatement(string statement, IReadOnlyDictionary<string, object?> parameters)
    {
        InMemoryStore store = Target;
        return statement switch
        {
            InMemoryStatementEmitter.AddNode => RunUpsertNode(store, parameters),
            InMemoryStatementEmitter.UpdateNode => RunUpsertNode(store, parameters),
            InMemoryStatementEmitter.MergeNode => RunUpsertNode(store, parameters),
            InMemoryStatementEmitter.RemoveNode => RunRemoveNode(store, parameters),
            InMemoryStatementEmitter.AddEdge => RunAddEdge(store, parameters),
            InMemoryStatementEmitter.RemoveEdge => RunRemoveEdge(store, parameters),
            InMemoryStatementEmitter.FindById => RunFindById(store, parameters),
            InMemoryStatementEmitter.Exists => RunExists(store, parameters),
            InMemoryStatementEmitter.EnumerateNodes => RunEnumerateNodes(store, parameters),
            InMemoryStatementEmitter.EnumerateEdges => RunEnumerateEdges(store, parameters),
            _ => throw new RawStatementException(
                $"Unknown in-memory statement op-code '{statement}'. The in-memory provider only accepts statements emitted by InMemoryStatementEmitter.")
        };
    }

    /// <summary>
    /// Inserts or replaces the node carried by the supplied parameter bag.
    /// </summary>
    /// <param name="store">The store to mutate.</param>
    /// <param name="parameters">The parameter bag carrying the type, key, and instance.</param>
    /// <returns>Returns an empty row sequence.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunUpsertNode(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        Type type = ReadType(parameters, InMemoryStatementEmitter.TypeParameter);
        object key = ReadObject(parameters, InMemoryStatementEmitter.KeyParameter);
        object node = ReadObject(parameters, InMemoryStatementEmitter.NodeParameter);
        store.UpsertNode(type, key, node);
        return [];
    }

    /// <summary>
    /// Removes the node identified by the supplied parameter bag.
    /// </summary>
    /// <param name="store">The store to mutate.</param>
    /// <param name="parameters">The parameter bag carrying the type and key.</param>
    /// <returns>Returns an empty row sequence.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunRemoveNode(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        Type type = ReadType(parameters, InMemoryStatementEmitter.TypeParameter);
        object key = ReadObject(parameters, InMemoryStatementEmitter.KeyParameter);
        store.RemoveNode(type, key);
        return [];
    }

    /// <summary>
    /// Adds the edge carried by the supplied parameter bag.
    /// </summary>
    /// <param name="store">The store to mutate.</param>
    /// <param name="parameters">The parameter bag carrying the endpoint types, keys, and edge payload.</param>
    /// <returns>Returns an empty row sequence.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunAddEdge(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        InMemoryEdge edge = new(
            ReadType(parameters, InMemoryStatementEmitter.StartTypeParameter),
            ReadObject(parameters, InMemoryStatementEmitter.StartKeyParameter),
            ReadType(parameters, InMemoryStatementEmitter.EdgeTypeParameter),
            ReadObject(parameters, InMemoryStatementEmitter.EdgeParameter),
            ReadType(parameters, InMemoryStatementEmitter.EndTypeParameter),
            ReadObject(parameters, InMemoryStatementEmitter.EndKeyParameter));
        store.AddEdge(edge);
        return [];
    }

    /// <summary>
    /// Removes every edge that matches the endpoint and edge-type tuple in the supplied parameter bag.
    /// </summary>
    /// <param name="store">The store to mutate.</param>
    /// <param name="parameters">The parameter bag carrying the endpoint types, keys, and edge type.</param>
    /// <returns>Returns an empty row sequence.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunRemoveEdge(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        store.RemoveEdges(
            ReadType(parameters, InMemoryStatementEmitter.StartTypeParameter),
            ReadObject(parameters, InMemoryStatementEmitter.StartKeyParameter),
            ReadType(parameters, InMemoryStatementEmitter.EdgeTypeParameter),
            ReadType(parameters, InMemoryStatementEmitter.EndTypeParameter),
            ReadObject(parameters, InMemoryStatementEmitter.EndKeyParameter));
        return [];
    }

    /// <summary>
    /// Looks up a node by type and key, projecting it as a single-row sequence under the <c>"n"</c> alias.
    /// </summary>
    /// <param name="store">The store to read from.</param>
    /// <param name="parameters">The parameter bag carrying the type and key.</param>
    /// <returns>Returns one row when the node exists, or zero rows otherwise.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunFindById(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        Type type = ReadType(parameters, InMemoryStatementEmitter.TypeParameter);
        object key = ReadObject(parameters, InMemoryStatementEmitter.KeyParameter);
        object? node = store.FindNode(type, key);
        return node is null
            ? []
            : [new Dictionary<string, object?> { [NodeAlias] = node }];
    }

    /// <summary>
    /// Tests whether a node exists for the supplied type and key, returning a single row with <c>"count"</c>.
    /// </summary>
    /// <param name="store">The store to read from.</param>
    /// <param name="parameters">The parameter bag carrying the type and key.</param>
    /// <returns>Returns a single-row sequence whose <c>"count"</c> column is <c>1L</c> on hit and <c>0L</c> otherwise.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunExists(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        Type type = ReadType(parameters, InMemoryStatementEmitter.TypeParameter);
        object key = ReadObject(parameters, InMemoryStatementEmitter.KeyParameter);
        long count = store.FindNode(type, key) is null ? 0L : 1L;
        return [new Dictionary<string, object?> { [CountAlias] = count }];
    }

    /// <summary>
    /// Yields every node of the requested type, each projected under the <c>"n"</c> alias.
    /// </summary>
    /// <param name="store">The store to read from.</param>
    /// <param name="parameters">The parameter bag carrying the type.</param>
    /// <returns>Returns one row per matching node.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunEnumerateNodes(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        Type type = ReadType(parameters, InMemoryStatementEmitter.TypeParameter);
        return store.NodesOfType(type).Select(node => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { [NodeAlias] = node });
    }

    /// <summary>
    /// Yields every edge of the requested type, each projected under the <c>"r"</c> alias.
    /// </summary>
    /// <param name="store">The store to read from.</param>
    /// <param name="parameters">The parameter bag carrying the edge type.</param>
    /// <returns>Returns one row per matching edge.</returns>
    private static IEnumerable<IReadOnlyDictionary<string, object?>> RunEnumerateEdges(InMemoryStore store, IReadOnlyDictionary<string, object?> parameters)
    {
        Type type = ReadType(parameters, InMemoryStatementEmitter.TypeParameter);
        return store.EdgesOfType(type).Select(edge => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { [EdgeAlias] = edge.Edge });
    }

    /// <summary>
    /// Reads a required <see cref="Type"/> value out of the parameter bag.
    /// </summary>
    /// <param name="parameters">The parameter bag.</param>
    /// <param name="key">The parameter key.</param>
    /// <returns>Returns the requested type, never <see langword="null"/>.</returns>
    /// <exception cref="RawStatementException">Thrown when the parameter is missing or is not a <see cref="Type"/>.</exception>
    private static Type ReadType(IReadOnlyDictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out object? value) || value is not Type type)
            throw new RawStatementException($"Missing or invalid type parameter '{key}'.");
        return type;
    }

    /// <summary>
    /// Reads a required non-null object value out of the parameter bag.
    /// </summary>
    /// <param name="parameters">The parameter bag.</param>
    /// <param name="key">The parameter key.</param>
    /// <returns>Returns the parameter value, never <see langword="null"/>.</returns>
    /// <exception cref="RawStatementException">Thrown when the parameter is missing or <see langword="null"/>.</exception>
    private static object ReadObject(IReadOnlyDictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out object? value) || value is null)
            throw new RawStatementException($"Missing or null parameter '{key}'.");
        return value;
    }
}
