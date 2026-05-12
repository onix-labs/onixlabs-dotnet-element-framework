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

using Npgsql;
using NpgsqlTypes;
using Xunit.Abstractions;

namespace OnixLabs.ElementFramework.AGE.IntegrationTests;

/// <summary>
/// Reconnaissance tests that probe how Apache AGE behaves through Npgsql before we commit to the
/// final provider design. These tests are not part of the conformance suite — they answer specific
/// design questions about agtype round-trip, parameter binding, and the cypher() envelope.
/// </summary>
/// <remarks>
/// Each test logs its inspected values via <see cref="ITestOutputHelper"/> so the reconnaissance
/// output is visible in CI without needing to attach a debugger. Once the provider stabilises these
/// can be retired in favour of conformance coverage.
/// </remarks>
public sealed class AgeReconnaissanceTests(ITestOutputHelper output, AgeReconnaissanceFixture fixture)
    : IClassFixture<AgeReconnaissanceFixture>, IAsyncLifetime
{
    private const string GraphName = "recon_graph";
    private readonly NpgsqlDataSource dataSource = fixture.CreateDataSource();

    public async Task InitializeAsync()
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await ExecuteAsync(connection, "CREATE EXTENSION IF NOT EXISTS age");
        await ExecuteAsync(connection, "LOAD 'age'");
        await ExecuteAsync(connection, "SET search_path = ag_catalog, \"$user\", public");
        try
        {
            await ExecuteAsync(connection, $"SELECT drop_graph('{GraphName}', true)");
        }
        catch
        {
            // graph may not exist on first run
        }
        await ExecuteAsync(connection, $"SELECT create_graph('{GraphName}')");
    }

    public Task DisposeAsync()
    {
        dataSource.Dispose();
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "Reconnaissance: trivial cypher() envelope returns a single agtype column readable as string")]
    public async Task TrivialCypherEnvelope()
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync();
        await using NpgsqlCommand command = new(
            $"SELECT * FROM cypher('{GraphName}', $$ RETURN 1 AS one $$) AS (one agtype)", connection);
        command.AllResultTypesAreUnknown = true;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        string raw = reader.GetFieldValue<string>(0);
        output.WriteLine($"agtype value:   {raw}");
        output.WriteLine($"DataTypeName:   {reader.GetDataTypeName(0)}");
        Assert.False(await reader.ReadAsync());
    }

    [Fact(DisplayName = "Reconnaissance: vertex agtype reads as a vertex literal — '{...}::vertex'")]
    public async Task NodeRoundTrip()
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync();
        await ExecuteAsync(connection,
            $"SELECT * FROM cypher('{GraphName}', $$ CREATE (a:Author {{ name: 'Alice', age: 30 }}) RETURN a $$) AS (a agtype)");

        await using NpgsqlCommand command = new(
            $"SELECT * FROM cypher('{GraphName}', $$ MATCH (a:Author) RETURN a $$) AS (a agtype)", connection);
        command.AllResultTypesAreUnknown = true;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        string raw = reader.GetFieldValue<string>(0);
        output.WriteLine($"vertex literal: {raw}");
        output.WriteLine($"DataTypeName:   {reader.GetDataTypeName(0)}");
    }

    [Fact(DisplayName = "Reconnaissance: projecting node properties returns scalar agtypes")]
    public async Task PropertyProjection()
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync();
        await ExecuteAsync(connection,
            $"SELECT * FROM cypher('{GraphName}', $$ CREATE (b:Author {{ name: 'Bob', age: 42, active: true }}) RETURN b $$) AS (b agtype)");

        await using NpgsqlCommand command = new(
            $"SELECT * FROM cypher('{GraphName}', $$ MATCH (b:Author {{ name: 'Bob' }}) RETURN b.name, b.age, b.active $$) AS (name agtype, age agtype, active agtype)",
            connection);
        command.AllResultTypesAreUnknown = true;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        for (int i = 0; i < reader.FieldCount; i++)
        {
            string raw = reader.GetFieldValue<string>(i);
            output.WriteLine($"col[{i}] {reader.GetName(i)}: {raw}");
        }
    }

    [Fact(DisplayName = "Reconnaissance: parameter sent with NpgsqlDbType.Unknown lets Postgres infer agtype")]
    public async Task ParameterBindingViaUnknown()
    {
        // The cypher() third argument must be a bare $N — no cast expression — and Npgsql refuses
        // to write System.String as ag_catalog.agtype. The workaround that actually compiles cleanly
        // through Npgsql is to send the parameter as NpgsqlDbType.Unknown, which leaves type
        // inference to Postgres (and AGE's third-arg check sees a real parameter symbol).
        await using NpgsqlConnection connection = await OpenConnectionAsync();
        await using NpgsqlCommand command = new(
            $"SELECT * FROM cypher('{GraphName}', $$ CREATE (c:Author {{ name: $name, age: $age }}) RETURN c $$, @p) AS (c agtype)",
            connection);
        command.Parameters.Add(new NpgsqlParameter("p", NpgsqlDbType.Unknown) { Value = "{\"name\": \"Carol\", \"age\": 27}" });
        command.AllResultTypesAreUnknown = true;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        string raw = reader.GetFieldValue<string>(0);
        output.WriteLine($"parameterized create: {raw}");
    }

    [Fact(DisplayName = "Reconnaissance: relationship round-trip — CREATE start, end, edge, then MATCH the pattern")]
    public async Task RelationshipRoundTrip()
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync();
        await ExecuteAsync(connection,
            $"SELECT * FROM cypher('{GraphName}', $$ CREATE (a:Author {{ name: 'Dora' }}) RETURN a $$) AS (a agtype)");
        await ExecuteAsync(connection,
            $"SELECT * FROM cypher('{GraphName}', $$ CREATE (p:Post {{ title: 'Hello' }}) RETURN p $$) AS (p agtype)");
        await ExecuteAsync(connection,
            $"SELECT * FROM cypher('{GraphName}', $$ MATCH (a:Author {{ name: 'Dora' }}), (p:Post {{ title: 'Hello' }}) CREATE (a)-[w:WROTE {{ at: '2026' }}]->(p) RETURN w $$) AS (w agtype)");

        await using NpgsqlCommand command = new(
            $"SELECT * FROM cypher('{GraphName}', $$ MATCH (a:Author)-[w:WROTE]->(p:Post) RETURN a, w, p $$) AS (a agtype, w agtype, p agtype)",
            connection);
        command.AllResultTypesAreUnknown = true;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        for (int i = 0; i < reader.FieldCount; i++)
            output.WriteLine($"col[{i}] {reader.GetName(i)}: {reader.GetFieldValue<string>(i)}");
    }

    [Fact(DisplayName = "Reconnaissance: count() projection returns an integer agtype")]
    public async Task CountProjection()
    {
        await using NpgsqlConnection connection = await OpenConnectionAsync();
        await ExecuteAsync(connection,
            $"SELECT * FROM cypher('{GraphName}', $$ CREATE (:Author {{ name: 'Eve' }}) $$) AS (x agtype)");

        await using NpgsqlCommand command = new(
            $"SELECT * FROM cypher('{GraphName}', $$ MATCH (a:Author {{ name: 'Eve' }}) RETURN count(a) AS c $$) AS (c agtype)",
            connection);
        command.AllResultTypesAreUnknown = true;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        string raw = reader.GetFieldValue<string>(0);
        output.WriteLine($"count agtype: {raw}");
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await ExecuteAsync(connection, "LOAD 'age'");
        await ExecuteAsync(connection, "SET search_path = ag_catalog, \"$user\", public");
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
