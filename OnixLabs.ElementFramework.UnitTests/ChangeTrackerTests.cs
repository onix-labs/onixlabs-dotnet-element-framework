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

public class ChangeTrackerTests
{
    private readonly GraphModel model = TestModel.Build();
    private readonly FakeStatementEmitter emitter = new();
    private readonly FakeRawStatementExecutor executor = new();
    private readonly FakeGraphTransactionOpener opener = new();

    private ChangeTracker NewTracker() => new(model, emitter, executor, opener);

    [Fact(DisplayName = "TrackAdd stages an Add operation and records the node in the identity map")]
    public void TrackAddStagesAndIndexes()
    {
        ChangeTracker tracker = NewTracker();
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };

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
        Author original = new() { Id = id, Name = "Alice" };
        Author replacement = new() { Id = id, Name = "Bob" };

        tracker.TrackAdd(original);
        tracker.TrackUpdate(replacement);

        Author? found = tracker.Find<Author>(id);
        Assert.Same(replacement, found);
    }

    [Fact(DisplayName = "TrackRemove drops the node from the identity map")]
    public void TrackRemoveDropsFromIdentityMap()
    {
        ChangeTracker tracker = NewTracker();
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };

        tracker.TrackAdd(author);
        Assert.NotNull(tracker.Find<Author>(author.Id));

        tracker.TrackRemove(author);

        Assert.Null(tracker.Find<Author>(author.Id));
    }

    [Fact(DisplayName = "TrackMerge stages a Merge and records the node in the identity map")]
    public void TrackMergeStagesAndIndexes()
    {
        ChangeTracker tracker = NewTracker();
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };

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
        Post post = new() { Id = default, Title = "x" };

        tracker.TrackAdd(post);

        Assert.NotNull(tracker.Find<Post>(default(Guid)));
    }

    [Fact(DisplayName = "Tracking a node with a null reference-type key value throws GraphContextException")]
    public void TrackingNullReferenceKeyValueThrows()
    {
        GraphModelBuilder builder = new();
        builder.Node<StringKeyed>().HasKey(s => s.Identifier!);
        GraphModel stringModel = builder.Build();

        ChangeTracker stringTracker = new(stringModel, emitter, executor, opener);
        StringKeyed instance = new() { Identifier = null, Body = "x" };

        GraphContextException exception = Assert.Throws<GraphContextException>(() => stringTracker.TrackAdd(instance));
        Assert.Contains("null key value", exception.Message);
    }

    [Fact(DisplayName = "TrackConnect stages a Connect statement once flushed")]
    public void TrackConnectStages()
    {
        ChangeTracker tracker = NewTracker();
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };
        Post post = new() { Id = Guid.NewGuid(), Title = "Hi" };
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
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };
        Post post = new() { Id = Guid.NewGuid(), Title = "Hi" };

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
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };
        tracker.TrackAdd(author);
        Assert.NotNull(tracker.Find<Author>(author.Id));

        tracker.Reset();

        Assert.Null(tracker.Find<Author>(author.Id));
    }

    [Fact(DisplayName = "Reset clears the pending operation queue")]
    public void ResetClearsPendingQueue()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Alice" });

        tracker.Reset();
        int flushed = tracker.Flush();

        Assert.Equal(0, flushed);
        Assert.Empty(executor.Executed);
    }

    [Fact(DisplayName = "Attach indexes the supplied instance without staging an operation")]
    public void AttachIndexesWithoutStaging()
    {
        ChangeTracker tracker = NewTracker();
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };

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
        Author author = new() { Id = Guid.NewGuid(), Name = "Alice" };

        tracker.Attach(author);
        tracker.Attach(author);

        Assert.Same(author, tracker.Find<Author>(author.Id));
    }

    [Fact(DisplayName = "Attach throws GraphContextException when a different instance with the same key is already tracked")]
    public void AttachThrowsOnDifferentInstanceSameKey()
    {
        ChangeTracker tracker = NewTracker();
        Guid id = Guid.NewGuid();
        Author first = new() { Id = id, Name = "Alice" };
        Author second = new() { Id = id, Name = "Alice (copy)" };

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
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Alice" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Bob" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Carol" });

        int flushed = tracker.Flush();

        Assert.Equal(3, flushed);
        Assert.Equal(3, executor.Executed.Count);
    }

    [Fact(DisplayName = "FlushAsync returns the count of successful operations on the happy path")]
    public async Task FlushAsyncHappyPathReturnsCount()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Alice" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Bob" });

        int flushed = await tracker.FlushAsync();

        Assert.Equal(2, flushed);
        Assert.Equal(2, executor.Executed.Count);
    }

    [Fact(DisplayName = "Flush auto-opens an ambient transaction, commits on full success, and clears pending")]
    public void FlushAutoOpensAndCommits()
    {
        ChangeTracker tracker = NewTracker();
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Alice" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Bob" });

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
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Alice" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Bob" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Carol" });

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
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Alice" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Bob" });

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
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Alice" });
        tracker.TrackAdd(new Author { Id = Guid.NewGuid(), Name = "Bob" });

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
