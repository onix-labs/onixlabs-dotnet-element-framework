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

using Neo4j.Driver;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Provides serialization of CLR property values into Bolt-friendly representations that the Neo4j driver accepts as bound parameter values.
/// </summary>
/// <remarks>
/// Conversions covered in v1: <see cref="Guid"/> to <see cref="string"/> (canonical hyphenated form), <see cref="DateTimeOffset"/> to <see cref="ZonedDateTime"/> (Bolt's offset-aware temporal), <see cref="Enum"/> to its member name. Anything else (primitives, <see cref="string"/>, <see cref="DateTime"/>, byte arrays, lists, maps) is returned unchanged and the driver's PackStream encoder handles it.
/// </remarks>
internal static class PropertySerializer
{
    /// <summary>
    /// Serializes a CLR property value into a form the Neo4j driver can bind.
    /// </summary>
    /// <param name="value">The CLR value, possibly null.</param>
    /// <returns>The Bolt-friendly representation, or <see langword="null"/> when the input was null.</returns>
    public static object? Serialize(object? value) => value switch
    {
        null => null,
        Guid guidValue => guidValue.ToString(),
        DateTimeOffset dateTimeOffsetValue => new ZonedDateTime(dateTimeOffsetValue),
        Enum enumValue => enumValue.ToString(),
        _ => value
    };
}
