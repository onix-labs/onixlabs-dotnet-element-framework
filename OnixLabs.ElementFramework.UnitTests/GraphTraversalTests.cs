// All Rights Reserved License
//
// 1. Grant of License
// Subject to the terms and conditions of this License, ONIXLabs ("Licensor") hereby grants to you a limited, non-exclusive, non-transferable, non-sublicensable license to use the Software for commercial, private, and paid purposes. This license does not include any rights to modify, distribute, or create derivative works of the Software.
//
// 2. Permitted Uses
// You are permitted to:
//  - Use the Software for commercial purposes.
//  - Use the Software for private purposes.
//  - Use the Software for paid purposes.
//  - Exercise any patent rights associated with the Software, solely in connection with your use of the Software as permitted under this License.
//
// 3. Restrictions
// You are not permitted to:
//  - Modify, alter, or create any derivative works of the Software.
//  - Distribute, sublicense, lease, rent, or otherwise transfer the Software to any third party.
//  - Use the Software without obtaining a proper license for paid use.
//  - Use the Software in any way that infringes upon the trademarks, service marks, or trade names of the Licensor.
//  - Use the Software in any manner that could cause it to be considered open-source software or otherwise subject to an open-source license.
//
// 4. No Free Use
// This license does not permit any free use of the Software. Any use of the Software without a paid license is strictly prohibited.
//
// 5. No Liability
// To the maximum extent permitted by applicable law, the Software is provided "as is" and "as available" without warranty of any kind, express or implied, including but not limited to the implied warranties of merchantability, fitness for a particular purpose, and non-infringement. In no event shall the Licensor be liable for any damages whatsoever arising out of the use of or inability to use the Software, even if the Licensor has been advised of the possibility of such damages.
//
// 6. No Warranty
// The Licensor makes no warranty that the Software will meet your requirements, be uninterrupted, secure, or error-free. The Licensor disclaims all warranties with respect to the Software, whether express or implied, including but not limited to any warranties of merchantability, fitness for a particular purpose, and non-infringement.
//
// 7. Termination
// This license is effective until terminated. Your rights under this license will terminate automatically without notice if you fail to comply with any term of this license. Upon termination, you must immediately cease all use of the Software and destroy all copies of the Software in your possession or control.
//
// 8. Governing Law
// This license will be governed by and construed in accordance with the laws of [Your Jurisdiction], without regard to its conflict of laws principles.
//
// 9. Entire Agreement
// This license constitutes the entire agreement between you and the Licensor concerning the Software and supersedes all prior or contemporaneous communications, agreements, or understandings, whether oral or written, concerning the subject matter hereof.
//
// By using the Software, you acknowledge that you have read and understood this license and agree to be bound by its terms and conditions.

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
