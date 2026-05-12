// MIT License

// Copyright (c) 2020 ONIXLabs

// Copyright notice trimmed for brevity above — applies to this file too.

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

using System.Text.Json;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Serializes a flat parameter dictionary into the agtype JSON object that AGE's <c>cypher()</c> third argument accepts.
/// </summary>
/// <remarks>
/// Values are expected to already have been passed through <see cref="AgePropertySerializer.Serialize"/> so every entry is a JSON-friendly primitive (string, integer, double, boolean, or null). The output is a single-line JSON object suitable for sending as a text parameter with <see cref="NpgsqlTypes.NpgsqlDbType.Unknown"/>.
/// </remarks>
internal static class AgtypeWriter
{
    /// <summary>
    /// Serializes <paramref name="parameters"/> as an agtype JSON object.
    /// </summary>
    /// <param name="parameters">The parameter dictionary whose values are already JSON-friendly primitives.</param>
    /// <returns>Returns the JSON text representation of the agtype object.</returns>
    public static string Serialize(IReadOnlyDictionary<string, object?> parameters)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object?> entry in parameters)
            {
                writer.WritePropertyName(entry.Key);
                WriteValue(writer, entry.Value);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string s: writer.WriteStringValue(s); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case byte v: writer.WriteNumberValue(v); break;
            case sbyte v: writer.WriteNumberValue(v); break;
            case short v: writer.WriteNumberValue(v); break;
            case ushort v: writer.WriteNumberValue(v); break;
            case int v: writer.WriteNumberValue(v); break;
            case uint v: writer.WriteNumberValue(v); break;
            case long v: writer.WriteNumberValue(v); break;
            case ulong v: writer.WriteNumberValue(v); break;
            case float v: writer.WriteNumberValue(v); break;
            case double v: writer.WriteNumberValue(v); break;
            case decimal v: writer.WriteNumberValue(v); break;
            default:
                throw new StatementEmissionException(
                    $"AGE parameter values must be JSON-friendly primitives after AgePropertySerializer.Serialize. Got '{value.GetType().FullName}'.");
        }
    }
}
