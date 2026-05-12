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

namespace OnixLabs.ElementFramework.Arango.UnitTests;

public class ArangoTraversalEmitterTests
{
    private static readonly GraphModel Model = TestModel.Build();

    private static TraversalAst NodeOnly(string alias = "a") => new(
        TraversalKind.Match,
        [new NodePatternSegment(typeof(Author), alias)],
        [],
        alias);

    private static TraversalAst AuthorWroteToPost(string returnAlias = "p") => new(
        TraversalKind.Match,
        [
            new NodePatternSegment(typeof(Author), "a"),
            new RelationshipPatternSegment(typeof(Wrote), "w", RelationshipDirection.Outgoing),
            new NodePatternSegment(typeof(Post), "p")
        ],
        [],
        returnAlias);

    [Fact(DisplayName = "Emit Match on a single node produces a top-level FOR ... IN @@col RETURN { alias: alias }")]
    public void EmitSingleNodeMatch()
    {
        DataStatement statement = ArangoTraversalEmitter.Emit(Model, NodeOnly());

        Assert.Equal("FOR a IN @@c0 RETURN { a: a }", statement.Statement);
        Assert.Equal("Author", statement.Parameters["@c0"]);
    }

    [Fact(DisplayName = "Emit Match on a relationship pattern nests FOR loops with OUTBOUND for Outgoing direction")]
    public void EmitRelationshipMatchOutbound()
    {
        DataStatement statement = ArangoTraversalEmitter.Emit(Model, AuthorWroteToPost());

        Assert.Equal("FOR a IN @@c0 FOR p, w IN 1..1 OUTBOUND a @@c1 RETURN { p: p }", statement.Statement);
        Assert.Equal("Author", statement.Parameters["@c0"]);
        Assert.Equal("Wrote", statement.Parameters["@c1"]);
    }

    [Theory(DisplayName = "Emit Match maps relationship direction to AQL keyword")]
    [InlineData(RelationshipDirection.Outgoing, "OUTBOUND")]
    [InlineData(RelationshipDirection.Incoming, "INBOUND")]
    [InlineData(RelationshipDirection.Either, "ANY")]
    public void EmitRelationshipDirection(RelationshipDirection direction, string keyword)
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

        DataStatement statement = ArangoTraversalEmitter.Emit(Model, ast);

        Assert.Contains(keyword, statement.Statement);
    }

    [Fact(DisplayName = "Emit lowers a property-comparison predicate to alias.prop OP @paramN")]
    public void EmitPropertyComparisonPredicate()
    {
        TraversalAst ast = NodeOnly() with
        {
            Predicates = [new PropertyComparisonPredicate("a", "Name", ComparisonOperator.Equal, "Ada")]
        };

        DataStatement statement = ArangoTraversalEmitter.Emit(Model, ast);

        Assert.Contains("FILTER a.Name == @p0", statement.Statement);
        Assert.Equal("Ada", statement.Parameters["p0"]);
    }

    [Theory(DisplayName = "Emit lowers each comparison operator to its AQL equivalent")]
    [InlineData(ComparisonOperator.Equal, "==")]
    [InlineData(ComparisonOperator.NotEqual, "!=")]
    [InlineData(ComparisonOperator.LessThan, "<")]
    [InlineData(ComparisonOperator.LessThanOrEqual, "<=")]
    [InlineData(ComparisonOperator.GreaterThan, ">")]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, ">=")]
    public void EmitComparisonOperators(ComparisonOperator op, string aql)
    {
        TraversalAst ast = NodeOnly() with
        {
            Predicates = [new PropertyComparisonPredicate("a", "Name", op, "x")]
        };

        DataStatement statement = ArangoTraversalEmitter.Emit(Model, ast);

        Assert.Contains($"a.Name {aql} @p0", statement.Statement);
    }

    [Fact(DisplayName = "Emit Contains/StartsWith/EndsWith use AQL string functions and a safe SUBSTRING fallback")]
    public void EmitStringComparisons()
    {
        TraversalAst contains = NodeOnly() with
        {
            Predicates = [new StringComparisonPredicate("a", "Name", StringComparisonOperator.Contains, "ada")]
        };
        TraversalAst startsWith = NodeOnly() with
        {
            Predicates = [new StringComparisonPredicate("a", "Name", StringComparisonOperator.StartsWith, "Ad")]
        };
        TraversalAst endsWith = NodeOnly() with
        {
            Predicates = [new StringComparisonPredicate("a", "Name", StringComparisonOperator.EndsWith, "ace")]
        };

        Assert.Contains("CONTAINS(a.Name, @p0)", ArangoTraversalEmitter.Emit(Model, contains).Statement);
        Assert.Contains("STARTS_WITH(a.Name, @p0)", ArangoTraversalEmitter.Emit(Model, startsWith).Statement);
        // ENDS_WITH is not in AQL 3.12; we use SUBSTRING instead.
        Assert.Contains("SUBSTRING(a.Name", ArangoTraversalEmitter.Emit(Model, endsWith).Statement);
    }

    [Fact(DisplayName = "Emit lowers NullPredicate to alias.prop == null or != null")]
    public void EmitNullPredicates()
    {
        TraversalAst isNull = NodeOnly() with { Predicates = [new NullPredicate("a", "Bio", true)] };
        TraversalAst isNotNull = NodeOnly() with { Predicates = [new NullPredicate("a", "Bio", false)] };

        Assert.Contains("a.Bio == null", ArangoTraversalEmitter.Emit(Model, isNull).Statement);
        Assert.Contains("a.Bio != null", ArangoTraversalEmitter.Emit(Model, isNotNull).Statement);
    }

    [Fact(DisplayName = "Emit composes And/Or/Not predicates with parenthesised AQL boolean logic")]
    public void EmitBooleanComposition()
    {
        PropertyComparisonPredicate left = new("a", "Name", ComparisonOperator.Equal, "Ada");
        PropertyComparisonPredicate right = new("a", "Name", ComparisonOperator.NotEqual, "Bob");
        TraversalAst and = NodeOnly() with { Predicates = [new AndPredicate(left, right)] };
        TraversalAst or = NodeOnly() with { Predicates = [new OrPredicate(left, right)] };
        TraversalAst not = NodeOnly() with { Predicates = [new NotPredicate(left)] };

        Assert.Contains("(a.Name == @p0 AND a.Name != @p1)", ArangoTraversalEmitter.Emit(Model, and).Statement);
        Assert.Contains("(a.Name == @p0 OR a.Name != @p1)", ArangoTraversalEmitter.Emit(Model, or).Statement);
        Assert.Contains("NOT (a.Name == @p0)", ArangoTraversalEmitter.Emit(Model, not).Statement);
    }

    [Fact(DisplayName = "Emit lowers OrderBy / OrderByDescending to AQL SORT clauses in registration order")]
    public void EmitOrderings()
    {
        TraversalAst ast = NodeOnly() with
        {
            Orderings =
            [
                new TraversalOrdering("a", "Name", OrderDirection.Ascending),
                new TraversalOrdering("a", "JoinedAt", OrderDirection.Descending)
            ]
        };

        DataStatement statement = ArangoTraversalEmitter.Emit(Model, ast);

        Assert.Contains("SORT a.Name ASC, a.JoinedAt DESC", statement.Statement);
    }

    [Fact(DisplayName = "Emit lowers Skip + Take to a 'LIMIT @skip, @take' clause")]
    public void EmitSkipTake()
    {
        TraversalAst takeOnly = NodeOnly() with { Take = 5 };
        TraversalAst both = NodeOnly() with { Skip = 10, Take = 5 };

        DataStatement takeStatement = ArangoTraversalEmitter.Emit(Model, takeOnly);
        DataStatement bothStatement = ArangoTraversalEmitter.Emit(Model, both);

        Assert.Contains("LIMIT @p0", takeStatement.Statement);
        Assert.Equal(5L, takeStatement.Parameters["p0"]);

        Assert.Contains("LIMIT @p0, @p1", bothStatement.Statement);
        Assert.Equal(10L, bothStatement.Parameters["p0"]);
        Assert.Equal(5L, bothStatement.Parameters["p1"]);
    }

    [Fact(DisplayName = "Emit Create on a 3-segment pattern produces an INSERT against the edge collection")]
    public void EmitCreateRelationship()
    {
        TraversalAst ast = AuthorWroteToPost("w") with { Kind = TraversalKind.Create };

        DataStatement statement = ArangoTraversalEmitter.Emit(Model, ast);

        Assert.Contains("INSERT { _from: a._id, _to: p._id } INTO @@", statement.Statement);
        Assert.Contains("RETURN { w: NEW }", statement.Statement);
    }

    [Fact(DisplayName = "Emit Merge on a 3-segment pattern produces an UPSERT against the edge collection")]
    public void EmitMergeRelationship()
    {
        TraversalAst ast = AuthorWroteToPost("w") with { Kind = TraversalKind.Merge };

        DataStatement statement = ArangoTraversalEmitter.Emit(Model, ast);

        Assert.Contains("UPSERT { _from: a._id, _to: p._id }", statement.Statement);
        Assert.Contains("INSERT { _from: a._id, _to: p._id }", statement.Statement);
        Assert.Contains("UPDATE {} IN @@", statement.Statement);
    }

    [Fact(DisplayName = "Emit Create throws when the pattern is not exactly Node-Rel-Node")]
    public void EmitCreateOnSingleNodeThrows()
    {
        TraversalAst ast = NodeOnly() with { Kind = TraversalKind.Create };

        Assert.Throws<StatementEmissionException>(() => ArangoTraversalEmitter.Emit(Model, ast));
    }

    [Fact(DisplayName = "Emit throws when the first segment is not a node")]
    public void EmitThrowsOnRelationshipFirst()
    {
        TraversalAst ast = new(
            TraversalKind.Match,
            [new RelationshipPatternSegment(typeof(Wrote), "w", RelationshipDirection.Outgoing)],
            [],
            "w");

        Assert.Throws<StatementEmissionException>(() => ArangoTraversalEmitter.Emit(Model, ast));
    }
}
