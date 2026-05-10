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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the in-memory provider's <see cref="IGraphTransaction"/> implementation.
/// </summary>
/// <remarks>
/// The transaction holds a private clone of the canonical store; ambient writes mutate the clone. Commit copies the
/// clone's contents back over the canonical store; rollback or dispose without commit discards the clone. The opener's
/// ambient slot is cleared on each terminal so a new transaction can be opened immediately afterwards.
/// </remarks>
internal sealed class InMemoryGraphTransaction : IGraphTransaction
{
    /// <summary>
    /// The canonical store the transaction commits back into.
    /// </summary>
    private readonly InMemoryStore canonical;

    /// <summary>
    /// The opener that produced this transaction, notified on every terminal call.
    /// </summary>
    private readonly InMemoryGraphTransactionOpener opener;

    /// <summary>
    /// Indicates whether the transaction has been committed, rolled back, or disposed.
    /// </summary>
    private bool closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryGraphTransaction"/> class.
    /// </summary>
    /// <param name="canonical">The canonical store this transaction commits back into.</param>
    /// <param name="opener">The opener that produced this transaction.</param>
    internal InMemoryGraphTransaction(InMemoryStore canonical, InMemoryGraphTransactionOpener opener)
    {
        this.canonical = canonical;
        this.opener = opener;
        Store = canonical.Clone();
    }

    /// <summary>
    /// Gets the cloned store ambient operations mutate while the transaction is open.
    /// </summary>
    /// <value>The transaction-scoped copy of the canonical store.</value>
    internal InMemoryStore Store { get; }

    /// <inheritdoc/>
    public void Commit()
    {
        if (closed) return;
        canonical.ReplaceWith(Store);
        Close();
    }

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken token = default)
    {
        Commit();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Rollback()
    {
        if (closed) return;
        Close();
    }

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken token = default)
    {
        Rollback();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (closed) return;
        Close();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Marks the transaction closed and clears the opener's ambient slot exactly once.
    /// </summary>
    private void Close()
    {
        closed = true;
        opener.ClearActive();
    }
}
