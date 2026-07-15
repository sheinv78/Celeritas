// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Argument checks that <see cref="ArgumentNullException.ThrowIfNull(object?, string?)"/> cannot
/// express on its own. See <c>docs/adr/0002-argument-validation-conventions.md</c>.
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Reject a collection that is itself null, or that contains a null element.
    /// </summary>
    /// <remarks>
    /// The element half is the point. Every caller here forwards each element to another public
    /// method that guards its own parameter, so a null element already threw — but naming that
    /// method's parameter, which the caller never passed. Someone handing
    /// <c>DetectCadence(["C", null])</c> a bad array was told the problem was with an argument
    /// called "symbol". Checking up front lets the exception name the array the caller actually
    /// owns, and say which index is wrong.
    ///
    /// This is a boundary check, not a loop guard: it runs once per public call, over input the
    /// method is about to parse anyway.
    /// </remarks>
    internal static void ThrowIfNullOrHasNullElement<T>(IReadOnlyList<T>? items, string paramName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, paramName);

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is null)
            {
                throw new ArgumentNullException(paramName, $"Element at index {i} is null.");
            }
        }
    }
}
