using System;
using System.IO;

// These tutorial GIFs contain full, opaque frames in display order.
internal sealed class TutorialGif
{
    public readonly int Width, Height;
    public readonly byte[][] Frames;
    public readonly int[] EndTimes;
    public int Duration => EndTimes[EndTimes.Length - 1];

    public TutorialGif(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(data));
        string header = new string(reader.ReadChars(6));
        if (header != "GIF89a" && header != "GIF87a") throw new InvalidDataException("Expected a GIF.");
        Width = reader.ReadUInt16();
        Height = reader.ReadUInt16();
        int flags = reader.ReadByte();
        reader.ReadUInt16();
        var globalPalette = ReadPalette(reader, flags);
        var frames = new System.Collections.Generic.List<byte[]>();
        var endTimes = new System.Collections.Generic.List<int>();
        int delay = 100, elapsed = 0;

        for (int block = reader.ReadByte(); block != 0x3B; block = reader.ReadByte())
        {
            if (block == 0x21)
            {
                if (reader.ReadByte() == 0xF9)
                {
                    reader.ReadByte();
                    int control = reader.ReadByte();
                    if ((control & 1) != 0) throw new InvalidDataException("Tutorial GIF frames must be opaque.");
                    delay = Math.Max(10, reader.ReadUInt16() * 10);
                    reader.ReadUInt16();
                }
                else ReadBlocks(reader);
                continue;
            }

            if (block != 0x2C || reader.ReadUInt16() != 0 || reader.ReadUInt16() != 0
                || reader.ReadUInt16() != Width || reader.ReadUInt16() != Height)
                throw new InvalidDataException("Expected a full tutorial GIF frame.");
            flags = reader.ReadByte();
            if ((flags & 0x40) != 0) throw new InvalidDataException("Interlaced GIF frames are unsupported.");
            var palette = ReadPalette(reader, flags) ?? globalPalette;
            int minimumCodeSize = reader.ReadByte();
            var indices = Decode(ReadBlocks(reader), minimumCodeSize, Width * Height);
            var pixels = new byte[indices.Length * 3];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    Array.Copy(palette, indices[y * Width + x] * 3, pixels, ((Height - 1 - y) * Width + x) * 3, 3);

            frames.Add(pixels);
            elapsed += delay;
            endTimes.Add(elapsed);
        }

        if (frames.Count == 0) throw new InvalidDataException("GIF has no frames.");
        Frames = frames.ToArray();
        EndTimes = endTimes.ToArray();
    }

    private static byte[] ReadPalette(BinaryReader reader, int flags) =>
        (flags & 0x80) == 0 ? null : reader.ReadBytes(3 * (2 << (flags & 7)));

    private static byte[] ReadBlocks(BinaryReader reader)
    {
        using var bytes = new MemoryStream();
        for (int count = reader.ReadByte(); count != 0; count = reader.ReadByte())
            bytes.Write(reader.ReadBytes(count), 0, count);
        return bytes.ToArray();
    }

    private static byte[] Decode(byte[] bytes, int minimum, int pixelCount)
    {
        if (minimum < 2 || minimum > 8) throw new InvalidDataException("Invalid GIF code size.");
        int clear = 1 << minimum, end = clear + 1;
        int next = end + 1, width = minimum + 1, previous = -1, bit = 0, written = 0;
        var prefix = new int[4096];
        var suffix = new byte[4096];
        var stack = new byte[4097];
        var pixels = new byte[pixelCount];
        byte first = 0;

        while (bit + width <= bytes.Length * 8)
        {
            int code = 0;
            for (int i = 0; i < width; i++, bit++)
                code |= ((bytes[bit / 8] >> (bit % 8)) & 1) << i;
            if (code == end) break;
            if (code == clear)
            {
                next = end + 1;
                width = minimum + 1;
                previous = -1;
                continue;
            }

            int entry = code, count = 0;
            if (entry == next && previous >= 0)
            {
                stack[count++] = first;
                entry = previous;
            }
            if (entry >= next || entry >= clear && previous < 0)
                throw new InvalidDataException("Invalid GIF dictionary entry.");
            while (entry >= clear)
            {
                stack[count++] = suffix[entry];
                entry = prefix[entry];
            }
            first = (byte)entry;
            stack[count++] = first;
            if (written + count > pixelCount) throw new InvalidDataException("GIF frame exceeds its bounds.");
            while (count > 0) pixels[written++] = stack[--count];

            if (previous >= 0 && next < 4096)
            {
                prefix[next] = previous;
                suffix[next++] = first;
                if (next == (1 << width) && width < 12) width++;
            }
            previous = code;
        }

        if (written != pixelCount) throw new InvalidDataException("Incomplete GIF frame.");
        return pixels;
    }
}
