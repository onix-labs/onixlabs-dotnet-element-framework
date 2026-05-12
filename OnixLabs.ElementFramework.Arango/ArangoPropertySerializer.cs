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

using System.Globalization;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents a helper that serializes CLR property values into representations that fit ArangoDB's JSON document model.
/// </summary>
/// <remarks>
/// ArangoDB stores documents as JSON. Most CLR primitives map directly. Rich CLR types that have no JSON counterpart are flattened to canonical strings here so the inverse path in <see cref="ArangoResultMaterializer"/> can rehydrate them. Covered conversions: <see cref="Guid"/> to its hyphenated string form, <see cref="DateTimeOffset"/> to ISO 8601 round-trip ("o"), <see cref="DateTime"/> to ISO 8601 round-trip ("o"), <see cref="Enum"/> to its member name. Everything else is passed through unchanged so Newtonsoft.Json (ArangoDBNetStandard's default serializer) handles the wire encoding.
/// </remarks>
internal static class ArangoPropertySerializer
{
    /// <summary>
    /// Serializes a CLR property value into a JSON-friendly form.
    /// </summary>
    /// <param name="value">The CLR value, possibly null.</param>
    /// <returns>Returns the JSON-friendly representation, or <see langword="null"/> when the input was <see langword="null"/>.</returns>
    public static object? Serialize(object? value) => value switch
    {
        null => null,
        Guid guidValue => guidValue.ToString(),
        DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue.ToString("o", CultureInfo.InvariantCulture),
        DateTime dateTimeValue => dateTimeValue.ToString("o", CultureInfo.InvariantCulture),
        Enum enumValue => enumValue.ToString(),
        _ => value
    };

    /// <summary>
    /// Serializes a CLR key value into the string form ArangoDB requires for <c>_key</c>.
    /// </summary>
    /// <remarks>
    /// ArangoDB constrains <c>_key</c> to a specific character class; this method does not validate against it, but does ensure the value is a non-null string. Validation failures will surface from the server at write time as <see cref="RawStatementException"/>.
    /// </remarks>
    /// <param name="value">The CLR key value.</param>
    /// <returns>Returns the string form of the key suitable for use as <c>_key</c>.</returns>
    /// <exception cref="StatementEmissionException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string SerializeKey(object? value)
    {
        if (value is null)
            throw new StatementEmissionException("Cannot serialize a null key value; the node's key property must be set before tracking.");

        object serialized = Serialize(value)!; // Serialize only returns null for null input, which we rejected above.
        return serialized switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => serialized.ToString()!
        };
    }
}
