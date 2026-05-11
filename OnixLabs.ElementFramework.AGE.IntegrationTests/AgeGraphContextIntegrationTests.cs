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

using Microsoft.Extensions.DependencyInjection;
using OnixLabs.ElementFramework.Conformance;
using OnixLabs.ElementFramework.Conformance.TestFixtures.BlogApplication;
using Xunit.Abstractions;

namespace OnixLabs.ElementFramework.AGE.IntegrationTests;

public sealed class AgeGraphContextIntegrationTests(ITestOutputHelper output, AgeFixture fixture)
    : AbstractGraphContextIntegrationTests(output), IClassFixture<AgeFixture>
{
    private const string GraphName = "blog_conformance";

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddGraphContext<BlogGraphContext>(builder =>
            builder.UseAge(() => fixture.ConnectionString, GraphName));

    protected override Task ResetGraphAsync()
    {
        _ = Context.RawStatement.Execute(
            $"SELECT * FROM ag_catalog.cypher('{GraphName}', $$ MATCH (n) DETACH DELETE n $$) AS (r agtype)",
            new Dictionary<string, object?>());
        return Task.CompletedTask;
    }

    private BlogGraphContext Context => Scope.GetRequiredService<BlogGraphContext>();

    // The four raw-statement conformance tests in the base class send openCypher directly into
    // RawStatement.Execute. AGE's raw surface is SQL — Cypher must be wrapped in cypher() — so the
    // Neo4j-flavored Cypher snippets the base tests use don't compose. We skip them here in the
    // same way the in-memory provider does.

    [Fact(DisplayName = "RawStatement Execute should return result rows", Skip = "AGE raw statements are SQL-wrapped Cypher, not bare Cypher.")]
    public override void RawStatementExecuteShouldReturnResultRows() => base.RawStatementExecuteShouldReturnResultRows();

    [Fact(DisplayName = "RawStatement.ExecuteAsync should yield result rows", Skip = "AGE raw statements are SQL-wrapped Cypher, not bare Cypher.")]
    public override Task RawStatementExecuteAsyncShouldYieldRows() => base.RawStatementExecuteAsyncShouldYieldRows();

    [Fact(DisplayName = "RawStatement.Execute writes should be visible to the OGM read APIs", Skip = "AGE raw statements are SQL-wrapped Cypher, not bare Cypher.")]
    public override void RawStatementExecuteShouldPersistMutationVisibleToOgm() => base.RawStatementExecuteShouldPersistMutationVisibleToOgm();

    [Fact(DisplayName = "RawStatement.Execute with a multi-row return should yield every row", Skip = "AGE raw statements are SQL-wrapped Cypher, not bare Cypher.")]
    public override void RawStatementExecuteShouldYieldMultipleRows() => base.RawStatementExecuteShouldYieldMultipleRows();
}
