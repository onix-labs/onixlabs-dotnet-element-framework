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
/// Defines a projector from a provider result row to a node or edge instance using a registered <see cref="IGraphModel"/>.
/// </summary>
public interface IResultMaterializer
{
    /// <summary>
    /// Materializes the entity bound at <paramref name="alias"/> in <paramref name="row"/> as a node of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type of the materialized node.</typeparam>
    /// <param name="model">The frozen graph model used for property mapping.</param>
    /// <param name="row">The result row from which to read the bound entity.</param>
    /// <param name="alias">The alias under which the entity is bound in <paramref name="row"/>.</param>
    /// <returns>Returns the materialized node instance.</returns>
    /// <exception cref="ResultMaterializationException">Thrown when the entity cannot be materialized.</exception>
    T MaterializeNode<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias);

    /// <summary>
    /// Materializes the entity bound at <paramref name="alias"/> in <paramref name="row"/> as an edge of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type of the materialized edge.</typeparam>
    /// <param name="model">The frozen graph model used for property mapping.</param>
    /// <param name="row">The result row from which to read the bound entity.</param>
    /// <param name="alias">The alias under which the entity is bound in <paramref name="row"/>.</param>
    /// <returns>Returns the materialized edge instance.</returns>
    /// <exception cref="ResultMaterializationException">Thrown when the entity cannot be materialized.</exception>
    T MaterializeEdge<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias);
}
