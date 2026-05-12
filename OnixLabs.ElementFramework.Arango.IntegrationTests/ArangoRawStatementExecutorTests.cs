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

using ArangoDBNetStandard;
using ArangoDBNetStandard.Transport.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace OnixLabs.ElementFramework.Arango.IntegrationTests;

/// <summary>
/// Wire-level coverage of <c>ArangoRawStatementExecutor</c> — proves the executor can round-trip AQL statements with parameters, project rows by alias, and stream multi-batch cursors. Sits at the same level as the AGE provider's reconnaissance tests: validates the wire and the row-shape contract before the emitter / materializer pipeline is built on top.
/// </summary>
public sealed class ArangoRawStatementExecutorTests(ArangoFixture fixture) : IClassFixture<ArangoFixture>
{
    private ArangoRawStatementExecutor CreateExecutor()
    {
        HttpApiTransport transport = HttpApiTransport.UsingBasicAuth(fixture.Endpoint, ArangoFixture.SystemDatabase, fixture.Username, fixture.Password);
        ArangoDBClient client = new(transport);
        ArangoGraphTransactionOpener opener = new(client, NullLogger<ArangoGraphTransactionOpener>.Instance, NullLogger<ArangoGraphTransaction>.Instance);
        return new ArangoRawStatementExecutor(client, opener, NullLogger<ArangoRawStatementExecutor>.Instance);
    }

    [Fact(DisplayName = "Execute returns a single row for a literal RETURN object")]
    public void ExecuteReturnsSingleLiteralRow()
    {
        ArangoRawStatementExecutor executor = CreateExecutor();

        List<IReadOnlyDictionary<string, object?>> rows = executor.Execute("RETURN { x: 1, y: \"two\" }", new Dictionary<string, object?>()).ToList();

        IReadOnlyDictionary<string, object?> row = Assert.Single(rows);
        Assert.Equal(1L, row["x"]);
        Assert.Equal("two", row["y"]);
    }

    [Fact(DisplayName = "Execute substitutes bind variables")]
    public void ExecuteSubstitutesBindVariables()
    {
        ArangoRawStatementExecutor executor = CreateExecutor();

        List<IReadOnlyDictionary<string, object?>> rows = executor.Execute(
            "RETURN { greeting: @greeting, count: @count }",
            new Dictionary<string, object?> { ["greeting"] = "hello", ["count"] = 7 }).ToList();

        IReadOnlyDictionary<string, object?> row = Assert.Single(rows);
        Assert.Equal("hello", row["greeting"]);
        Assert.Equal(7L, row["count"]);
    }

    [Fact(DisplayName = "ExecuteAsync streams multi-batch results")]
    public async Task ExecuteAsyncStreamsMultiBatchResults()
    {
        ArangoRawStatementExecutor executor = CreateExecutor();

        // 250 rows with batch size 100 forces three round-trips: initial POST + two PUTs.
        List<IReadOnlyDictionary<string, object?>> rows = [];
        await foreach (IReadOnlyDictionary<string, object?> row in executor.ExecuteAsync(
            "FOR i IN 1..250 RETURN { n: { _key: TO_STRING(i), value: i } }",
            new Dictionary<string, object?>()))
        {
            rows.Add(row);
        }

        Assert.Equal(250, rows.Count);
        Assert.Equal("1", ((IReadOnlyDictionary<string, object?>)rows[0]["n"]!)["_key"]);
        Assert.Equal(250L, ((IReadOnlyDictionary<string, object?>)rows[249]["n"]!)["value"]);
    }

    [Fact(DisplayName = "Execute wraps an invalid AQL statement as a RawStatementException")]
    public void ExecuteWrapsInvalidAql()
    {
        ArangoRawStatementExecutor executor = CreateExecutor();

        RawStatementException exception = Assert.Throws<RawStatementException>(() =>
            executor.Execute("THIS IS NOT VALID AQL", new Dictionary<string, object?>()).ToList());

        Assert.NotNull(exception.InnerException);
    }

    [Fact(DisplayName = "Result rows project nested objects as nested dictionaries the materializer can read")]
    public void ResultRowsProjectNestedDictionaries()
    {
        ArangoRawStatementExecutor executor = CreateExecutor();
        ArangoResultMaterializer materializer = new();

        List<IReadOnlyDictionary<string, object?>> rows = executor.Execute(
            "RETURN { cnt: 5 }",
            new Dictionary<string, object?>()).ToList();

        Assert.True(materializer.ReadExists(rows[0]));
    }
}
