// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Shared limits for stack-allocated scratch buffers.
/// </summary>
/// <remarks>
/// A <c>stackalloc</c> sized by caller-controlled input is a process-kill waiting to happen:
/// blowing the stack raises <see cref="StackOverflowException"/>, which cannot be caught, so
/// no amount of argument validation can turn it back into a recoverable error. The fix is
/// structural — stay on the stack while the scratch buffer is small, fall back to the heap
/// once it isn't:
/// <code>
/// Span&lt;int&gt; scratch = length &lt;= StackAlloc.MaxInts
///     ? stackalloc int[length]
///     : new int[length];
/// </code>
/// </remarks>
internal static class StackAlloc
{
    /// <summary>
    /// Maximum element count to <c>stackalloc</c> as <see cref="int"/> or <see cref="float"/>
    /// (4 KB at 4 bytes each — comfortably inside the 1 MB default stack, even nested).
    /// Matches the threshold <see cref="NoteBuffer.Sort"/> has always used.
    /// </summary>
    internal const int MaxInts = 1024;
}
