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

using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the ArangoDB implementation of <see cref="IResultMaterializer"/> that projects a JSON document from a result row into a CLR node or edge instance.
/// </summary>
/// <remarks>
/// Result rows reach the materializer as <c>IReadOnlyDictionary&lt;string, object?&gt;</c> where each value is either a nested dictionary (for objects), a list (for arrays), or a boxed primitive. The framework's typed reads project documents under an internal alias — <c>"n"</c> for nodes, <c>"r"</c> for edges, <c>"cnt"</c> for the existence count — and this materializer reads from those aliases. The alias-bearing methods accept a consumer-supplied alias for traversal returns. Conversions from JSON-shaped values to CLR types cover the inverse of the emitter's property serialization: <see cref="string"/> to <see cref="Guid"/>, ISO-8601 string to <see cref="DateTimeOffset"/>/<see cref="DateTime"/>, member-name string to <see cref="Enum"/>, and primitive widening/narrowing via <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> in the invariant culture.
/// </remarks>
internal sealed class ArangoResultMaterializer : IResultMaterializer
{
    /// <summary>
    /// The alias the Arango emitter uses to project node entities in typed reads.
    /// </summary>
    internal const string NodeAlias = "n";

    /// <summary>
    /// The alias the Arango emitter uses to project edge entities in typed reads.
    /// </summary>
    internal const string EdgeAlias = "r";

    /// <summary>
    /// The alias the Arango emitter uses to project the existence count.
    /// </summary>
    internal const string CountAlias = "cnt";

    private readonly ConcurrentDictionary<Type, Func<object>> instantiators = new();

    /// <inheritdoc/>
    public T MaterializeNode<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row) =>
        MaterializeNodeAt<T>(model, row, NodeAlias);

    /// <inheritdoc/>
    public T MaterializeEdge<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row) =>
        MaterializeEdgeAt<T>(model, row, EdgeAlias);

    /// <inheritdoc/>
    public T MaterializeNodeAt<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias)
    {
        IReadOnlyDictionary<string, object?> source = ReadDocumentAt(row, alias);
        return Materialize<T>(model.GetNode(typeof(T)).Properties, source);
    }

    /// <inheritdoc/>
    public T MaterializeEdgeAt<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias)
    {
        IReadOnlyDictionary<string, object?> source = ReadDocumentAt(row, alias);
        return Materialize<T>(model.GetRelationship(typeof(T)).Properties, source);
    }

    /// <inheritdoc/>
    public bool ReadExists(IReadOnlyDictionary<string, object?> row)
    {
        if (!row.TryGetValue(CountAlias, out object? value))
            throw new ResultMaterializationException($"Existence row does not contain the expected '{CountAlias}' alias.");
        return value is long count && count > 0;
    }

    private static IReadOnlyDictionary<string, object?> ReadDocumentAt(IReadOnlyDictionary<string, object?> row, string alias)
    {
        if (!row.TryGetValue(alias, out object? raw))
            throw new ResultMaterializationException($"Row does not contain the expected alias '{alias}'.");
        if (raw is not IReadOnlyDictionary<string, object?> document)
            throw new ResultMaterializationException(
                $"Expected a JSON object at alias '{alias}', got {raw?.GetType().FullName ?? "null"}.");
        return document;
    }

    private T Materialize<T>(IReadOnlyList<IPropertyMetadata> properties, IReadOnlyDictionary<string, object?> source)
    {
        T instance = (T)instantiators.GetOrAdd(typeof(T), BuildInstantiator)();

        foreach (IPropertyMetadata property in properties)
        {
            if (!source.TryGetValue(property.Name, out object? raw)) continue;
            object? converted = Convert(raw, property.Property.PropertyType);
            property.Setter(instance!, converted);
        }

        return instance;
    }

    private static Func<object> BuildInstantiator(Type type)
    {
        ConstructorInfo? parameterless = type.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        return parameterless is not null
            ? Expression.Lambda<Func<object>>(Expression.New(parameterless)).Compile()
            : () => RuntimeHelpers.GetUninitializedObject(type);
    }

    private static object? Convert(object? value, Type targetType)
    {
        if (value is null) return null;

        Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(Guid) && value is string guidText) return Guid.Parse(guidText);
        if (underlying == typeof(DateTimeOffset))
        {
            // Newtonsoft.Json autodetects ISO-8601 strings into DateTime; we may also receive a raw string.
            if (value is string dtoText) return DateTimeOffset.Parse(dtoText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (value is DateTime dtoFromDate) return new DateTimeOffset(DateTime.SpecifyKind(dtoFromDate, DateTimeKind.Utc));
        }
        if (underlying == typeof(DateTime))
        {
            if (value is string dtText) return DateTime.Parse(dtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (value is DateTime dtPassthrough) return dtPassthrough;
        }
        if (underlying.IsEnum && value is string enumText) return Enum.Parse(underlying, enumText);
        if (underlying == typeof(string)) return value as string;
        if (underlying.IsPrimitive && underlying != value.GetType())
            return System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return value;
    }
}
