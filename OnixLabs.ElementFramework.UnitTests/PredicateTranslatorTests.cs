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

using System.Linq.Expressions;

namespace OnixLabs.ElementFramework.UnitTests;

public class PredicateTranslatorTests
{
    [Fact(DisplayName = "Translates property == constant against the bound parameter")]
    public void TranslatesPropertyEqualsConstant()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name == "Alice";

        TraversalPredicate result = PredicateTranslator.Translate("a", predicate);

        Assert.Equal("a", result.Alias);
        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.Equal("Alice", result.Value);
    }

    [Fact(DisplayName = "Translates constant == property against the bound parameter (operands swapped)")]
    public void TranslatesConstantEqualsProperty()
    {
        Expression<Func<Author, bool>> predicate = a => "Alice" == a.Name;

        TraversalPredicate result = PredicateTranslator.Translate("a", predicate);

        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.Equal("Alice", result.Value);
    }

    [Fact(DisplayName = "Translates property == captured-variable by compiling and invoking the value side")]
    public void TranslatesPropertyEqualsCapturedVariable()
    {
        string captured = "Bob";
        Expression<Func<Author, bool>> predicate = a => a.Name == captured;

        TraversalPredicate result = PredicateTranslator.Translate("a", predicate);

        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.Equal("Bob", result.Value);
    }

    [Fact(DisplayName = "Throws NotSupportedException for non-equality operators")]
    public void ThrowsForNonEqualityOperator()
    {
        Expression<Func<Post, bool>> predicate = p => p.Title.Length > 5;

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("p", predicate));
    }

    [Fact(DisplayName = "Throws NotSupportedException when neither side is a parameter property access")]
    public void ThrowsWhenNeitherSideIsParameterProperty()
    {
        const string left = "x";
        const string right = "y";
        Expression<Func<Author, bool>> predicate = _ => left == right;

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("a", predicate));
    }

    [Fact(DisplayName = "Throws NotSupportedException when the property access is on a method-call result")]
    public void ThrowsForMethodCallPropertyAccess()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name.ToUpperInvariant() == "ALICE";

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("a", predicate));
    }
}
