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

public class RollbackAwareGraphTransactionTests
{
    [Fact(DisplayName = "Commit delegates to the inner transaction and does not reset the tracker")]
    public void CommitDelegatesAndDoesNotReset()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        transaction.Commit();

        Assert.Equal(1, inner.CommitCount);
        Assert.Equal(0, tracker.ResetCount);
    }

    [Fact(DisplayName = "CommitAsync delegates to the inner transaction and does not reset the tracker")]
    public async Task CommitAsyncDelegatesAndDoesNotReset()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await transaction.CommitAsync();

        Assert.Equal(1, inner.CommitAsyncCount);
        Assert.Equal(0, tracker.ResetCount);
    }

    [Fact(DisplayName = "Rollback resets the tracker after a successful inner rollback")]
    public void RollbackResetsTrackerOnSuccess()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        transaction.Rollback();

        Assert.Equal(1, inner.RollbackCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "Rollback does not reset the tracker when the inner rollback throws")]
    public void RollbackDoesNotResetTrackerOnFailure()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new() { ThrowOnRollback = true };
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        Assert.Throws<InvalidOperationException>(() => transaction.Rollback());

        Assert.Equal(1, inner.RollbackCount);
        Assert.Equal(0, tracker.ResetCount);
    }

    [Fact(DisplayName = "RollbackAsync resets the tracker after a successful inner rollback")]
    public async Task RollbackAsyncResetsTrackerOnSuccess()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await transaction.RollbackAsync();

        Assert.Equal(1, inner.RollbackAsyncCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "RollbackAsync does not reset the tracker when the inner rollback throws")]
    public async Task RollbackAsyncDoesNotResetTrackerOnFailure()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new() { ThrowOnRollback = true };
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await transaction.RollbackAsync());

        Assert.Equal(1, inner.RollbackAsyncCount);
        Assert.Equal(0, tracker.ResetCount);
    }

    [Fact(DisplayName = "Dispose without a prior terminal resets the tracker (inner does best-effort rollback)")]
    public void DisposeWithoutTerminalResetsTracker()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        transaction.Dispose();

        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "DisposeAsync without a prior terminal resets the tracker (inner does best-effort rollback)")]
    public async Task DisposeAsyncWithoutTerminalResetsTracker()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await transaction.DisposeAsync();

        Assert.Equal(1, inner.DisposeAsyncCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "Dispose after a successful Commit does not reset the tracker")]
    public void DisposeAfterSuccessfulCommitDoesNotReset()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        transaction.Commit();
        transaction.Dispose();

        Assert.Equal(1, inner.CommitCount);
        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(0, tracker.ResetCount);
    }

    [Fact(DisplayName = "DisposeAsync after a successful CommitAsync does not reset the tracker")]
    public async Task DisposeAsyncAfterSuccessfulCommitDoesNotReset()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await transaction.CommitAsync();
        await transaction.DisposeAsync();

        Assert.Equal(1, inner.CommitAsyncCount);
        Assert.Equal(1, inner.DisposeAsyncCount);
        Assert.Equal(0, tracker.ResetCount);
    }

    [Fact(DisplayName = "Dispose after a failed Commit resets the tracker (inner does best-effort rollback)")]
    public void DisposeAfterFailedCommitResetsTracker()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new() { ThrowOnCommit = true };
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
        transaction.Dispose();

        Assert.Equal(1, inner.CommitCount);
        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "DisposeAsync after a failed CommitAsync resets the tracker (inner does best-effort rollback)")]
    public async Task DisposeAsyncAfterFailedCommitResetsTracker()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new() { ThrowOnCommit = true };
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await transaction.CommitAsync());
        await transaction.DisposeAsync();

        Assert.Equal(1, inner.CommitAsyncCount);
        Assert.Equal(1, inner.DisposeAsyncCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "Dispose after a successful Rollback does not double-reset the tracker")]
    public void DisposeAfterSuccessfulRollbackDoesNotDoubleReset()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        transaction.Rollback();
        transaction.Dispose();

        Assert.Equal(1, inner.RollbackCount);
        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "DisposeAsync after a successful RollbackAsync does not double-reset the tracker")]
    public async Task DisposeAsyncAfterSuccessfulRollbackDoesNotDoubleReset()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new();
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await transaction.RollbackAsync();
        await transaction.DisposeAsync();

        Assert.Equal(1, inner.RollbackAsyncCount);
        Assert.Equal(1, inner.DisposeAsyncCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "Dispose after a failed Rollback resets the tracker (transaction state is ambiguous; clear stale tracker)")]
    public void DisposeAfterFailedRollbackResetsTracker()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new() { ThrowOnRollback = true };
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        Assert.Throws<InvalidOperationException>(() => transaction.Rollback());
        transaction.Dispose();

        Assert.Equal(1, inner.RollbackCount);
        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact(DisplayName = "DisposeAsync after a failed RollbackAsync resets the tracker (transaction state is ambiguous; clear stale tracker)")]
    public async Task DisposeAsyncAfterFailedRollbackResetsTracker()
    {
        FakeChangeTracker tracker = new();
        FakeInnerTransaction inner = new() { ThrowOnRollback = true };
        RollbackAwareGraphTransaction transaction = new(inner, tracker);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await transaction.RollbackAsync());
        await transaction.DisposeAsync();

        Assert.Equal(1, inner.RollbackAsyncCount);
        Assert.Equal(1, inner.DisposeAsyncCount);
        Assert.Equal(1, tracker.ResetCount);
    }

    private sealed class FakeInnerTransaction : IGraphTransaction
    {
        public bool ThrowOnCommit { get; set; }
        public bool ThrowOnRollback { get; set; }
        public int CommitCount { get; private set; }
        public int CommitAsyncCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int RollbackAsyncCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int DisposeAsyncCount { get; private set; }

        public void Commit()
        {
            CommitCount++;
            if (ThrowOnCommit) throw new InvalidOperationException("commit boom");
        }

        public Task CommitAsync(CancellationToken token = default)
        {
            CommitAsyncCount++;
            if (ThrowOnCommit) throw new InvalidOperationException("commit boom");
            return Task.CompletedTask;
        }

        public void Rollback()
        {
            RollbackCount++;
            if (ThrowOnRollback) throw new InvalidOperationException("rollback boom");
        }

        public Task RollbackAsync(CancellationToken token = default)
        {
            RollbackAsyncCount++;
            if (ThrowOnRollback) throw new InvalidOperationException("rollback boom");
            return Task.CompletedTask;
        }

        public void Dispose() => DisposeCount++;

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeChangeTracker : IChangeTracker
    {
        public int ResetCount { get; private set; }

        public int Flush() => 0;
        public Task<int> FlushAsync(CancellationToken token = default) => Task.FromResult(0);
        public void TrackAdd<T>(T node) where T : class { }
        public void TrackUpdate<T>(T node) where T : class { }
        public void TrackRemove<T>(T node) where T : class { }
        public void TrackMerge<T>(T node) where T : class { }
        public void TrackConnect<TStart, TEdge, TEnd>(TStart start, TEdge edge, TEnd end) where TStart : class where TEdge : class where TEnd : class { }
        public void TrackDisconnect<TStart, TEdge, TEnd>(TStart start, TEnd end) where TStart : class where TEdge : class where TEnd : class { }
        public T? Find<T>(object key) where T : class => null;
        public void Attach<T>(T node) where T : class { }
        public void Reset() => ResetCount++;
    }
}
