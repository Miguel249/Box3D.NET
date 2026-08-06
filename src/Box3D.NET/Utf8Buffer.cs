// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Text;

namespace Box3D;

/// <summary>
/// Converts a string to a null-terminated UTF-8 buffer for the duration of a
/// single call.
/// </summary>
/// <remarks>
/// <para>
/// Box3D takes names as <c>const char*</c> and copies them, so the buffer only
/// needs to outlive the call. Short names, which is nearly all of them, are
/// written into caller-provided stack space and allocate nothing; longer ones
/// fall back to the array pool rather than growing the stack without bound.
/// </para>
/// <para>
/// This is a ref struct so that it cannot outlive the stack buffer it may be
/// pointing at.
/// </para>
/// </remarks>
internal ref struct Utf8Buffer
{
    private byte[]? _rented;
    private readonly Span<byte> _buffer;
    private readonly int _length;

    /// <summary>
    /// Encodes a string into the supplied scratch space, renting from the array
    /// pool only if it does not fit.
    /// </summary>
    /// <param name="value">The string, which may be null.</param>
    /// <param name="scratch">Caller-provided stack space.</param>
    public Utf8Buffer(string? value, Span<byte> scratch)
    {
        if (value is null)
        {
            _rented = null;
            _buffer = default;
            _length = -1;
            return;
        }

        // One extra byte for the terminator that C requires and .NET does not add.
        int required = Encoding.UTF8.GetByteCount(value) + 1;

        if (required <= scratch.Length)
        {
            _rented = null;
            _buffer = scratch;
        }
        else
        {
            _rented = ArrayPool<byte>.Shared.Rent(required);
            _buffer = _rented;
        }

        int written = Encoding.UTF8.GetBytes(value, _buffer);
        _buffer[written] = 0;
        _length = written;
    }

    /// <summary>
    /// Gets a pointer to the null-terminated bytes, or null when the source
    /// string was null.
    /// </summary>
    /// <remarks>
    /// Only valid while this instance is alive and only if the buffer is pinned,
    /// which callers do with <c>fixed</c>.
    /// </remarks>
    public readonly bool IsNull => _length < 0;

    /// <summary>Gets the encoded bytes, including the terminator.</summary>
    public readonly ReadOnlySpan<byte> Span => IsNull ? default : _buffer[..(_length + 1)];

    /// <summary>Returns any pooled array.</summary>
    public void Dispose()
    {
        if (_rented is not null)
        {
            ArrayPool<byte>.Shared.Return(_rented);
            _rented = null;
        }
    }
}
