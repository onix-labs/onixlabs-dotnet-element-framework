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
using System.Text.Json;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Parses the text form of an <c>agtype</c> column that Apache AGE returns through Npgsql when reads run with <c>AllResultTypesAreUnknown</c>.
/// </summary>
/// <remarks>
/// AGE emits agtype values as JSON-like text, optionally suffixed with a kind tag — <c>::vertex</c>, <c>::edge</c>, or <c>::path</c>. Vertex bodies are <c>{"id": ..., "label": "...", "properties": {...}}</c> and edge bodies add <c>"start_id"</c>, <c>"end_id"</c>. Scalars come back as JSON literals: <c>"text"</c>, <c>42</c>, <c>3.14</c>, <c>true</c>, <c>null</c>. This reader strips the suffix when present and delegates to <see cref="JsonDocument"/> for the JSON body, then exposes the parsed shape via the <see cref="AgtypeEntity"/>/<see cref="AgtypeScalar"/> records consumed by <see cref="AgeResultMaterializer"/>.
/// </remarks>
internal static class AgtypeReader
{
    /// <summary>
    /// Parses an agtype text value as a vertex or edge entity, exposing its properties as a CLR map.
    /// </summary>
    /// <param name="text">The raw agtype text, e.g. <c>{"id": 1, "label": "Author", "properties": {...}}::vertex</c>.</param>
    /// <returns>Returns the parsed <see cref="AgtypeEntity"/>.</returns>
    /// <exception cref="ResultMaterializationException">Thrown when <paramref name="text"/> is not a vertex- or edge-shaped agtype literal.</exception>
    public static AgtypeEntity ParseEntity(string text)
    {
        string body = StripKindSuffix(text, out string? kind);
        if (kind is not "vertex" and not "edge")
            throw new ResultMaterializationException($"Expected an agtype vertex or edge literal; got '{text}'.");

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string label = root.GetProperty("label").GetString() ?? "";
            Dictionary<string, object?> properties = [];
            if (root.TryGetProperty("properties", out JsonElement props))
                foreach (JsonProperty property in props.EnumerateObject())
                    properties[property.Name] = ParseJsonScalar(property.Value);

            return new AgtypeEntity(kind, label, properties);
        }
        catch (JsonException exception)
        {
            throw new ResultMaterializationException($"Failed to parse agtype entity body: {body}", exception);
        }
    }

    /// <summary>
    /// Parses an agtype text value as a JSON scalar (string, number, boolean, or null).
    /// </summary>
    /// <param name="text">The raw agtype text, e.g. <c>"Alice"</c>, <c>42</c>, <c>true</c>, <c>null</c>.</param>
    /// <returns>Returns the parsed CLR value as <see cref="string"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/>, or <see langword="null"/>.</returns>
    /// <exception cref="ResultMaterializationException">Thrown when <paramref name="text"/> is not a recognized JSON scalar.</exception>
    public static object? ParseScalar(string text)
    {
        string body = StripKindSuffix(text, out _);
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return ParseJsonScalar(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new ResultMaterializationException($"Failed to parse agtype scalar: {body}", exception);
        }
    }

    /// <summary>
    /// Splits an agtype text into its JSON body and optional kind tag (<c>vertex</c>, <c>edge</c>, <c>path</c>, …).
    /// </summary>
    /// <param name="text">The raw agtype text.</param>
    /// <param name="kind">The kind tag without the leading <c>::</c>, or <see langword="null"/> when no tag is present.</param>
    /// <returns>Returns the JSON body suitable for <see cref="JsonDocument.Parse(string, JsonDocumentOptions)"/>.</returns>
    private static string StripKindSuffix(string text, out string? kind)
    {
        int last = text.LastIndexOf("::", StringComparison.Ordinal);
        if (last > 0 && AllAsciiLetters(text, last + 2))
        {
            kind = text[(last + 2)..];
            return text[..last];
        }
        kind = null;
        return text;
    }

    private static bool AllAsciiLetters(string text, int start)
    {
        if (start >= text.Length) return false;
        for (int i = start; i < text.Length; i++)
            if (!char.IsAsciiLetter(text[i])) return false;
        return true;
    }

    private static object? ParseJsonScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        // The cast on the long branch is load-bearing: without it the ternary's common type widens
        // long to double, so every integer agtype scalar (including count() results) would arrive
        // here as System.Double, which then fails the "scalar is long" pattern match in ReadExists.
        JsonValueKind.Number => element.TryGetInt64(out long longValue) ? (object)longValue : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw new ResultMaterializationException($"Unsupported agtype JSON value kind '{element.ValueKind}'. Only scalar properties are mapped in v1.")
    };
}

/// <summary>
/// Parsed shape of a vertex or edge agtype literal — the kind tag, its label, and the JSON-mapped property bag.
/// </summary>
/// <param name="Kind">The kind tag (<c>vertex</c> or <c>edge</c>).</param>
/// <param name="Label">The graph label (node label or relationship type).</param>
/// <param name="Properties">The property bag keyed by stored property name. Values are JSON scalars (<see cref="string"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/>) or <see langword="null"/>.</param>
internal sealed record AgtypeEntity(string Kind, string Label, IReadOnlyDictionary<string, object?> Properties);

/// <summary>
/// Parsed shape of a scalar agtype literal — the underlying CLR value.
/// </summary>
/// <param name="Value">The parsed CLR value (<see cref="string"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/>, or <see langword="null"/>).</param>
internal sealed record AgtypeScalar(object? Value);
