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
using Microsoft.Extensions.DependencyInjection;
using OnixLabs.ElementFramework.Conformance;
using OnixLabs.ElementFramework.Conformance.TestFixtures.BlogApplication;
using Xunit.Abstractions;

namespace OnixLabs.ElementFramework.Arango.IntegrationTests;

public sealed class ArangoGraphContextIntegrationTests(ITestOutputHelper output, ArangoFixture fixture)
    : AbstractGraphContextIntegrationTests(output), IClassFixture<ArangoFixture>
{
    private const string Database = ArangoFixture.SystemDatabase;

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddGraphContext<BlogGraphContext>(builder =>
            builder.UseArango(() => fixture.Endpoint, Database, fixture.Username, fixture.Password));

    protected override string? ProviderSourceName => ArangoDiagnostics.SourceName;

    protected override async Task ResetGraphAsync()
    {
        BlogGraphContext context = Scope.GetRequiredService<BlogGraphContext>();

        // Ensure every collection the model expects exists before truncating — idempotent after the first run.
        await ArangoSchemaBootstrap.EnsureCollectionsAsync(
            context, fixture.Endpoint, Database, fixture.Username, fixture.Password);

        using HttpApiTransport transport = HttpApiTransport.UsingBasicAuth(fixture.Endpoint, Database, fixture.Username, fixture.Password);
        using ArangoDBClient client = new(transport);
        foreach (INodeMetadata node in context.Model.Nodes)
            await client.Collection.TruncateCollectionAsync(node.Label);
        foreach (IRelationshipMetadata relationship in context.Model.Relationships)
            await client.Collection.TruncateCollectionAsync(relationship.RelationshipType);
    }

    // The four raw-statement conformance tests in the base class send openCypher directly into RawStatement.Execute.
    // ArangoDB's raw surface is AQL, not Cypher, so those snippets don't compose. We skip them here in the same
    // way the AGE provider does.

    [Fact(DisplayName = "RawStatement Execute should return result rows", Skip = "ArangoDB raw statements are AQL, not Cypher.")]
    public override void RawStatementExecuteShouldReturnResultRows() => base.RawStatementExecuteShouldReturnResultRows();

    [Fact(DisplayName = "RawStatement.ExecuteAsync should yield result rows", Skip = "ArangoDB raw statements are AQL, not Cypher.")]
    public override Task RawStatementExecuteAsyncShouldYieldRows() => base.RawStatementExecuteAsyncShouldYieldRows();

    [Fact(DisplayName = "RawStatement.Execute writes should be visible to the OGM read APIs", Skip = "ArangoDB raw statements are AQL, not Cypher.")]
    public override void RawStatementExecuteShouldPersistMutationVisibleToOgm() => base.RawStatementExecuteShouldPersistMutationVisibleToOgm();

    [Fact(DisplayName = "RawStatement.Execute with a multi-row return should yield every row", Skip = "ArangoDB raw statements are AQL, not Cypher.")]
    public override void RawStatementExecuteShouldYieldMultipleRows() => base.RawStatementExecuteShouldYieldMultipleRows();
}
