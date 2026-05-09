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
/// Defines a per-context factory that produces and caches typed-set instances for the <see cref="GraphContext"/> typed-set accessors.
/// </summary>
internal interface IGraphSetFactory
{
    /// <summary>
    /// Returns the typed-set accessor for nodes of the specified type, creating and caching it on the first call.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <returns>An <see cref="INodeSet{T}"/> scoped to the node type.</returns>
    INodeSet<T> GetNodesOfType<T>() where T : class;

    /// <summary>
    /// Returns the typed-set accessor for edges of the specified type, creating and caching it on the first call.
    /// </summary>
    /// <typeparam name="T">The edge type. Must be a registered edge in the model.</typeparam>
    /// <returns>An <see cref="IEdgeSet{T}"/> scoped to the edge type.</returns>
    IEdgeSet<T> GetEdgesOfType<T>() where T : class;
}
