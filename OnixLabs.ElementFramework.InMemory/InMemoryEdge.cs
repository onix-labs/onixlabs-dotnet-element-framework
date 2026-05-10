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
/// Represents an edge tuple stored in the <see cref="InMemoryStore"/>.
/// </summary>
/// <param name="StartType">The CLR type of the start node.</param>
/// <param name="StartKey">The configured key of the start node.</param>
/// <param name="EdgeType">The CLR type of the edge.</param>
/// <param name="Edge">The edge instance carrying the relationship's payload.</param>
/// <param name="EndType">The CLR type of the end node.</param>
/// <param name="EndKey">The configured key of the end node.</param>
internal sealed record InMemoryEdge(
    Type StartType,
    object StartKey,
    Type EdgeType,
    object Edge,
    Type EndType,
    object EndKey);
