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

namespace OnixLabs.ElementFramework.AGE.UnitTests;

public class AgePropertySerializerTests
{
    [Fact(DisplayName = "Serialize returns null for a null input")]
    public void SerializeReturnsNullForNullInput()
    {
        Assert.Null(AgePropertySerializer.Serialize(null));
    }

    [Fact(DisplayName = "Serialize converts a Guid to its hyphenated string form")]
    public void SerializeConvertsGuidToHyphenatedString()
    {
        Guid guid = Guid.Parse("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a");

        object? result = AgePropertySerializer.Serialize(guid);

        Assert.Equal("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a", result);
    }

    [Fact(DisplayName = "Serialize converts a DateTimeOffset to its ISO 8601 round-trip form")]
    public void SerializeConvertsDateTimeOffsetToRoundTripString()
    {
        DateTimeOffset value = new(2026, 5, 11, 13, 30, 0, TimeSpan.FromHours(2));

        object? result = AgePropertySerializer.Serialize(value);

        Assert.Equal(value.ToString("o", CultureInfo.InvariantCulture), result);
        Assert.IsType<string>(result);
    }

    [Fact(DisplayName = "Serialize converts a DateTime to its ISO 8601 round-trip form, preserving Kind")]
    public void SerializeConvertsDateTimeToRoundTripString()
    {
        DateTime utc = new(2026, 5, 11, 13, 30, 0, DateTimeKind.Utc);

        object? result = AgePropertySerializer.Serialize(utc);

        Assert.Equal(utc.ToString("o", CultureInfo.InvariantCulture), result);
        Assert.EndsWith("Z", (string)result!);
    }

    [Fact(DisplayName = "Serialize converts an enum value to its member name")]
    public void SerializeConvertsEnumToMemberName()
    {
        object? result = AgePropertySerializer.Serialize(SampleStatus.Submitted);

        Assert.Equal("Submitted", result);
    }

    [Theory(DisplayName = "Serialize passes through JSON-friendly primitives unchanged")]
    [InlineData(42)]
    [InlineData(42L)]
    [InlineData(3.14)]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData("text")]
    public void SerializePassesThroughPrimitives(object input)
    {
        Assert.Equal(input, AgePropertySerializer.Serialize(input));
    }
}
