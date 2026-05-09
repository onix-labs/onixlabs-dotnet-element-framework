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
/// Provides Cypher identifier escaping that wraps an identifier in backticks when it is not a valid bare identifier or collides with a Cypher 5 reserved word.
/// </summary>
/// <remarks>
/// Reserved-word list mirrors Neo4j's published Cypher 5 reserved-word reference. Comparison is case-insensitive because Cypher reserved words are case-insensitive in the parser. Backticks inside an identifier are escaped by doubling per the Cypher quoting rules.
/// </remarks>
internal static class CypherIdentifier
{
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL", "AND", "ANY", "AS", "ASC", "ASCENDING", "BY", "CALL", "CASE", "CONSTRAINT", "CONTAINS",
        "CREATE", "DELETE", "DESC", "DESCENDING", "DETACH", "DISTINCT", "DROP", "ELSE", "END", "ENDS",
        "EXISTS", "FALSE", "FIELDTERMINATOR", "FOR", "FOREACH", "FROM", "GRANT", "IF", "IN", "INDEX",
        "INFINITY", "IS", "JOIN", "LIMIT", "LOAD", "MANDATORY", "MATCH", "MERGE", "NAN", "NONE", "NOT",
        "NULL", "OF", "ON", "OPTIONAL", "OR", "ORDER", "REDUCE", "REL", "RELATIONSHIP", "REMOVE", "REQUIRE",
        "RETURN", "REVOKE", "SCAN", "SET", "SHOW", "SINGLE", "SKIP", "START", "STARTS", "THEN", "TO",
        "TRUE", "UNION", "UNIQUE", "UNWIND", "USE", "USING", "WHEN", "WHERE", "WITH", "XOR", "YIELD"
    };

    /// <summary>
    /// Returns the supplied identifier formatted for safe inclusion in a Cypher query. Bare alphanumeric identifiers that are not reserved words are returned unchanged; everything else is wrapped in backticks (and any embedded backtick is doubled).
    /// </summary>
    /// <param name="identifier">The CLR-side label or property name to escape.</param>
    /// <returns>The Cypher-safe identifier text.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> is null, empty, or whitespace.</exception>
    public static string Escape(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return IsBareIdentifier(identifier) && !ReservedWords.Contains(identifier)
            ? identifier
            : $"`{identifier.Replace("`", "``")}`";
    }

    private static bool IsBareIdentifier(string identifier)
    {
        char first = identifier[0];
        if (!char.IsAsciiLetter(first) && first != '_') return false;
        for (int i = 1; i < identifier.Length; i++)
        {
            char c = identifier[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '_') return false;
        }
        return true;
    }
}
