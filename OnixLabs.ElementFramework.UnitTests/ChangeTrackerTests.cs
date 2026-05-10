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

using Microsoft.Extensions.Logging.Abstractions;

namespace OnixLabs.ElementFramework.UnitTests;

public class ChangeTrackerTests
{
    private readonly GraphModel model = TestModel.Build();
    private readonly FakeStatementEmitter emitter = new();
    private readonly FakeRawStatementExecutor executor = new();
    private readonly FakeGraphTransactionOpener opener = new();

    private ChangeTracker NewTracker() => new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);

    [Fact(DisplayName = "TrackAdd stages an Add operation and records the node in the identity map")]
    public void TrackAddStagesAndIndexes()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");

        tracker.TrackAdd(author);

        Assert.NotNull(tracker.Find<Author>(author.Id));

        int flushed = tracker.Flush();

        Assert.Equal(1, flushed);
        Assert.Single(executor.Executed);
        Assert.StartsWith("ADD ", executor.Executed[0]);
    }

    [Fact(DisplayName = "TrackUpdate stages an Update and overwrites the identity map entry")]
    public void TrackUpdateOverwrites()
    {
        ChangeTracker tracker = NewTracker();
        Guid id = Guid.NewGuid();
        Author original = new() { Id = id, Name = "Alice", JoinedAt = DateTimeOffset.UtcNow };
        Author replacement = new() { Id = id, Name = "Bob", JoinedAt = DateTimeOffset.UtcNow };

        tracker.TrackAdd(original);
        tracker.TrackUpdate(replacement);

        Author? found = tracker.Find<Author>(id);
        Assert.Same(replacement, found);
    }

    [Fact(DisplayName = "TrackRemove drops the node from the identity map")]
    public void TrackRemoveDropsFromIdentityMap()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");

        tracker.TrackAdd(author);
        Assert.NotNull(tracker.Find<Author>(author.Id));

        tracker.TrackRemove(author);

        Assert.Null(tracker.Find<Author>(author.Id));
    }

    [Fact(DisplayName = "TrackMerge stages a Merge and records the node in the identity map")]
    public void TrackMergeStagesAndIndexes()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");

        tracker.TrackMerge(author);

        Assert.NotNull(tracker.Find<Author>(author.Id));
    }

    [Fact(DisplayName = "Tracking a node whose type has no key registered throws ModelConfigurationException")]
    public void TrackingNoKeyTypeThrows()
    {
        ChangeTracker tracker = NewTracker();

        Assert.Throws<ModelConfigurationException>(() => tracker.TrackAdd(new Unregistered()));
    }

    [Fact(DisplayName = "Tracking a node with a default value-type key succeeds (default Guid is a valid key value)")]
    public void TrackingDefaultGuidKeySucceeds()
    {
        ChangeTracker tracker = NewTracker();
        Post post = new() { Id = default, Title = "x", Body = "y", PublishedAt = DateTimeOffset.UtcNow };

        tracker.TrackAdd(post);

        Assert.NotNull(tracker.Find<Post>(default(Guid)));
    }

    [Fact(DisplayName = "Tracking a node with a null reference-type key value throws GraphContextException")]
    public void TrackingNullReferenceKeyValueThrows()
    {
        GraphModelBuilder builder = new();
        builder.Node<StringKeyed>().HasKey(s => s.Identifier!);
        GraphModel stringModel = builder.Build();

        ChangeTracker stringTracker = new(stringModel, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        StringKeyed instance = new() { Identifier = null, Body = "x" };

        GraphContextException exception = Assert.Throws<GraphContextException>(() => stringTracker.TrackAdd(instance));
        Assert.Contains("null key value", exception.Message);
    }

    [Fact(DisplayName = "TrackConnect stages a Connect statement once flushed")]
    public void TrackConnectStages()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hi", "Body");
        Wrote edge = new() { WrittenAt = DateTimeOffset.UtcNow };

        tracker.TrackConnect(author, edge, post);
        int flushed = tracker.Flush();

        Assert.Equal(1, flushed);
        Assert.Single(executor.Executed);
        Assert.Equal("CONNECT Wrote", executor.Executed[0]);
    }

    [Fact(DisplayName = "TrackDisconnect stages a Disconnect statement once flushed")]
    public void TrackDisconnectStages()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hi", "Body");

        tracker.TrackDisconnect<Author, Wrote, Post>(author, post);
        int flushed = tracker.Flush();

        Assert.Equal(1, flushed);
        Assert.Single(executor.Executed);
        Assert.Equal("DISCONNECT Wrote", executor.Executed[0]);
    }

    [Fact(DisplayName = "Find returns null for a key that was never tracked")]
    public void FindReturnsNullForUnknownKey()
    {
        ChangeTracker tracker = NewTracker();

        Assert.Null(tracker.Find<Author>(Guid.NewGuid()));
    }

    [Fact(DisplayName = "Reset clears the identity map")]
    public void ResetClearsIdentityMap()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");
        tracker.TrackAdd(author);
        Assert.NotNull(tracker.Find<Author>(author.Id));

        tracker.Reset();

        Assert.Null(tracker.Find<Author>(author.Id));
    }

    [Fact(DisplayName = "Reset clears the pending operation queue")]
    public void ResetClearsPendingQueue()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(Author.Create("Alice"));

        tracker.Reset();
        int flushed = tracker.Flush();

        Assert.Equal(0, flushed);
        Assert.Empty(executor.Executed);
    }

    [Fact(DisplayName = "Attach indexes the supplied instance without staging an operation")]
    public void AttachIndexesWithoutStaging()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");

        tracker.Attach(author);

        Assert.NotNull(tracker.Find<Author>(author.Id));

        int flushed = tracker.Flush();
        Assert.Equal(0, flushed);
        Assert.Empty(executor.Executed);
    }

    [Fact(DisplayName = "Attach is idempotent for the same reference")]
    public void AttachIsIdempotentForSameReference()
    {
        ChangeTracker tracker = NewTracker();
        Author author = Author.Create("Alice");

        tracker.Attach(author);
        tracker.Attach(author);

        Assert.Same(author, tracker.Find<Author>(author.Id));
    }

    [Fact(DisplayName = "Attach throws GraphContextException when a different instance with the same key is already tracked")]
    public void AttachThrowsOnDifferentInstanceSameKey()
    {
        ChangeTracker tracker = NewTracker();
        Guid id = Guid.NewGuid();
        Author first = new() { Id = id, Name = "Alice", JoinedAt = DateTimeOffset.UtcNow };
        Author second = new() { Id = id, Name = "Alice (copy)", JoinedAt = DateTimeOffset.UtcNow };

        tracker.Attach(first);

        Assert.Throws<GraphContextException>(() => tracker.Attach(second));
    }

    [Fact(DisplayName = "Flush with no pending operations returns 0 and does not open a transaction")]
    public void FlushWithNoPendingReturnsZero()
    {
        ChangeTracker tracker = NewTracker();

        int flushed = tracker.Flush();

        Assert.Equal(0, flushed);
        Assert.Empty(opener.Events);
    }

    [Fact(DisplayName = "Flush returns the count of successful operations on the happy path")]
    public void FlushHappyPathReturnsCount()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));
        tracker.TrackAdd(Author.Create("Carol"));

        int flushed = tracker.Flush();

        Assert.Equal(3, flushed);
        Assert.Equal(3, executor.Executed.Count);
    }

    [Fact(DisplayName = "FlushAsync returns the count of successful operations on the happy path")]
    public async Task FlushAsyncHappyPathReturnsCount()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));

        int flushed = await tracker.FlushAsync();

        Assert.Equal(2, flushed);
        Assert.Equal(2, executor.Executed.Count);
    }

    [Fact(DisplayName = "Flush auto-opens an ambient transaction, commits on full success, and clears pending")]
    public void FlushAutoOpensAndCommits()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));

        int flushed = tracker.Flush();

        Assert.Equal(2, flushed);
        Assert.Equal(["Open", "Commit", "Dispose"], opener.Events);

        int retry = tracker.Flush();
        Assert.Equal(0, retry);
        Assert.Equal(["Open", "Commit", "Dispose"], opener.Events);
    }

    [Fact(DisplayName = "Flush partial failure rolls back the auto-opened transaction and preserves pending for retry")]
    public void FlushPartialFailureRollsBackAndPreservesPending()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));
        tracker.TrackAdd(Author.Create("Carol"));

        int callCount = 0;
        executor.OnExecute = _ =>
        {
            callCount++;
            if (callCount == 2) throw new InvalidOperationException("op 2 failed");
            return [];
        };

        Assert.Throws<InvalidOperationException>(() => tracker.Flush());
        Assert.Equal(2, executor.Executed.Count);
        Assert.Equal(["Open", "Rollback", "Dispose"], opener.Events);

        executor.OnExecute = null;
        int retry = tracker.Flush();
        Assert.Equal(3, retry);
        Assert.Equal(5, executor.Executed.Count);
        Assert.Equal(["Open", "Rollback", "Dispose", "Open", "Commit", "Dispose"], opener.Events);
    }

    [Fact(DisplayName = "Flush honours an existing ambient transaction without opening or committing one of its own")]
    public void FlushHonoursExistingAmbientTransaction()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));

        IGraphTransaction ambient = opener.Open();
        Assert.Same(ambient, opener.Active);

        int flushed = tracker.Flush();

        Assert.Equal(2, flushed);
        Assert.Equal(["Open"], opener.Events);
    }

    [Fact(DisplayName = "Flush within an existing ambient transaction preserves pending on failure (consumer owns rollback)")]
    public void FlushWithinAmbientTransactionPreservesPendingOnFailure()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));

        opener.Open();

        int callCount = 0;
        executor.OnExecute = _ =>
        {
            callCount++;
            if (callCount == 2) throw new InvalidOperationException("op 2 failed");
            return [];
        };

        Assert.Throws<InvalidOperationException>(() => tracker.Flush());
        Assert.Equal(["Open"], opener.Events);

        executor.OnExecute = null;
        int retry = tracker.Flush();
        Assert.Equal(2, retry);
        Assert.Equal(4, executor.Executed.Count);
    }
}
