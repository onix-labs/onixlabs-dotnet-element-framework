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

namespace OnixLabs.ElementFramework.UnitTests;

[Collection("Diagnostics-sensitive")]
public class GraphContextClearChangesTests
{
    private readonly FakeRawStatementExecutor executor = new();
    private readonly FakeGraphTransactionOpener opener = new();
    private readonly BlogGraphContext context;

    public GraphContextClearChangesTests()
    {
        ServiceCollection services = new();
        services.AddGraphContext<BlogGraphContext>(builder => builder
            .UseStatementEmitter(new FakeStatementEmitter())
            .UseResultMaterializer(new FakeResultMaterializer())
            .UseRawStatementExecutor(executor)
            .UseGraphTransactionOpener(opener)
            .UseTraversalTranslator(new FakeTraversalTranslator()));
        context = services.BuildServiceProvider().GetRequiredService<BlogGraphContext>();
    }

    /// <summary>
    /// Simulates a permanent store-side failure (for example, a constraint violation) for every author write.
    /// </summary>
    private void FailAuthorWrites() => executor.OnExecute = statement =>
        statement.StartsWith("ADD Author") ? throw new InvalidOperationException("constraint violated") : [];

    [Fact(DisplayName = "SaveChanges after a failed save replays the failed batch and fails again")]
    public void SaveChangesAfterFailedSaveReplaysAndFailsAgain()
    {
        FailAuthorWrites();
        context.Nodes<Author>().Add(Author.Create("Alice"));

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

        Assert.Equal(["ADD Author", "ADD Author"], executor.Executed);
    }

    [Fact(DisplayName = "ClearChanges after a failed save discards the failed batch so the next save writes nothing")]
    public void ClearChangesAfterFailedSaveDiscardsBatch()
    {
        FailAuthorWrites();
        context.Nodes<Author>().Add(Author.Create("Alice"));
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

        context.ClearChanges();
        int written = context.SaveChanges();

        Assert.Equal(0, written);
        Assert.Equal(["ADD Author"], executor.Executed);
    }

    [Fact(DisplayName = "ClearChanges after a failed async save discards the failed batch so the next async save writes nothing")]
    public async Task ClearChangesAfterFailedSaveAsyncDiscardsBatch()
    {
        FailAuthorWrites();
        context.Nodes<Author>().Add(Author.Create("Alice"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        context.ClearChanges();
        int written = await context.SaveChangesAsync();

        Assert.Equal(0, written);
        Assert.Equal(["ADD Author"], executor.Executed);
    }

    [Fact(DisplayName = "ClearChanges after a failed save allows new work to be saved on its own")]
    public void ClearChangesAfterFailedSaveAllowsNewWork()
    {
        FailAuthorWrites();
        context.Nodes<Author>().Add(Author.Create("Alice"));
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

        context.ClearChanges();
        context.Nodes<Post>().Add(Post.Create("Hello", "World"));
        int written = context.SaveChanges();

        Assert.Equal(1, written);
        Assert.Equal(["ADD Author", "ADD Post"], executor.Executed);
    }

    [Fact(DisplayName = "ClearChanges empties the identity map so FindById goes to the store")]
    public void ClearChangesEmptiesIdentityMap()
    {
        Author author = Author.Create("Alice");
        context.Nodes<Author>().Add(author);
        Assert.Same(author, context.Nodes<Author>().FindById(author.Id));
        Assert.Empty(executor.Executed);

        context.ClearChanges();

        Assert.Null(context.Nodes<Author>().FindById(author.Id));
        Assert.Equal(["FIND Author"], executor.Executed);
    }

    [Fact(DisplayName = "ClearChanges within an ambient transaction leaves the transaction open and usable")]
    public void ClearChangesWithinAmbientTransactionLeavesTransactionOpen()
    {
        using IGraphTransaction transaction = context.BeginTransaction();
        FailAuthorWrites();
        context.Nodes<Author>().Add(Author.Create("Alice"));
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

        context.ClearChanges();

        Assert.NotNull(opener.Active);
        Assert.Equal(["Open"], opener.Events);

        context.Nodes<Post>().Add(Post.Create("Hello", "World"));
        Assert.Equal(1, context.SaveChanges());
        transaction.Commit();

        Assert.Equal(["ADD Author", "ADD Post"], executor.Executed);
        Assert.Equal(["Open", "Commit"], opener.Events);
    }
}
