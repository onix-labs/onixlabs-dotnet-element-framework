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
/// Represents the bundle of internal coordination services resolved for a single <see cref="GraphContext"/>.
/// </summary>
/// <param name="ChangeTracker">The change tracker that the owning context delegates entity-state operations to.</param>
/// <param name="SetFactory">The factory that produces typed-set instances for the owning context.</param>
/// <param name="TransactionFactory">The factory that opens graph transactions for the owning context.</param>
/// <param name="Traversal">The fluent traversal entry point bound to this context's model and the provider's translator.</param>
/// <param name="RawStatementExecutor">The provider's raw statement executor for the owning context.</param>
/// <param name="Model">The frozen graph model resolved for the owning context.</param>
internal sealed record GraphContextServices(
    IChangeTracker ChangeTracker,
    IGraphSetFactory SetFactory,
    IGraphTransactionFactory TransactionFactory,
    IGraphTraversal Traversal,
    IRawStatementExecutor RawStatementExecutor,
    IGraphModel Model
);
