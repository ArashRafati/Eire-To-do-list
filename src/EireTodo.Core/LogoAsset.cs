using System.Buffers.Binary;
namespace EireTodo.Core;

public static class LogoAsset
{
    private static readonly uint[] CrcTable = Enumerable.Range(0,256).Select(index =>
    {
        var value = (uint)index; for (var bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xedb88320 ^ (value >> 1) : value >> 1; return value;
    }).ToArray();
    public const int MaxBytes = 4 * 1024 * 1024;
    public static void Validate(byte[] bytes)
    {
        if (bytes is null) throw new ArgumentException("Logo data is missing.");
        if (bytes.Length == 0) return;
        if (bytes.Length > MaxBytes || bytes.Length < 33 || !bytes.AsSpan(0,8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }) || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8,4)) != 13 || !bytes.AsSpan(12,4).SequenceEqual("IHDR"u8)) throw new ArgumentException("Choose a PNG logo up to 4 MB.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)); var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if (width == 0 || height == 0 || width > 4096 || height > 4096) throw new ArgumentException("Logo dimensions must be between 1 and 4096 pixels on each side.");
        var offset = 8; var imageData = false; var ended = false;
        while (offset < bytes.Length)
        {
            if (bytes.Length-offset < 12) throw new ArgumentException("The PNG logo is incomplete.");
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));
            if (size > (uint)(bytes.Length-offset-12)) throw new ArgumentException("The PNG logo is incomplete.");
            var length = (int)size; var type = bytes.AsSpan(offset+4,4); var crc = uint.MaxValue;
            foreach(var value in bytes.AsSpan(offset+4,length+4)) crc = CrcTable[(crc ^ value)&255] ^ (crc >> 8);
            if ((crc ^ uint.MaxValue) != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8+length,4))) throw new ArgumentException("The PNG logo is damaged.");
            imageData |= type.SequenceEqual("IDAT"u8);
            if (type.SequenceEqual("IEND"u8)) { if (length != 0 || offset+12 != bytes.Length) throw new ArgumentException("The PNG logo has an invalid end."); ended = true; break; }
            offset += length+12;
        }
        if (!imageData || !ended) throw new ArgumentException("The PNG logo is incomplete.");
    }
}
