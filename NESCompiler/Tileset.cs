using System;
using System.Collections.Generic;
using System.Text;

namespace NESCompiler
{
    internal class Tileset
    {
        public static byte[] ConvertsChar(string file)
        {
            const int Width = 128;
            const int Height = 256;
            const int TileSize = 8;
            const int TileBytes = 16;

            if (!File.Exists(file))
                throw new FileNotFoundException("Character PNG not found.", file);

            using var bitmap = new Bitmap(file);

            if (bitmap.Width != Width || bitmap.Height != Height)
            {
                throw new InvalidOperationException(
                    $"Expected a {Width}x{Height} PNG, " +
                    $"but got {bitmap.Width}x{bitmap.Height}.");
            }

            // 16 columns × 32 rows = 512 tiles.
            // 512 tiles × 16 bytes = 8192 bytes.
            byte[] chr = new byte[512 * TileBytes];

            int tileIndex = 0;

            for (int tileY = 0; tileY < 32; tileY++)
            {
                for (int tileX = 0; tileX < 16; tileX++)
                {
                    int tileOffset = tileIndex * TileBytes;

                    // NES plane 0 occupies the first 8 bytes.
                    // NES plane 1 occupies the next 8 bytes.
                    for (int row = 0; row < 8; row++)
                    {
                        byte plane0 = 0;

                        for (int col = 0; col < 8; col++)
                        {
                            int x = tileX * 8 + col;
                            int y = tileY * 8 + row;

                            Color pixel = bitmap.GetPixel(x, y);

                            // Black pixel = character pixel.
                            if (pixel.A > 128)
                            {
                                // NES pixels are stored MSB first.
                                plane0 |= (byte)(1 << (7 - col));
                            }
                        }

                        chr[tileOffset + row] = plane0;

                        // Monochrome font, therefore plane 1 is zero.
                        chr[tileOffset + 8 + row] = 0;
                    }

                    tileIndex++;
                }
            }

            return chr;
        }
    }
}
