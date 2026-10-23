namespace Gwota.Core.Common;

/// <summary>Incremental 64-bit FNV-1a hash: stable across runs and machines, unlike <see cref="HashCode"/>.</summary>
public struct Fnv64
{
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    private ulong _hash;
    private bool _started;

    public readonly ulong Value => _started ? _hash : OffsetBasis;

    public void Add(int value)
    {
        for (var shift = 0; shift < 32; shift += 8)
        {
            AddByte((byte)(value >> shift));
        }
    }

    public void Add(bool value) => AddByte(value ? (byte)1 : (byte)0);

    public void Add(string? value)
    {
        if (value is null)
        {
            Add(-1);
            return;
        }

        Add(value.Length);
        foreach (var c in value)
        {
            AddByte((byte)c);
            AddByte((byte)(c >> 8));
        }
    }

    private void AddByte(byte value)
    {
        if (!_started)
        {
            _hash = OffsetBasis;
            _started = true;
        }

        _hash = (_hash ^ value) * Prime;
    }
}