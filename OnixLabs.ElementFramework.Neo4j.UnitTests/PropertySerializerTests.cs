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

namespace OnixLabs.ElementFramework.Neo4j.UnitTests;

public class PropertySerializerTests
{
    [Fact(DisplayName = "Serialize returns null for a null input")]
    public void SerializeReturnsNullForNull()
    {
        Assert.Null(PropertySerializer.Serialize(null));
    }

    [Fact(DisplayName = "Serialize converts a Guid into its canonical hyphenated string form")]
    public void SerializeConvertsGuidToString()
    {
        Guid value = new("11111111-2222-3333-4444-555555555555");
        Assert.Equal("11111111-2222-3333-4444-555555555555", PropertySerializer.Serialize(value));
    }

    [Fact(DisplayName = "Serialize converts an empty Guid into the zero-Guid string")]
    public void SerializeConvertsEmptyGuid()
    {
        Assert.Equal("00000000-0000-0000-0000-000000000000", PropertySerializer.Serialize(Guid.Empty));
    }

    [Fact(DisplayName = "Serialize converts a DateTimeOffset into a Bolt ZonedDateTime preserving the offset")]
    public void SerializeConvertsDateTimeOffsetToZonedDateTime()
    {
        DateTimeOffset value = new(2026, 5, 11, 13, 30, 0, TimeSpan.FromHours(2));
        object? serialized = PropertySerializer.Serialize(value);

        ZonedDateTime zoned = Assert.IsType<ZonedDateTime>(serialized);
        DateTimeOffset roundTripped = zoned.ToDateTimeOffset();
        Assert.Equal(value, roundTripped);
    }

    [Fact(DisplayName = "Serialize converts an enum value into its declared member name")]
    public void SerializeConvertsEnumToName()
    {
        Assert.Equal(nameof(SampleStatus.Submitted), PropertySerializer.Serialize(SampleStatus.Submitted));
    }

    [Theory(DisplayName = "Serialize returns primitive and string values unchanged")]
    [InlineData(42)]
    [InlineData(3.14)]
    [InlineData("hello")]
    [InlineData(true)]
    [InlineData(false)]
    public void SerializeReturnsPrimitivesUnchanged(object value)
    {
        Assert.Equal(value, PropertySerializer.Serialize(value));
    }

    [Fact(DisplayName = "Serialize returns a DateTime unchanged (only DateTimeOffset is converted)")]
    public void SerializeReturnsDateTimeUnchanged()
    {
        DateTime value = new(2026, 5, 11, 13, 30, 0, DateTimeKind.Utc);
        Assert.Equal(value, PropertySerializer.Serialize(value));
    }

    [Fact(DisplayName = "Serialize returns a byte array unchanged")]
    public void SerializeReturnsByteArrayUnchanged()
    {
        byte[] value = [1, 2, 3, 4];
        Assert.Same(value, PropertySerializer.Serialize(value));
    }

    private enum SampleStatus
    {
        Pending,
        Submitted,
        Shipped
    }
}
