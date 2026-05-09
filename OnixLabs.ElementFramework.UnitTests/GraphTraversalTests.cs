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
        Assert.Equal("a", predicate.Alias);
        Assert.Equal(nameof(Author.Name), predicate.ClrPropertyName);
        Assert.Equal("Alice", predicate.Value);
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
        Assert.Equal(nameof(Author.Name), ast.Predicates[0].ClrPropertyName);
        Assert.Equal(nameof(Author.Id), ast.Predicates[1].ClrPropertyName);
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
        Author alice = new() { Id = Guid.NewGuid(), Name = "Alice" };
        translator.OnTranslate = _ => [alice];

        IEnumerable<Author> rows = traversal.Match().Node<Author>("a").Return<Author>("a");

        Assert.Same(alice, Assert.Single(rows));
    }

    [Fact(DisplayName = "ReturnAsync forwards the materialized rows produced by the translator")]
    public async Task ReturnAsyncForwardsTranslatorRows()
    {
        GraphTraversal traversal = NewTraversal();
        Author alice = new() { Id = Guid.NewGuid(), Name = "Alice" };
        translator.OnTranslate = _ => [alice];

        List<Author> collected = [];
        await foreach (Author author in traversal.Match().Node<Author>("a").ReturnAsync<Author>("a"))
            collected.Add(author);

        Assert.Same(alice, Assert.Single(collected));
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
