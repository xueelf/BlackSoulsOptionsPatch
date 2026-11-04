using StbImageSharp;
using StbImageWriteSharp;

internal static class Program
{
    private static readonly int[] IconSizes = { 16, 24, 32, 48, 64, 128, 256 };

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: IconBuilder <source.png> <output.ico>");
            return 2;
        }

        string input = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        ImageResult source;
        using (var stream = File.OpenRead(input))
        {
            source = ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        }

        var images = new List<IconImage>();
        foreach (int size in IconSizes)
        {
            byte[] canvas = CreateSquareCanvas(source, size);
            if (size == 256)
            {
                using var stream = new MemoryStream();
                new ImageWriter().WritePng(
                    canvas, size, size, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
                images.Add(new IconImage(size, stream.ToArray()));
            }
            else
            {
                images.Add(new IconImage(size, BuildBitmap(canvas, size)));
            }
        }

        WriteIco(output, images);
        Console.WriteLine($"{output} ({new FileInfo(output).Length} bytes)");
        return 0;
    }

    private static byte[] CreateSquareCanvas(ImageResult source, int size)
    {
        var canvas = new byte[size * size * 4];
        float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
        int targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        int targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
        int offsetX = (size - targetWidth) / 2;
        int offsetY = (size - targetHeight) / 2;

        for (int y = 0; y < targetHeight; y++)
        {
            float sourceY = (y + 0.5f) * source.Height / targetHeight - 0.5f;
            for (int x = 0; x < targetWidth; x++)
            {
                float sourceX = (x + 0.5f) * source.Width / targetWidth - 0.5f;
                int destination = ((offsetY + y) * size + offsetX + x) * 4;
                SampleBilinear(source, sourceX, sourceY, canvas, destination);
            }
        }

        return canvas;
    }

    private static void SampleBilinear(
        ImageResult source, float sourceX, float sourceY, byte[] destination, int destinationIndex)
    {
        int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, source.Width - 1);
        int y0 = Math.Clamp((int)Math.Floor(sourceY), 0, source.Height - 1);
        int x1 = Math.Min(x0 + 1, source.Width - 1);
        int y1 = Math.Min(y0 + 1, source.Height - 1);
        float fx = Math.Clamp(sourceX - x0, 0f, 1f);
        float fy = Math.Clamp(sourceY - y0, 0f, 1f);

        int topLeft = (y0 * source.Width + x0) * 4;
        int topRight = (y0 * source.Width + x1) * 4;
        int bottomLeft = (y1 * source.Width + x0) * 4;
        int bottomRight = (y1 * source.Width + x1) * 4;

        for (int channel = 0; channel < 4; channel++)
        {
            float top = Lerp(source.Data[topLeft + channel], source.Data[topRight + channel], fx);
            float bottom = Lerp(source.Data[bottomLeft + channel], source.Data[bottomRight + channel], fx);
            destination[destinationIndex + channel] = (byte)Math.Clamp(
                (int)Math.Round(Lerp(top, bottom, fy)), 0, 255);
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static byte[] BuildBitmap(byte[] rgba, int size)
    {
        int xorSize = size * size * 4;
        int maskRowSize = ((size + 31) / 32) * 4;
        int maskSize = maskRowSize * size;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(40);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(xorSize + maskSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (int y = size - 1; y >= 0; y--)
        {
            int row = y * size * 4;
            for (int x = 0; x < size; x++)
            {
                int index = row + x * 4;
                writer.Write(rgba[index + 2]);
                writer.Write(rgba[index + 1]);
                writer.Write(rgba[index]);
                writer.Write(rgba[index + 3]);
            }
        }

        for (int y = size - 1; y >= 0; y--)
        {
            byte[] mask = new byte[maskRowSize];
            int row = y * size * 4;
            for (int x = 0; x < size; x++)
            {
                if (rgba[row + x * 4 + 3] < 128)
                {
                    mask[x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }

            writer.Write(mask);
        }

        return stream.ToArray();
    }

    private static void WriteIco(string path, IReadOnlyList<IconImage> images)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);

        uint offset = (uint)(6 + 16 * images.Count);
        foreach (var image in images)
        {
            byte dimension = image.Size >= 256 ? (byte)0 : (byte)image.Size;
            writer.Write(dimension);
            writer.Write(dimension);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)image.Data.Length);
            writer.Write(offset);
            offset += (uint)image.Data.Length;
        }

        foreach (var image in images)
        {
            writer.Write(image.Data);
        }
    }

    private sealed record IconImage(int Size, byte[] Data);
}
