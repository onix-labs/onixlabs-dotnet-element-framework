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

namespace OnixLabs.ElementFramework.UnitTests;

public class GraphTraversalTests
{
    private readonly GraphModel model = TestModel.Build();
    private readonly FakeTraversalTranslator translator = new();

    private GraphTraversal NewTraversal() => new(model, translator);

    [Fact(DisplayName = "Match begins a traversal with kind Match")]
    public void MatchBeginsMatchTraversal()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").Return<Author>("a");

        Assert.NotNull(translator.LastAst);
        Assert.Equal(TraversalKind.Match, translator.LastAst!.Kind);
    }

    [Fact(DisplayName = "Merge begins a traversal with kind Merge")]
    public void MergeBeginsMergeTraversal()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Merge().Node<Author>("a").Return<Author>("a");

        Assert.Equal(TraversalKind.Merge, translator.LastAst!.Kind);
    }

    [Fact(DisplayName = "Create begins a traversal with kind Create")]
    public void CreateBeginsCreateTraversal()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Create().Node<Author>("a").Return<Author>("a");

        Assert.Equal(TraversalKind.Create, translator.LastAst!.Kind);
    }

    [Fact(DisplayName = "Single-node match emits a single NodePatternSegment")]
    public void SingleNodeMatchEmitsOneSegment()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").Return<Author>("a");

        TraversalAst ast = translator.LastAst!;
        Assert.Single(ast.Segments);
        NodePatternSegment node = Assert.IsType<NodePatternSegment>(ast.Segments[0]);
        Assert.Equal(typeof(Author), node.NodeType);
        Assert.Equal("a", node.Alias);
        Assert.Equal("a", ast.ReturnAlias);
        Assert.Empty(ast.Predicates);
    }

    [Fact(DisplayName = "RelatedBy + Outgoing + To records relationship and end-node segments in order")]
    public void RelationshipChainBuildsOrderedSegments()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w").Outgoing().To("p")
            .Return<Post>("p");

        TraversalAst ast = translator.LastAst!;
        Assert.Equal(3, ast.Segments.Count);

        NodePatternSegment startNode = Assert.IsType<NodePatternSegment>(ast.Segments[0]);
        Assert.Equal(typeof(Author), startNode.NodeType);
        Assert.Equal("a", startNode.Alias);

        RelationshipPatternSegment edge = Assert.IsType<RelationshipPatternSegment>(ast.Segments[1]);
        Assert.Equal(typeof(Wrote), edge.EdgeType);
        Assert.Equal("w", edge.Alias);
        Assert.Equal(RelationshipDirection.Outgoing, edge.Direction);

        NodePatternSegment endNode = Assert.IsType<NodePatternSegment>(ast.Segments[2]);
        Assert.Equal(typeof(Post), endNode.NodeType);
        Assert.Equal("p", endNode.Alias);

        Assert.Equal("p", ast.ReturnAlias);
    }

    [Fact(DisplayName = "Incoming pins direction Incoming on the relationship segment")]
    public void IncomingPinsIncomingDirection()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w").Incoming().To("p")
            .Return<Author>("a");

        RelationshipPatternSegment edge = Assert.IsType<RelationshipPatternSegment>(translator.LastAst!.Segments[1]);
        Assert.Equal(RelationshipDirection.Incoming, edge.Direction);
    }

    [Fact(DisplayName = "Either pins direction Either on the relationship segment")]
    public void EitherPinsEitherDirection()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w").Either().To("p")
            .Return<Author>("a");

        RelationshipPatternSegment edge = Assert.IsType<RelationshipPatternSegment>(translator.LastAst!.Segments[1]);
        Assert.Equal(RelationshipDirection.Either, edge.Direction);
    }

    [Fact(DisplayName = "RelatedBy accepts the model's natural endpoint orientation")]
    public void RelatedByAcceptsNaturalOrientation()
    {
        GraphTraversal traversal = NewTraversal();

        // Wrote is registered Author -> Post; pose it forward.
        traversal.Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w").Outgoing().To("p")
            .Return<Author>("a");

        Assert.Equal(typeof(Wrote), Assert.IsType<RelationshipPatternSegment>(translator.LastAst!.Segments[1]).EdgeType);
    }

    [Fact(DisplayName = "RelatedBy accepts the model's reversed endpoint orientation")]
    public void RelatedByAcceptsReversedOrientation()
    {
        GraphTraversal traversal = NewTraversal();

        // Wrote is registered Author -> Post; pose it from the Post side.
        traversal.Match()
            .Node<Post>("p")
            .RelatedBy<Wrote, Author>("w").Incoming().To("a")
            .Return<Post>("p");

        Assert.Equal(typeof(Wrote), Assert.IsType<RelationshipPatternSegment>(translator.LastAst!.Segments[1]).EdgeType);
    }

    [Fact(DisplayName = "RelatedBy throws ModelConfigurationException when endpoint types do not match the registered relationship")]
    public void RelatedByThrowsOnEndpointMismatch()
    {
        GraphTraversal traversal = NewTraversal();

        Assert.Throws<ModelConfigurationException>(() => traversal.Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Comment>("w"));
    }

    [Fact(DisplayName = "RelatedBy throws ModelConfigurationException when the edge type is not registered in the model")]
    public void RelatedByThrowsOnUnregisteredEdge()
    {
        GraphTraversal traversal = NewTraversal();

        Assert.Throws<ModelConfigurationException>(() => traversal.Match()
            .Node<Author>("a")
            .RelatedBy<Unregistered, Post>("u"));
    }

    [Fact(DisplayName = "Where accumulates a property-equality predicate scoped to the bound alias")]
    public void WhereAccumulatesPredicate()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match()
            .Node<Author>("a").Where(a => a.Name == "Alice")
            .Return<Author>("a");

        TraversalAst ast = translator.LastAst!;
        TraversalPredicate predicate = Assert.Single(ast.Predicates);
        PropertyComparisonPredicate comparison = Assert.IsType<PropertyComparisonPredicate>(predicate);
        Assert.Equal("a", comparison.Alias);
        Assert.Equal(nameof(Author.Name), comparison.ClrPropertyName);
        Assert.Equal(ComparisonOperator.Equal, comparison.Operator);
        Assert.Equal("Alice", comparison.Value);
    }

    [Fact(DisplayName = "Where chains accumulate multiple predicates in order")]
    public void WhereChainsAccumulateMultiplePredicates()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match()
            .Node<Author>("a")
                .Where(a => a.Name == "Alice")
                .Where(a => a.Id == Guid.Empty)
            .Return<Author>("a");

        TraversalAst ast = translator.LastAst!;
        Assert.Equal(2, ast.Predicates.Count);
        Assert.Equal(nameof(Author.Name), Assert.IsType<PropertyComparisonPredicate>(ast.Predicates[0]).ClrPropertyName);
        Assert.Equal(nameof(Author.Id), Assert.IsType<PropertyComparisonPredicate>(ast.Predicates[1]).ClrPropertyName);
    }

    [Fact(DisplayName = "Node throws ArgumentException when the alias is null or whitespace")]
    public void NodeThrowsForBlankAlias()
    {
        GraphTraversal traversal = NewTraversal();

        Assert.Throws<ArgumentException>(() => traversal.Match().Node<Author>(""));
    }

    [Fact(DisplayName = "Node throws ModelConfigurationException when the node type is not registered")]
    public void NodeThrowsForUnregisteredType()
    {
        GraphTraversal traversal = NewTraversal();

        Assert.Throws<ModelConfigurationException>(() => traversal.Match().Node<Unregistered>("u"));
    }

    [Fact(DisplayName = "Where throws ArgumentNullException when the predicate is null")]
    public void WhereThrowsForNullPredicate()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a");

        Assert.Throws<ArgumentNullException>(() => node.Where(null!));
    }

    [Fact(DisplayName = "Return forwards the materialized rows produced by the translator")]
    public void ReturnForwardsTranslatorRows()
    {
        GraphTraversal traversal = NewTraversal();
        Author alice = Author.Create("Alice");
        translator.OnTranslate = _ => [alice];

        IEnumerable<Author> rows = traversal.Match().Node<Author>("a").Return<Author>("a");

        Assert.Same(alice, Assert.Single(rows));
    }

    [Fact(DisplayName = "ReturnAsync forwards the materialized rows produced by the translator")]
    public async Task ReturnAsyncForwardsTranslatorRows()
    {
        GraphTraversal traversal = NewTraversal();
        Author alice = Author.Create("Alice");
        translator.OnTranslate = _ => [alice];

        List<Author> collected = [];
        await foreach (Author author in traversal.Match().Node<Author>("a").ReturnAsync<Author>("a"))
            collected.Add(author);

        Assert.Same(alice, Assert.Single(collected));
    }

    [Fact(DisplayName = "OrderBy records an ascending TraversalOrdering scoped to the bound alias")]
    public void OrderByRecordsAscendingOrdering()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").OrderBy(a => a.Name).Return<Author>("a");

        TraversalOrdering ordering = Assert.Single(translator.LastAst!.Orderings);
        Assert.Equal("a", ordering.Alias);
        Assert.Equal(nameof(Author.Name), ordering.ClrPropertyName);
        Assert.Equal(OrderDirection.Ascending, ordering.Direction);
    }

    [Fact(DisplayName = "OrderByDescending records a descending TraversalOrdering")]
    public void OrderByDescendingRecordsDescendingOrdering()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").OrderByDescending(a => a.Name).Return<Author>("a");

        Assert.Equal(OrderDirection.Descending, Assert.Single(translator.LastAst!.Orderings).Direction);
    }

    [Fact(DisplayName = "OrderBy unwraps a Convert wrapper around a value-type property access")]
    public void OrderByUnwrapsConvertForValueType()
    {
        GraphTraversal traversal = NewTraversal();

        // Selecting Guid via object boxes the access through a Convert node.
        traversal.Match().Node<Author>("a").OrderBy<object>(a => a.Id).Return<Author>("a");

        Assert.Equal(nameof(Author.Id), Assert.Single(translator.LastAst!.Orderings).ClrPropertyName);
    }

    [Fact(DisplayName = "OrderBy throws InvalidOperationException when an ordering is already applied")]
    public void OrderByThrowsOnDuplicate()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a").OrderBy(a => a.Name);

        Assert.Throws<InvalidOperationException>(() => node.OrderBy(a => a.Name));
    }

    [Fact(DisplayName = "OrderBy and OrderByDescending are mutually exclusive on the same traversal")]
    public void OrderByDescendingThrowsAfterOrderBy()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a").OrderBy(a => a.Name);

        Assert.Throws<InvalidOperationException>(() => node.OrderByDescending(a => a.Name));
    }

    [Fact(DisplayName = "OrderBy throws ArgumentNullException when the selector is null")]
    public void OrderByThrowsForNullSelector()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a");

        Assert.Throws<ArgumentNullException>(() => node.OrderBy<string>(null!));
    }

    [Fact(DisplayName = "OrderBy throws NotSupportedException when the selector is not a single property access")]
    public void OrderByThrowsForCompoundSelector()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a");

        Assert.Throws<NotSupportedException>(() => node.OrderBy(a => a.Name.Length));
    }

    [Fact(DisplayName = "Skip records the supplied row count on the AST")]
    public void SkipRecordsCountOnAst()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").Skip(10).Return<Author>("a");

        Assert.Equal(10, translator.LastAst!.Skip);
    }

    [Fact(DisplayName = "Take records the supplied row count on the AST")]
    public void TakeRecordsCountOnAst()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").Take(25).Return<Author>("a");

        Assert.Equal(25, translator.LastAst!.Take);
    }

    [Fact(DisplayName = "Skip throws ArgumentOutOfRangeException when count is negative")]
    public void SkipThrowsForNegativeCount()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a");

        Assert.Throws<ArgumentOutOfRangeException>(() => node.Skip(-1));
    }

    [Fact(DisplayName = "Take throws ArgumentOutOfRangeException when count is negative")]
    public void TakeThrowsForNegativeCount()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a");

        Assert.Throws<ArgumentOutOfRangeException>(() => node.Take(-1));
    }

    [Fact(DisplayName = "Skip throws InvalidOperationException when already applied")]
    public void SkipThrowsOnDuplicate()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a").Skip(1);

        Assert.Throws<InvalidOperationException>(() => node.Skip(2));
    }

    [Fact(DisplayName = "Take throws InvalidOperationException when already applied")]
    public void TakeThrowsOnDuplicate()
    {
        GraphTraversal traversal = NewTraversal();
        IPatternNode<Author> node = traversal.Match().Node<Author>("a").Take(1);

        Assert.Throws<InvalidOperationException>(() => node.Take(2));
    }

    [Fact(DisplayName = "OrderBy / Skip / Take can chain together before Return")]
    public void OrderingChainsWithSkipAndTake()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match()
            .Node<Author>("a")
            .OrderBy(a => a.Name)
            .Skip(5)
            .Take(10)
            .Return<Author>("a");

        TraversalAst ast = translator.LastAst!;
        Assert.Single(ast.Orderings);
        Assert.Equal(5, ast.Skip);
        Assert.Equal(10, ast.Take);
    }

    [Fact(DisplayName = "AST defaults Orderings to empty, Skip to null, Take to null when none are applied")]
    public void TailClausesDefaultToEmptyWhenAbsent()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").Return<Author>("a");

        TraversalAst ast = translator.LastAst!;
        Assert.Empty(ast.Orderings);
        Assert.Null(ast.Skip);
        Assert.Null(ast.Take);
    }

    [Fact(DisplayName = "Each Match/Merge/Create call allocates an independent traversal state")]
    public void EachEntryAllocatesIndependentState()
    {
        GraphTraversal traversal = NewTraversal();

        traversal.Match().Node<Author>("a").Where(a => a.Name == "Alice").Return<Author>("a");
        TraversalAst first = translator.LastAst!;

        traversal.Match().Node<Post>("p").Return<Post>("p");
        TraversalAst second = translator.LastAst!;

        Assert.NotSame(first, second);
        Assert.Single(first.Predicates);
        Assert.Empty(second.Predicates);
        Assert.Equal(typeof(Author), Assert.IsType<NodePatternSegment>(first.Segments[0]).NodeType);
        Assert.Equal(typeof(Post), Assert.IsType<NodePatternSegment>(second.Segments[0]).NodeType);
    }
}
