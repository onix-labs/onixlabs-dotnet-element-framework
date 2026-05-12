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
/// Represents a null check over a bound property. Carried as a distinct predicate so emitters can produce
/// the SQL/Cypher idiomatic <c>IS NULL</c> form rather than <c>= NULL</c>.
/// </summary>
/// <param name="Alias">The alias of the bound variable to which the predicate applies.</param>
/// <param name="ClrPropertyName">The name of the CLR property whose null state is being tested.</param>
/// <param name="IsNull">When <see langword="true"/> the predicate matches null values; otherwise non-null values.</param>
public sealed record NullPredicate(
    string Alias,
    string ClrPropertyName,
    bool IsNull
) : TraversalPredicate;
