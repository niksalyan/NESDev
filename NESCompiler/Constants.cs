using System;
using System.Collections.Generic;
using System.Text;

namespace NESCompiler
{
    public static class Constants
    {
        private static readonly Dictionary<string, byte> NesConstants = new()
        {
            ["Gray"] = 0x00,
            ["DarkBlue"] = 0x01,
            ["DarkIndigo"] = 0x02,
            ["DarkPurple"] = 0x03,
            ["DarkMagenta"] = 0x04,
            ["DarkRed"] = 0x05,
            ["DarkBrown"] = 0x06,
            ["DarkOlive"] = 0x07,
            ["DarkGreen"] = 0x08,
            ["DarkTeal"] = 0x09,
            ["DarkCyan"] = 0x0A,
            ["Black"] = 0x0F,

            // The remaining entries should cover the complete
            // NES PPU palette, including all luminance levels.
        };

        public static Dictionary<string, byte> GetConstants()
        {
            return new Dictionary<string, byte>(NesConstants);
        }
    }
}
