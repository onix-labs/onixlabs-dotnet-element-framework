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
