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
/// Represents an accumulator of openCypher query parameters with collision-free naming for the AGE provider.
/// </summary>
/// <remarks>
/// AGE's <c>cypher()</c> envelope accepts a single <c>agtype</c> parameter; every bound value contributes a field on the object that AGE unpacks as inner <c>$name</c> references. Each <see cref="Bind"/> call returns the openCypher token (with leading <c>$</c>) ready to inline into the Cypher body; <see cref="ToParameters"/> snapshots the accumulated fields for <see cref="AgtypeWriter"/> to serialize. <see cref="AgeCypherEmitter"/> creates a fresh binder for every emission so state never leaks across queries. Collisions on the preferred name resolve via numeric suffix (<c>$Body</c>, <c>$Body_1</c>, <c>$Body_2</c>, …) and suffixes are stable within a single binder instance so emitted output is deterministic for unit-test assertions.
/// </remarks>
internal sealed class AgeParameterBinder
{
    /// <summary>
    /// The accumulated parameter bindings keyed by their final, collision-resolved name.
    /// </summary>
    private readonly Dictionary<string, object?> parameters = [];

    /// <summary>
    /// Tracks the highest assigned numeric suffix for each preferred parameter name to keep collision resolution deterministic.
    /// </summary>
    private readonly Dictionary<string, int> nameCounts = [];

    /// <summary>
    /// Binds the supplied value to a parameter and returns the openCypher token (with leading <c>$</c>) to inline into the query.
    /// </summary>
    /// <param name="preferredName">The starting parameter name (typically the property name, no <c>$</c>).</param>
    /// <param name="value">The bound value, already serialized by <see cref="AgePropertySerializer"/> if needed.</param>
    /// <returns>Returns the openCypher parameter token, e.g. <c>$Body</c> or <c>$Body_1</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="preferredName"/> is null, empty, or whitespace.</exception>
    public string Bind(string preferredName, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredName);

        string name = preferredName;

        if (parameters.ContainsKey(name))
        {
            int suffix = nameCounts.GetValueOrDefault(preferredName, 0) + 1;
            while (parameters.ContainsKey($"{preferredName}_{suffix}")) suffix++;
            name = $"{preferredName}_{suffix}";
            nameCounts[preferredName] = suffix;
        }

        parameters[name] = value;
        return $"${name}";
    }

    /// <summary>
    /// Snapshots the accumulated parameter bindings as the read-only dictionary that <see cref="DataStatement.Parameters"/> exposes.
    /// </summary>
    /// <returns>Returns the accumulated parameter bindings.</returns>
    public IReadOnlyDictionary<string, object?> ToParameters() => parameters;
}
