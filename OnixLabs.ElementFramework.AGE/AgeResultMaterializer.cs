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
/// Represents the Apache AGE implementation of <see cref="IResultMaterializer"/> that projects an agtype-encoded entity from a result row into a CLR node or edge instance.
/// </summary>
/// <remarks>
/// Result rows reach the materializer as <c>IReadOnlyDictionary&lt;string, object?&gt;</c> where each value is the raw agtype text string (Npgsql returns rows as strings when the executor sets <c>AllResultTypesAreUnknown</c>). The materializer parses the agtype via <see cref="AgtypeReader.ParseEntity"/>, then walks the registered <see cref="IPropertyMetadata"/> entries and assigns each converted value to the matching CLR property. Instance construction uses the type's parameterless constructor when available (compiled once and cached per type), falling back to <see cref="RuntimeHelpers.GetUninitializedObject(Type)"/>. Agtype-to-CLR conversions cover the inverse of <see cref="AgePropertySerializer"/>: <see cref="string"/> to <see cref="Guid"/>, ISO-8601 string to <see cref="DateTimeOffset"/>/<see cref="DateTime"/>, member-name string to <see cref="Enum"/>, <see cref="long"/> to narrower integer types via <see cref="Convert.ChangeType(object, Type)"/>.
/// </remarks>
internal sealed class AgeResultMaterializer : IResultMaterializer
{
    /// <summary>
    /// The alias the AGE emitter uses to project node entities in typed reads.
    /// </summary>
    internal const string NodeAlias = "n";

    /// <summary>
    /// The alias the AGE emitter uses to project edge entities in typed reads.
    /// </summary>
    internal const string EdgeAlias = "r";

    /// <summary>
    /// The alias the AGE emitter uses to project the existence count. Matches <see cref="AgeCypherEmitter.CountAlias"/> — named <c>cnt</c> rather than <c>count</c> because <c>count</c> is a reserved SQL keyword that AGE rejects as a column alias in the <c>AS (...)</c> schema.
    /// </summary>
    internal const string CountAlias = "cnt";

    /// <summary>
    /// Cache of compiled parameterless-constructor delegates keyed by CLR type.
    /// </summary>
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
        AgtypeEntity entity = ParseEntityAt(row, alias, "vertex");
        return Materialize<T>(model.GetNode(typeof(T)).Properties, entity.Properties);
    }

    /// <inheritdoc/>
    public T MaterializeEdgeAt<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias)
    {
        AgtypeEntity entity = ParseEntityAt(row, alias, "edge");
        return Materialize<T>(model.GetRelationship(typeof(T)).Properties, entity.Properties);
    }

    /// <inheritdoc/>
    public bool ReadExists(IReadOnlyDictionary<string, object?> row)
    {
        if (!row.TryGetValue(CountAlias, out object? value))
            throw new ResultMaterializationException($"Existence row does not contain the expected '{CountAlias}' alias.");
        if (value is not string text)
            throw new ResultMaterializationException($"Existence count is not a text-encoded agtype scalar; got {value?.GetType().FullName ?? "null"}.");
        return AgtypeReader.ParseScalar(text) is long count && count > 0;
    }

    /// <summary>
    /// Parses the agtype value at <paramref name="alias"/> as a vertex or edge entity and validates the kind tag.
    /// </summary>
    private static AgtypeEntity ParseEntityAt(IReadOnlyDictionary<string, object?> row, string alias, string expectedKind)
    {
        if (!row.TryGetValue(alias, out object? raw))
            throw new ResultMaterializationException($"Row does not contain the expected alias '{alias}'.");
        if (raw is not string text)
            throw new ResultMaterializationException(
                $"Expected an agtype string at alias '{alias}', got {raw?.GetType().FullName ?? "null"}.");

        AgtypeEntity entity = AgtypeReader.ParseEntity(text);
        if (!string.Equals(entity.Kind, expectedKind, StringComparison.Ordinal))
            throw new ResultMaterializationException(
                $"Expected agtype kind '{expectedKind}' at alias '{alias}', got '{entity.Kind}'.");
        return entity;
    }

    /// <summary>
    /// Constructs a CLR instance of <typeparamref name="T"/> and assigns each mapped property from <paramref name="source"/>.
    /// </summary>
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

    /// <summary>
    /// Builds a delegate that constructs an instance of <paramref name="type"/>, preferring its parameterless constructor and falling back to <see cref="RuntimeHelpers.GetUninitializedObject(Type)"/> when none exists.
    /// </summary>
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

    /// <summary>
    /// Converts an agtype-parsed <paramref name="value"/> (JSON scalar) to the CLR <paramref name="targetType"/>.
    /// </summary>
    private static object? Convert(object? value, Type targetType)
    {
        if (value is null) return null;

        Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(Guid) && value is string guidText) return Guid.Parse(guidText);
        if (underlying == typeof(DateTimeOffset) && value is string dtoText)
            return DateTimeOffset.Parse(dtoText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (underlying == typeof(DateTime) && value is string dtText)
            return DateTime.Parse(dtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (underlying.IsEnum && value is string enumText) return Enum.Parse(underlying, enumText);
        if (underlying == typeof(string)) return value as string;
        if (underlying.IsPrimitive && underlying != value.GetType())
            return System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return value;
    }
}
