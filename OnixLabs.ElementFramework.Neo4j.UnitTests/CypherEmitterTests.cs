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

namespace OnixLabs.ElementFramework.Neo4j.UnitTests;

public class CypherEmitterTests
{
    private readonly GraphModel model = TestModel.Build();
    private readonly CypherEmitter emitter = new();

    [Fact(DisplayName = "EmitAdd produces CREATE with every non-null property bound by name")]
    public void EmitAddProducesCreateWithBoundProperties()
    {
        Guid id = Guid.NewGuid();
        DateTimeOffset joinedAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        Author author = new() { Id = id, Name = "Alice", JoinedAt = joinedAt, Bio = "About Alice" };

        DataStatement statement = emitter.EmitAdd(model, author);

        Assert.Equal(
            "CREATE (n:Author { Id: $Id, Name: $Name, JoinedAt: $JoinedAt, Bio: $Bio }) RETURN n",
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
            "CREATE (n:Author { Id: $Id, Name: $Name, JoinedAt: $JoinedAt }) RETURN n",
            statement.Statement);
        Assert.False(statement.Parameters.ContainsKey("Bio"));
    }

    [Fact(DisplayName = "EmitUpdate produces MATCH ... SET ... RETURN with every non-key property explicitly set")]
    public void EmitUpdateProducesMatchSetReturn()
    {
        Author author = Author.Create("Alice");
        author.Bio = "About Alice";

        DataStatement statement = emitter.EmitUpdate(model, author);

        Assert.Equal(
            "MATCH (n:Author { Id: $Id }) SET n.Name = $Name, n.JoinedAt = $JoinedAt, n.Bio = $Bio RETURN n",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitUpdate explicitly sets null-valued properties so consumers can clear them")]
    public void EmitUpdateSetsNullProperties()
    {
        Author author = Author.Create("Alice");

        DataStatement statement = emitter.EmitUpdate(model, author);

        Assert.Equal(
            "MATCH (n:Author { Id: $Id }) SET n.Name = $Name, n.JoinedAt = $JoinedAt, n.Bio = $Bio RETURN n",
            statement.Statement);
        Assert.Null(statement.Parameters["Bio"]);
    }

    [Fact(DisplayName = "EmitRemove produces MATCH ... DETACH DELETE")]
    public void EmitRemoveProducesDetachDelete()
    {
        Author author = Author.Create("Alice");

        DataStatement statement = emitter.EmitRemove(model, author);

        Assert.Equal("MATCH (n:Author { Id: $Id }) DETACH DELETE n", statement.Statement);
        Assert.Equal(author.Id.ToString(), statement.Parameters["Id"]);
    }

    [Fact(DisplayName = "EmitMerge produces MERGE with ON CREATE SET and ON MATCH SET clauses when properties exist")]
    public void EmitMergeProducesOnCreateAndOnMatchClauses()
    {
        Author author = Author.Create("Alice");

        DataStatement statement = emitter.EmitMerge(model, author);

        Assert.Equal(
            "MERGE (n:Author { Id: $Id }) ON CREATE SET n.Name = $Name, n.JoinedAt = $JoinedAt, n.Bio = $Bio ON MATCH SET n.Name = $Name, n.JoinedAt = $JoinedAt, n.Bio = $Bio RETURN n",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitConnect produces MATCH ... CREATE [edge { props }]")]
    public void EmitConnectProducesMatchCreateWithEdgeProperties()
    {
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        DateTimeOffset writtenAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        Wrote wrote = new() { WrittenAt = writtenAt };

        DataStatement statement = emitter.EmitConnect(model, author, wrote, post);

        Assert.Equal(
            "MATCH (s:Author { Id: $start_Id }), (e:Post { Id: $end_Id }) CREATE (s)-[r:Wrote { WrittenAt: $edge_WrittenAt }]->(e) RETURN r",
            statement.Statement);
        Assert.Equal(author.Id.ToString(), statement.Parameters["start_Id"]);
        Assert.Equal(post.Id.ToString(), statement.Parameters["end_Id"]);
    }

    [Fact(DisplayName = "EmitConnect omits the property clause when the edge has no mapped properties")]
    public void EmitConnectOmitsPropertyClauseForMarkerEdges()
    {
        Comment comment = Comment.Create("Nice post.");
        Post post = Post.Create("Hello", "First post.");

        DataStatement statement = emitter.EmitConnect(model, comment, new CommentOn(), post);

        Assert.Equal(
            "MATCH (s:Comment { Id: $start_Id }), (e:Post { Id: $end_Id }) CREATE (s)-[r:CommentOn]->(e) RETURN r",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitDisconnect produces MATCH ... DELETE r")]
    public void EmitDisconnectProducesDeleteRelationship()
    {
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");

        DataStatement statement = emitter.EmitDisconnect<Author, Wrote, Post>(model, author, post);

        Assert.Equal(
            "MATCH (s:Author { Id: $start_Id })-[r:Wrote]->(e:Post { Id: $end_Id }) DELETE r",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitFindById produces MATCH with key constraint and LIMIT 1")]
    public void EmitFindByIdProducesLimitedMatch()
    {
        Guid id = Guid.NewGuid();
        DataStatement statement = emitter.EmitFindById<Author>(model, id);

        Assert.Equal("MATCH (n:Author { Id: $Id }) RETURN n LIMIT 1", statement.Statement);
        Assert.Equal(id.ToString(), statement.Parameters["Id"]);
    }

    [Fact(DisplayName = "EmitExists produces MATCH ... RETURN count(n) AS count")]
    public void EmitExistsProducesCountQuery()
    {
        Guid id = Guid.NewGuid();
        DataStatement statement = emitter.EmitExists<Author>(model, id);

        Assert.Equal("MATCH (n:Author { Id: $Id }) RETURN count(n) AS count", statement.Statement);
        Assert.Equal(id.ToString(), statement.Parameters["Id"]);
    }

    [Fact(DisplayName = "EmitAsEnumerableNodes produces an unfiltered MATCH on the label")]
    public void EmitAsEnumerableNodesProducesMatchAll()
    {
        DataStatement statement = emitter.EmitAsEnumerableNodes<Author>(model);

        Assert.Equal("MATCH (n:Author) RETURN n", statement.Statement);
        Assert.Empty(statement.Parameters);
    }

    [Fact(DisplayName = "EmitAsEnumerableEdges produces an unfiltered MATCH on the relationship type")]
    public void EmitAsEnumerableEdgesProducesMatchAll()
    {
        DataStatement statement = emitter.EmitAsEnumerableEdges<Wrote>(model);

        Assert.Equal("MATCH ()-[r:Wrote]->() RETURN r", statement.Statement);
        Assert.Empty(statement.Parameters);
    }

    [Fact(DisplayName = "EmitTraversal Match produces MATCH (alias:Label) RETURN alias")]
    public void EmitTraversalMatchSingleNode()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MATCH (a:Author) RETURN a", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal Merge produces MERGE keyword instead of MATCH")]
    public void EmitTraversalMergeProducesMergeKeyword()
    {
        TraversalAst ast = new(
            TraversalKind.Merge,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MERGE (a:Author) RETURN a", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal Create produces CREATE keyword instead of MATCH")]
    public void EmitTraversalCreateProducesCreateKeyword()
    {
        TraversalAst ast = new(
            TraversalKind.Create,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("CREATE (a:Author) RETURN a", statement.Statement);
    }

    [Theory(DisplayName = "EmitTraversal emits arrow notation matching the relationship direction")]
    [InlineData(RelationshipDirection.Outgoing, "MATCH (a:Author)-[w:Wrote]->(p:Post) RETURN p")]
    [InlineData(RelationshipDirection.Incoming, "MATCH (a:Author)<-[w:Wrote]-(p:Post) RETURN p")]
    [InlineData(RelationshipDirection.Either, "MATCH (a:Author)-[w:Wrote]-(p:Post) RETURN p")]
    public void EmitTraversalEmitsDirectionalArrows(RelationshipDirection direction, string expected)
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

        Assert.Equal(expected, statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal emits WHERE with a property-equality predicate")]
    public void EmitTraversalEmitsPropertyEqualityPredicate()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [new PropertyComparisonPredicate("a", nameof(Author.Name), ComparisonOperator.Equal, "Alice")],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MATCH (a:Author) WHERE (a.Name = $Name) RETURN a", statement.Statement);
        Assert.Equal("Alice", statement.Parameters["Name"]);
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

        Assert.Equal($"MATCH (a:Author) WHERE (a.JoinedAt {expectedToken} $JoinedAt) RETURN a", statement.Statement);
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

        Assert.Equal($"MATCH (a:Author) WHERE (a.Name {expectedToken} $Name) RETURN a", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal emits IS NULL and IS NOT NULL for NullPredicate without binding a value")]
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

        Assert.Equal("MATCH (a:Author) WHERE (a.Bio IS NULL) AND (a.Bio IS NOT NULL) RETURN a", statement.Statement);
        Assert.Empty(statement.Parameters);
    }

    [Fact(DisplayName = "EmitTraversal parenthesises And, Or, and Not predicates")]
    public void EmitTraversalParenthesisesBooleanComposition()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [
                new AndPredicate(
                    new OrPredicate(
                        new PropertyComparisonPredicate("a", nameof(Author.Name), ComparisonOperator.Equal, "Alice"),
                        new PropertyComparisonPredicate("a", nameof(Author.Name), ComparisonOperator.Equal, "Bob")),
                    new NotPredicate(
                        new PropertyComparisonPredicate("a", nameof(Author.JoinedAt), ComparisonOperator.LessThan, cutoff)))
            ],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal(
            "MATCH (a:Author) WHERE (((a.Name = $Name OR a.Name = $Name_1) AND NOT (a.JoinedAt < $JoinedAt))) RETURN a",
            statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal joins multiple top-level predicates with AND, each parenthesised")]
    public void EmitTraversalJoinsTopLevelPredicatesWithAnd()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [
                new PropertyComparisonPredicate("a", nameof(Author.Name), ComparisonOperator.Equal, "Alice"),
                new PropertyComparisonPredicate("a", nameof(Author.JoinedAt), ComparisonOperator.GreaterThan, cutoff)
            ],
            "a");

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MATCH (a:Author) WHERE (a.Name = $Name) AND (a.JoinedAt > $JoinedAt) RETURN a", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal appends ORDER BY <alias>.<property> ASC for an ascending ordering")]
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

        Assert.Equal("MATCH (a:Author) RETURN a ORDER BY a.Name ASC", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal appends ORDER BY <alias>.<property> DESC for a descending ordering")]
    public void EmitTraversalAppendsDescendingOrdering()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a")
        {
            Orderings = [new TraversalOrdering("a", nameof(Author.Name), OrderDirection.Descending)]
        };

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MATCH (a:Author) RETURN a ORDER BY a.Name DESC", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal appends SKIP n when the AST has a skip clause")]
    public void EmitTraversalAppendsSkip()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a") { Skip = 5 };

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MATCH (a:Author) RETURN a SKIP 5", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal appends LIMIT n when the AST has a Take clause")]
    public void EmitTraversalAppendsTakeAsLimit()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a") { Take = 10 };

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MATCH (a:Author) RETURN a LIMIT 10", statement.Statement);
    }

    [Fact(DisplayName = "EmitTraversal emits ORDER BY before SKIP before LIMIT in the canonical Cypher order")]
    public void EmitTraversalEmitsTailClausesInCanonicalOrder()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [],
            "a")
        {
            Orderings = [new TraversalOrdering("a", nameof(Author.Name), OrderDirection.Ascending)],
            Skip = 5,
            Take = 10
        };

        DataStatement statement = emitter.EmitTraversal(model, ast);

        Assert.Equal("MATCH (a:Author) RETURN a ORDER BY a.Name ASC SKIP 5 LIMIT 10", statement.Statement);
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

    [Fact(DisplayName = "EmitTraversal throws StatementEmissionException when a predicate references an unbound alias")]
    public void EmitTraversalThrowsForUnboundPredicateAlias()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [new PropertyComparisonPredicate("missing", nameof(Author.Name), ComparisonOperator.Equal, "Alice")],
            "a");
        Assert.Throws<StatementEmissionException>(() => emitter.EmitTraversal(model, ast));
    }

    [Fact(DisplayName = "EmitTraversal throws StatementEmissionException when a predicate references an unmapped property")]
    public void EmitTraversalThrowsForUnmappedPredicateProperty()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new NodePatternSegment(typeof(Author), "a")],
            [new PropertyComparisonPredicate("a", "DoesNotExist", ComparisonOperator.Equal, "x")],
            "a");
        Assert.Throws<StatementEmissionException>(() => emitter.EmitTraversal(model, ast));
    }
}
