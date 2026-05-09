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
/// Defines the entry point for the fluent graph pattern builder.
/// </summary>
public interface IGraphTraversal
{
    /// <summary>
    /// Begins a match pattern.
    /// </summary>
    /// <returns>A new <see cref="IPatternStart"/> that the caller can extend with the pattern shape.</returns>
    IPatternStart Match();

    /// <summary>
    /// Begins a merge pattern — match if the pattern exists, create otherwise.
    /// </summary>
    /// <returns>A new <see cref="IPatternStart"/> that the caller can extend with the pattern shape.</returns>
    IPatternStart Merge();

    /// <summary>
    /// Begins a create pattern.
    /// </summary>
    /// <returns>A new <see cref="IPatternStart"/> that the caller can extend with the pattern shape.</returns>
    IPatternStart Create();
}
