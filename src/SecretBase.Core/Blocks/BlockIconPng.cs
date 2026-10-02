using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SecretBase.Core.Blocks;

/// <summary>
/// Minimal PNG writer for solid rounded-ish Block icon tiles (no System.Drawing).
/// </summary>
public static class BlockIconPng
{
    public static bool TryWriteSolidTile(
        string destinationPath,
        int size,
        byte a,
        byte r,
        byte g,
        byte b,
        out string? errorMessage)
    {
        errorMessage = null;
        if (size < 8 || size > 512)
        {
            errorMessage = "Icon size is out of range.";
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var rgba = BuildRoundedRgba(size, a, r, g, b);
            var png = EncodeRgbaPng(size, size, rgba);
            var tmp = destinationPath + ".tmp";
            File.WriteAllBytes(tmp, png);
            File.Copy(tmp, destinationPath, overwrite: true);
            File.Delete(tmp);
            return File.Exists(destinationPath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool TryParseHex(string hex, out byte a, out byte r, out byte g, out byte b)
    {
        a = 255;
        r = 47;
        g = 111;
        b = 237;
        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        try
        {
            if (value.Length == 8)
            {
                a = Convert.ToByte(value[..2], 16);
                r = Convert.ToByte(value[2..4], 16);
                g = Convert.ToByte(value[4..6], 16);
                b = Convert.ToByte(value[6..8], 16);
                return true;
            }

            if (value.Length == 6)
            {
                a = 255;
                r = Convert.ToByte(value[..2], 16);
                g = Convert.ToByte(value[2..4], 16);
                b = Convert.ToByte(value[4..6], 16);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static byte[] BuildRoundedRgba(int size, byte a, byte r, byte g, byte b)
    {
        var data = new byte[size * size * 4];
        var radius = size * 0.22;
        var cx = (size - 1) / 2.0;
        var cy = (size - 1) / 2.0;
        var half = size / 2.0 - 2;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = (y * size + x) * 4;
                var dx = Math.Abs(x - cx) - (half - radius);
                var dy = Math.Abs(y - cy) - (half - radius);
                var outside = 0.0;
                if (dx > 0 && dy > 0)
                {
                    outside = Math.Sqrt(dx * dx + dy * dy) - radius;
                }
                else
                {
                    outside = Math.Max(dx, dy);
                }

                if (outside <= 0)
                {
                    data[i] = r;
                    data[i + 1] = g;
                    data[i + 2] = b;
                    data[i + 3] = a;
                }
                else
                {
                    data[i + 3] = 0;
                }
            }
        }

        return data;
    }

    private static byte[] EncodeRgbaPng(int width, int height, byte[] rgba)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..8], height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(ms, "IHDR", ihdr);

        var raw = new byte[(width * 4 + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (width * 4 + 1);
            raw[rowStart] = 0; // filter none
            Buffer.BlockCopy(rgba, y * width * 4, raw, rowStart + 1, width * 4);
        }

        using var deflate = new MemoryStream();
        // zlib header + deflate + adler32
        deflate.WriteByte(0x78);
        deflate.WriteByte(0x01);
        using (var zlib = new DeflateStream(deflate, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        var adler = Adler32(raw);
        Span<byte> adlerBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adlerBytes, adler);
        deflate.Write(adlerBytes);
        WriteChunk(ms, "IDAT", deflate.ToArray());
        WriteChunk(ms, "IEND", ReadOnlySpan<byte>.Empty);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        stream.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint mod = 65521;
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
