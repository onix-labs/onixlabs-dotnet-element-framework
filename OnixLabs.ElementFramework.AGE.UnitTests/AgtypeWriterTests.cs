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

namespace OnixLabs.ElementFramework.AGE.UnitTests;

public class AgtypeWriterTests
{
    [Fact(DisplayName = "Serialize emits an empty JSON object for an empty parameter dictionary")]
    public void SerializeEmitsEmptyObjectForEmptyDictionary()
    {
        Assert.Equal("{}", AgtypeWriter.Serialize(new Dictionary<string, object?>()));
    }

    [Fact(DisplayName = "Serialize emits a single string property quoted, with no whitespace between members")]
    public void SerializeEmitsSingleStringProperty()
    {
        Dictionary<string, object?> parameters = new() { ["Name"] = "Alice" };

        Assert.Equal("{\"Name\":\"Alice\"}", AgtypeWriter.Serialize(parameters));
    }

    [Fact(DisplayName = "Serialize emits multiple properties in their insertion order")]
    public void SerializeEmitsPropertiesInInsertionOrder()
    {
        Dictionary<string, object?> parameters = new()
        {
            ["Name"] = "Alice",
            ["Age"] = 30,
            ["Active"] = true
        };

        Assert.Equal("{\"Name\":\"Alice\",\"Age\":30,\"Active\":true}", AgtypeWriter.Serialize(parameters));
    }

    [Fact(DisplayName = "Serialize emits null as the JSON null literal")]
    public void SerializeEmitsNullAsJsonNull()
    {
        Dictionary<string, object?> parameters = new() { ["Bio"] = null };

        Assert.Equal("{\"Bio\":null}", AgtypeWriter.Serialize(parameters));
    }

    [Theory(DisplayName = "Serialize emits every integer-family primitive as a JSON number")]
    [InlineData((byte)7, "7")]
    [InlineData((sbyte)-7, "-7")]
    [InlineData((short)1234, "1234")]
    [InlineData((ushort)1234, "1234")]
    [InlineData(42, "42")]
    [InlineData(42u, "42")]
    [InlineData(42L, "42")]
    [InlineData(42uL, "42")]
    public void SerializeEmitsIntegerFamilyAsJsonNumber(object input, string expectedNumber)
    {
        Dictionary<string, object?> parameters = new() { ["n"] = input };

        Assert.Equal($"{{\"n\":{expectedNumber}}}", AgtypeWriter.Serialize(parameters));
    }

    [Fact(DisplayName = "Serialize emits double as a JSON number")]
    public void SerializeEmitsDoubleAsJsonNumber()
    {
        Dictionary<string, object?> parameters = new() { ["Score"] = 3.14 };

        Assert.Equal("{\"Score\":3.14}", AgtypeWriter.Serialize(parameters));
    }

    [Fact(DisplayName = "Serialize emits decimal as a JSON number")]
    public void SerializeEmitsDecimalAsJsonNumber()
    {
        Dictionary<string, object?> parameters = new() { ["Price"] = 9.99m };

        Assert.Equal("{\"Price\":9.99}", AgtypeWriter.Serialize(parameters));
    }

    [Fact(DisplayName = "Serialize emits booleans as the JSON true / false literals")]
    public void SerializeEmitsBooleans()
    {
        Dictionary<string, object?> parameters = new() { ["t"] = true, ["f"] = false };

        Assert.Equal("{\"t\":true,\"f\":false}", AgtypeWriter.Serialize(parameters));
    }

    [Fact(DisplayName = "Serialize throws StatementEmissionException for a value that should have been pre-flattened (e.g. DateTime)")]
    public void SerializeThrowsForUnsupportedClrType()
    {
        Dictionary<string, object?> parameters = new() { ["CreatedAt"] = new DateTime(2026, 1, 1) };

        Assert.Throws<StatementEmissionException>(() => AgtypeWriter.Serialize(parameters));
    }
}
