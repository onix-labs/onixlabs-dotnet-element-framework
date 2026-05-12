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

using Newtonsoft.Json.Linq;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Converts <see cref="JToken"/> trees coming back from AQL cursor responses into the framework's row shape — a tree of <see cref="Dictionary{TKey, TValue}"/>, <see cref="List{T}"/>, and primitive boxed CLR types.
/// </summary>
/// <remarks>
/// The framework's row contract is <c>IReadOnlyDictionary&lt;string, object?&gt;</c> with primitive-typed values for scalars and nested dictionaries/lists for structured values. ArangoDBNetStandard's default <c>JsonNetApiClientSerialization</c> hydrates cursor rows into <see cref="JObject"/> trees; this reader does the one-way conversion. Number handling: integers that fit a <see cref="long"/> are boxed as <see cref="long"/>, fractional or out-of-range numbers as <see cref="double"/>.
/// </remarks>
internal static class ArangoJsonReader
{
    /// <summary>
    /// Converts <paramref name="token"/> to the corresponding boxed CLR value.
    /// </summary>
    public static object? Read(JToken token) => token.Type switch
    {
        JTokenType.Object => ReadObject((JObject)token),
        JTokenType.Array => ReadArray((JArray)token),
        JTokenType.String => token.Value<string>(),
        JTokenType.Integer => token.Value<long>(),
        JTokenType.Float => token.Value<double>(),
        JTokenType.Boolean => token.Value<bool>(),
        JTokenType.Null or JTokenType.Undefined or JTokenType.None => null,
        JTokenType.Date => token.Value<DateTime>(),
        JTokenType.Guid => token.Value<Guid>(),
        JTokenType.TimeSpan => token.Value<TimeSpan>(),
        JTokenType.Uri => token.Value<string>(),
        _ => throw new ResultMaterializationException($"Unexpected JSON kind '{token.Type}' in AQL result row.")
    };

    /// <summary>
    /// Converts the top-level <paramref name="row"/> object into a <see cref="Dictionary{TKey,TValue}"/> keyed by JSON property name.
    /// </summary>
    public static Dictionary<string, object?> ReadRow(JObject row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return ReadObject(row);
    }

    private static Dictionary<string, object?> ReadObject(JObject element)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JToken?> property in element)
            result[property.Key] = property.Value is null ? null : Read(property.Value);
        return result;
    }

    private static List<object?> ReadArray(JArray array)
    {
        List<object?> result = [];
        foreach (JToken item in array)
            result.Add(Read(item));
        return result;
    }
}
