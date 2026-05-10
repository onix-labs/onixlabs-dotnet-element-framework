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
/// Represents the in-memory provider's <see cref="IResultMaterializer"/> implementation.
/// </summary>
/// <remarks>
/// In-memory rows already carry the live CLR instance under the requested alias, so materialization is just a typed
/// cast. The framework returns the very same instance the consumer stored, which preserves reference identity across
/// reads — handy for tests asserting on shared mutable state. The <see cref="NodeAlias"/>, <see cref="EdgeAlias"/>, and
/// <see cref="CountAlias"/> constants are the in-memory provider's internal convention for typed-read row keys; they
/// are paired with the same constants in <see cref="InMemoryRawStatementExecutor"/>.
/// </remarks>
internal sealed class InMemoryResultMaterializer : IResultMaterializer
{
    /// <summary>
    /// The row key under which the in-memory executor projects node entities for typed reads.
    /// </summary>
    internal const string NodeAlias = "n";

    /// <summary>
    /// The row key under which the in-memory executor projects edge entities for typed reads.
    /// </summary>
    internal const string EdgeAlias = "r";

    /// <summary>
    /// The row key under which the in-memory executor projects the existence count.
    /// </summary>
    internal const string CountAlias = "count";

    /// <inheritdoc/>
    public T MaterializeNode<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row) => Cast<T>(row, NodeAlias);

    /// <inheritdoc/>
    public T MaterializeEdge<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row) => Cast<T>(row, EdgeAlias);

    /// <inheritdoc/>
    public T MaterializeNodeAt<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) => Cast<T>(row, alias);

    /// <inheritdoc/>
    public T MaterializeEdgeAt<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) => Cast<T>(row, alias);

    /// <inheritdoc/>
    public bool ReadExists(IReadOnlyDictionary<string, object?> row)
    {
        if (!row.TryGetValue(CountAlias, out object? value))
            throw new ResultMaterializationException($"Existence row does not contain the expected '{CountAlias}' alias.");
        return value is long count && count > 0;
    }

    /// <summary>
    /// Casts the value bound at <paramref name="alias"/> in <paramref name="row"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target CLR type.</typeparam>
    /// <param name="row">The result row.</param>
    /// <param name="alias">The alias whose value should be cast.</param>
    /// <returns>Returns the live CLR instance bound at <paramref name="alias"/>.</returns>
    /// <exception cref="ResultMaterializationException">Thrown when the alias is missing or the bound value is not assignable to <typeparamref name="T"/>.</exception>
    private static T Cast<T>(IReadOnlyDictionary<string, object?> row, string alias)
    {
        if (!row.TryGetValue(alias, out object? value))
            throw new ResultMaterializationException($"Row does not contain a value at alias '{alias}'.");
        if (value is not T typed)
            throw new ResultMaterializationException(
                $"Value at alias '{alias}' is not assignable to {typeof(T).FullName}; got {value?.GetType().FullName ?? "null"}.");
        return typed;
    }
}
