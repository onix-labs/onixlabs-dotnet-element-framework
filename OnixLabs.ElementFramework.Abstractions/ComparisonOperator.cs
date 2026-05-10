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
/// Enumerates the binary comparison operators carried by a <see cref="PropertyComparisonPredicate"/>.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>Property is equal to value.</summary>
    Equal,

    /// <summary>Property is not equal to value.</summary>
    NotEqual,

    /// <summary>Property is strictly less than value.</summary>
    LessThan,

    /// <summary>Property is less than or equal to value.</summary>
    LessThanOrEqual,

    /// <summary>Property is strictly greater than value.</summary>
    GreaterThan,

    /// <summary>Property is greater than or equal to value.</summary>
    GreaterThanOrEqual
}
