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

public class AgeCypherEmitterTests
{
    private const string Graph = "test_graph";
    private readonly GraphModel model = TestModel.Build();
    private readonly AgeCypherEmitter emitter = new(Graph);

    [Fact(DisplayName = "Constructor throws ArgumentException for a blank graph name")]
    public void ConstructorThrowsForBlankGraphName()
    {
        Assert.Throws<ArgumentException>(() => new AgeCypherEmitter(""));
        Assert.Throws<ArgumentException>(() => new AgeCypherEmitter(" "));
    }

    [Fact(DisplayName = "Constructor escapes embedded single quotes in the graph name by doubling them")]
    public void ConstructorEscapesSingleQuotesInGraphName()
    {
        AgeCypherEmitter custom = new("name'with'quotes");
        DataStatement statement = custom.EmitAsEnumerableNodes<Author>(model);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('name''with''quotes', $$ MATCH (n:Author) RETURN n $$) AS (n agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitAdd wraps CREATE in ag_catalog.cypher and binds non-null properties through @p")]
    public void EmitAddWrapsCreate()
    {
        Guid id = Guid.NewGuid();
        DateTimeOffset joinedAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        Author author = new() { Id = id, Name = "Alice", JoinedAt = joinedAt, Bio = "About Alice" };

        DataStatement statement = emitter.EmitAdd(model, author);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ CREATE (n:Author { Id: $Id, Name: $Name, JoinedAt: $JoinedAt, Bio: $Bio }) RETURN n $$, @p) AS (n agtype)",
            statement.Statement);
        Assert.Equal(id.ToString(), statement.Parameters["Id"]);
        Assert.Equal("Alice", statement.Parameters["Name"]);
        Assert.Equal("About Alice", statement.Parameters["Bio"]);
    }

    [Fact(DisplayName = "EmitAdd skips null non-key properties from the property clause")]
    public void EmitAddSkipsNullProperties()
    {
        Author author = Author.Create("Alice");

        DataStatement statement = emitter.EmitAdd(model, author);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ CREATE (n:Author { Id: $Id, Name: $Name, JoinedAt: $JoinedAt }) RETURN n $$, @p) AS (n agtype)",
            statement.Statement);
        Assert.False(statement.Parameters.ContainsKey("Bio"));
    }

    [Fact(DisplayName = "EmitUpdate wraps MATCH ... SET ... RETURN and explicitly sets null-valued properties")]
    public void EmitUpdateWrapsMatchSet()
    {
        Author author = Author.Create("Alice");

        DataStatement statement = emitter.EmitUpdate(model, author);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (n:Author { Id: $Id }) SET n.Name = $Name, n.JoinedAt = $JoinedAt, n.Bio = $Bio RETURN n $$, @p) AS (n agtype)",
            statement.Statement);
        Assert.Null(statement.Parameters["Bio"]);
    }

    [Fact(DisplayName = "EmitRemove wraps MATCH ... DETACH DELETE; the AS schema still declares one column")]
    public void EmitRemoveWrapsDetachDelete()
    {
        Author author = Author.Create("Alice");

        DataStatement statement = emitter.EmitRemove(model, author);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (n:Author { Id: $Id }) DETACH DELETE n $$, @p) AS (n agtype)",
            statement.Statement);
        Assert.Equal(author.Id.ToString(), statement.Parameters["Id"]);
    }

    [Fact(DisplayName = "EmitMerge uses plain MERGE ... SET (AGE 1.6.0 doesn't implement ON CREATE SET / ON MATCH SET)")]
    public void EmitMergeUsesPlainSet()
    {
        Author author = Author.Create("Alice");

        DataStatement statement = emitter.EmitMerge(model, author);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MERGE (n:Author { Id: $Id }) SET n.Name = $Name, n.JoinedAt = $JoinedAt, n.Bio = $Bio RETURN n $$, @p) AS (n agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitConnect wraps MATCH ... CREATE [edge {props}] and uses the r alias for the column")]
    public void EmitConnectWrapsMatchCreate()
    {
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        DateTimeOffset writtenAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        Wrote wrote = new() { WrittenAt = writtenAt };

        DataStatement statement = emitter.EmitConnect(model, author, wrote, post);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (s:Author { Id: $start_Id }), (e:Post { Id: $end_Id }) CREATE (s)-[r:Wrote { WrittenAt: $edge_WrittenAt }]->(e) RETURN r $$, @p) AS (r agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitConnect omits the property clause for marker edges with no mapped properties")]
    public void EmitConnectOmitsPropertyClauseForMarkerEdges()
    {
        Comment comment = Comment.Create("Nice post.");
        Post post = Post.Create("Hello", "First post.");

        DataStatement statement = emitter.EmitConnect(model, comment, new CommentOn(), post);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (s:Comment { Id: $start_Id }), (e:Post { Id: $end_Id }) CREATE (s)-[r:CommentOn]->(e) RETURN r $$, @p) AS (r agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitDisconnect wraps MATCH ... DELETE r")]
    public void EmitDisconnectWrapsDelete()
    {
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");

        DataStatement statement = emitter.EmitDisconnect<Author, Wrote, Post>(model, author, post);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (s:Author { Id: $start_Id })-[r:Wrote]->(e:Post { Id: $end_Id }) DELETE r $$, @p) AS (r agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitFindById wraps MATCH with key constraint and LIMIT 1")]
    public void EmitFindByIdWrapsLimitedMatch()
    {
        Guid id = Guid.NewGuid();
        DataStatement statement = emitter.EmitFindById<Author>(model, id);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (n:Author { Id: $Id }) RETURN n LIMIT 1 $$, @p) AS (n agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitExists projects count(n) under the cnt alias to avoid 'count' as a SQL reserved word")]
    public void EmitExistsUsesCntAlias()
    {
        Guid id = Guid.NewGuid();
        DataStatement statement = emitter.EmitExists<Author>(model, id);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (n:Author { Id: $Id }) RETURN count(n) AS cnt $$, @p) AS (cnt agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitAsEnumerableNodes wraps an unfiltered MATCH and omits @p when no parameters are bound")]
    public void EmitAsEnumerableNodesOmitsParameter()
    {
        DataStatement statement = emitter.EmitAsEnumerableNodes<Author>(model);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (n:Author) RETURN n $$) AS (n agtype)",
            statement.Statement);
        Assert.Empty(statement.Parameters);
    }

    [Fact(DisplayName = "EmitAsEnumerableEdges wraps an unfiltered MATCH on the relationship type, no @p")]
    public void EmitAsEnumerableEdgesOmitsParameter()
    {
        DataStatement statement = emitter.EmitAsEnumerableEdges<Wrote>(model);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH ()-[r:Wrote]->() RETURN r $$) AS (r agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal wraps Match traversals; the AS column uses the return alias")]
    public void EmitTraversalWrapsMatch()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (a:Author) RETURN a $$) AS (a agtype)",
            statement.Statement);
    }

    [Theory(DisplayName = "EmitTraversal emits arrow notation matching the relationship direction")]
    [InlineData(RelationshipDirection.Outgoing, "MATCH (a:Author)-[w:Wrote]->(p:Post) RETURN p")]
    [InlineData(RelationshipDirection.Incoming, "MATCH (a:Author)<-[w:Wrote]-(p:Post) RETURN p")]
    [InlineData(RelationshipDirection.Either, "MATCH (a:Author)-[w:Wrote]-(p:Post) RETURN p")]
    public void EmitTraversalEmitsDirectionalArrows(RelationshipDirection direction, string innerCypher)
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [
                new NodePatternSegment(typeof(Author), "a"),
                new RelationshipPatternSegment(typeof(Wrote), "w", direction),
                new NodePatternSegment(typeof(Post), "p")
            ],
            [],
            "p");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            $"SELECT * FROM ag_catalog.cypher('test_graph', $$ {innerCypher} $$) AS (p agtype)",
            statement.Statement);
    }

    [Theory(DisplayName = "EmitTraversal maps every comparison operator to its Cypher token")]
    [InlineData(ComparisonOperator.Equal, "=")]
    [InlineData(ComparisonOperator.NotEqual, "<>")]
    [InlineData(ComparisonOperator.LessThan, "<")]
    [InlineData(ComparisonOperator.LessThanOrEqual, "<=")]
    [InlineData(ComparisonOperator.GreaterThan, ">")]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, ">=")]
    public void EmitTraversalMapsComparisonOperators(ComparisonOperator op, string expectedToken)
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [new PropertyComparisonPredicate("a", nameof(Author.JoinedAt), op, cutoff)],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            $"SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (a:Author) WHERE (a.JoinedAt {expectedToken} $JoinedAt) RETURN a $$, @p) AS (a agtype)",
            statement.Statement);
    }

    [Theory(DisplayName = "EmitTraversal maps every string-comparison operator to its Cypher token")]
    [InlineData(StringComparisonOperator.Contains, "CONTAINS")]
    [InlineData(StringComparisonOperator.StartsWith, "STARTS WITH")]
    [InlineData(StringComparisonOperator.EndsWith, "ENDS WITH")]
    public void EmitTraversalMapsStringComparisonOperators(StringComparisonOperator op, string expectedToken)
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [new StringComparisonPredicate("a", nameof(Author.Name), op, "li")],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            $"SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (a:Author) WHERE (a.Name {expectedToken} $Name) RETURN a $$, @p) AS (a agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal emits IS NULL / IS NOT NULL for NullPredicate without binding values")]
    public void EmitTraversalEmitsNullPredicate()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [
                new NullPredicate("a", nameof(Author.Bio), IsNull: true),
                new NullPredicate("a", nameof(Author.Bio), IsNull: false)
            ],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (a:Author) WHERE (a.Bio IS NULL) AND (a.Bio IS NOT NULL) RETURN a $$) AS (a agtype)",
            statement.Statement);
        Assert.Empty(statement.Parameters);
    }

    [Fact(DisplayName = "EmitTraversal appends ORDER BY <alias>.<property> ASC")]
    public void EmitTraversalAppendsAscendingOrdering()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a")
        {
            Orderings = [new TraversalOrdering("a", nameof(Author.Name), OrderDirection.Ascending)]
        };

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (a:Author) RETURN a ORDER BY a.Name ASC $$) AS (a agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal emits ORDER BY before SKIP before LIMIT inside the cypher() envelope")]
    public void EmitTraversalEmitsTailClausesInCanonicalOrder()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a")
        {
            Orderings = [new TraversalOrdering("a", nameof(Author.Name), OrderDirection.Descending)],
            Skip = 5,
            Take = 10
        };

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            "SELECT * FROM ag_catalog.cypher('test_graph', $$ MATCH (a:Author) RETURN a ORDER BY a.Name DESC SKIP 5 LIMIT 10 $$) AS (a agtype)",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal throws StatementEmissionException when segments are empty")]
    public void EmitTraversalThrowsForEmptySegments()
    {
        TraversalAst ast = new(TraversalKind.Match, [], [], "a");
        Assert.Throws<StatementEmissionException>(() => emitter.EmitTraversal(model, ast));
    }

    [Fact(DisplayName = "EmitTraversal throws StatementEmissionException when the first segment is not a node")]
    public void EmitTraversalThrowsWhenFirstSegmentIsNotANode()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new RelationshipPatternSegment(typeof(Wrote), "w", RelationshipDirection.Outgoing)],
            [],
            "w");
        Assert.Throws<StatementEmissionException>(() => emitter.EmitTraversal(model, ast));
    }

    [Fact(DisplayName = "EmitTraversal throws StatementEmissionException for an unbound predicate alias")]
    public void EmitTraversalThrowsForUnboundPredicateAlias()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [new PropertyComparisonPredicate("missing", nameof(Author.Name), ComparisonOperator.Equal, "Alice")],
            "a");
        Assert.Throws<StatementEmissionException>(() => emitter.EmitTraversal(model, ast));
    }

    [Fact(DisplayName = "EmitTraversal throws StatementEmissionException for an unmapped predicate property")]
    public void EmitTraversalThrowsForUnmappedPredicateProperty()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [new PropertyComparisonPredicate("a", "DoesNotExist", ComparisonOperator.Equal, "x")],
            "a");
        Assert.Throws<StatementEmissionException>(() => emitter.EmitTraversal(model, ast));
    }

    [Fact(DisplayName = "EmitTraversal throws StatementEmissionException when an ordering references an unbound alias")]
    public void EmitTraversalThrowsForUnboundOrderingAlias()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a")
        {
            Orderings = [new TraversalOrdering("missing", nameof(Author.Name), OrderDirection.Ascending)]
        };

        Assert.Throws<StatementEmissionException>(() => emitter.EmitTraversal(model, ast));
    }
}
