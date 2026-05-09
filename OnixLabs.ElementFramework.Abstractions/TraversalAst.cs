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
/// Represents a provider-agnostic description of a graph traversal constructed by the fluent builder.
/// </summary>
/// <param name="Kind">The kind of traversal — match, merge, or create.</param>
/// <param name="Segments">The linear sequence of pattern segments, beginning with a node and alternating relationship/node thereafter.</param>
/// <param name="Predicates">The property-equality predicates accumulated from Where clauses.</param>
/// <param name="ReturnAlias">The alias of the bound variable returned by the traversal.</param>
public sealed record TraversalAst(
    TraversalKind Kind,
    IReadOnlyList<PatternSegment> Segments,
    IReadOnlyList<TraversalPredicate> Predicates,
    string ReturnAlias
);
