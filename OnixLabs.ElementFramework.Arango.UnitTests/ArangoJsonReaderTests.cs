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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OnixLabs.ElementFramework.Arango.UnitTests;

public class ArangoJsonReaderTests
{
    private static JObject ParseObject(string json) => JObject.Parse(json);

    private static JObject ParseObjectNoDates(string json)
    {
        using StringReader sr = new(json);
        using JsonTextReader reader = new(sr) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
    }

    [Fact(DisplayName = "ReadRow converts a JSON object to a Dictionary keyed by property name")]
    public void ReadRowConvertsTopLevelObject()
    {
        JObject row = ParseObject("""{ "x": 1, "y": "two" }""");

        Dictionary<string, object?> result = ArangoJsonReader.ReadRow(row);

        Assert.Equal(1L, result["x"]);
        Assert.Equal("two", result["y"]);
    }

    [Fact(DisplayName = "ReadRow reads nested objects as nested Dictionaries")]
    public void ReadRowReadsNestedObjects()
    {
        JObject row = ParseObject("""{ "n": { "_key": "k", "value": 7 } }""");

        Dictionary<string, object?> result = ArangoJsonReader.ReadRow(row);

        IReadOnlyDictionary<string, object?> inner = Assert.IsType<Dictionary<string, object?>>(result["n"]);
        Assert.Equal("k", inner["_key"]);
        Assert.Equal(7L, inner["value"]);
    }

    [Fact(DisplayName = "ReadRow reads arrays as Lists, preserving primitive shape")]
    public void ReadRowReadsArrays()
    {
        JObject row = ParseObject("""{ "items": [1, "two", true, null] }""");

        Dictionary<string, object?> result = ArangoJsonReader.ReadRow(row);

        List<object?> items = Assert.IsType<List<object?>>(result["items"]);
        Assert.Equal(1L, items[0]);
        Assert.Equal("two", items[1]);
        Assert.Equal(true, items[2]);
        Assert.Null(items[3]);
    }

    [Fact(DisplayName = "ReadRow promotes fractional numbers to double and integers to long")]
    public void ReadRowDistinguishesIntegerAndFloat()
    {
        JObject row = ParseObject("""{ "i": 42, "f": 3.14 }""");

        Dictionary<string, object?> result = ArangoJsonReader.ReadRow(row);

        Assert.IsType<long>(result["i"]);
        Assert.IsType<double>(result["f"]);
    }

    [Fact(DisplayName = "ReadRow surfaces ISO-8601 strings as strings when the underlying reader uses DateParseHandling.None")]
    public void ReadRowReadsIsoDatesAsStrings()
    {
        // This is the configuration ArangoSerialization installs — date strings stay strings, not silently
        // parsed to DateTime with a lossy local-time conversion.
        JObject row = ParseObjectNoDates("""{ "when": "2026-05-11T13:30:00.0000000+02:00" }""");

        Dictionary<string, object?> result = ArangoJsonReader.ReadRow(row);

        Assert.Equal("2026-05-11T13:30:00.0000000+02:00", result["when"]);
    }
}
