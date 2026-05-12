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
/// Represents a helper that escapes Cypher identifiers, wrapping them in backticks when they are not valid bare identifiers or collide with an openCypher reserved word.
/// </summary>
/// <remarks>
/// The reserved-word list mirrors the Cypher 5 reference and is the same set the Neo4j provider tracks; Apache AGE accepts the same syntax inside the <c>cypher()</c> envelope. Comparison is case-insensitive because Cypher reserved words are case-insensitive in the parser. Backticks inside an identifier are escaped by doubling per the Cypher quoting rules.
/// </remarks>
internal static class CypherIdentifier
{
    /// <summary>
    /// The set of Cypher reserved words, compared case-insensitively.
    /// </summary>
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
    /// <returns>Returns the Cypher-safe identifier text.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> is null, empty, or whitespace.</exception>
    public static string Escape(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return IsBareIdentifier(identifier) && !ReservedWords.Contains(identifier)
            ? identifier
            : $"`{identifier.Replace("`", "``")}`";
    }

    /// <summary>
    /// Determines whether the supplied <paramref name="identifier"/> is a bare Cypher identifier (ASCII letter or underscore, followed by ASCII letters, digits, or underscores).
    /// </summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <returns>Returns <see langword="true"/> when <paramref name="identifier"/> is a bare identifier; otherwise, <see langword="false"/>.</returns>
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
