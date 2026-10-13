using System;
using System.Collections.Generic;
using System.Text;

namespace NESCompiler
{
    public static class Constants
    {
        private static readonly Dictionary<string, byte> NesConstants = new()
        {
            ["GRAY"] = 0x00,
            ["BLUE_1"] = 0x01,
            ["BLUE_2"] = 0x02,
            ["VIOLET_1"] = 0x03,
            ["MAGENTA"] = 0x04,
            ["RED_1"] = 0x05,
            ["RED_2"] = 0x06,
            ["ORANGE"] = 0x07,
            ["GREEN_1"] = 0x08,
            ["GREEN_2"] = 0x09,
            ["CYAN_1"] = 0x0A,
            ["BLACK_1"] = 0x0B,
            ["BLACK_2"] = 0x0C,
            ["BLACK_3"] = 0x0D,
            ["DARK_GRAY"] = 0x0E,
            ["BLACK"] = 0x0F,

            ["LIGHT_GRAY"] = 0x10,
            ["BLUE_3"] = 0x11,
            ["BLUE_4"] = 0x12,
            ["VIOLET_2"] = 0x13,
            ["MAGENTA_2"] = 0x14,
            ["RED_3"] = 0x15,
            ["RED_4"] = 0x16,
            ["ORANGE_2"] = 0x17,
            ["GREEN_3"] = 0x18,
            ["GREEN_4"] = 0x19,
            ["CYAN_2"] = 0x1A,
            ["BLACK_4"] = 0x1B,
            ["BLACK_5"] = 0x1C,
            ["GRAY_BLUE"] = 0x1D,
            ["GRAY_2"] = 0x1E,
            ["BLACK_6"] = 0x1F,

            ["LIGHT_GRAY_2"] = 0x20,
            ["SKY_BLUE"] = 0x21,
            ["BLUE_5"] = 0x22,
            ["VIOLET_3"] = 0x23,
            ["PINK_1"] = 0x24,
            ["RED_5"] = 0x25,
            ["ORANGE_3"] = 0x26,
            ["YELLOW_1"] = 0x27,
            ["GREEN_5"] = 0x28,
            ["GREEN_6"] = 0x29,
            ["CYAN_3"] = 0x2A,
            ["CYAN_4"] = 0x2B,
            ["VIOLET_4"] = 0x2C,
            ["BLUE_GRAY"] = 0x2D,
            ["GRAY_3"] = 0x2E,
            ["GRAY_4"] = 0x2F,

            ["WHITE"] = 0x30,
            ["PALE_BLUE"] = 0x31,
            ["PALE_BLUE_2"] = 0x32,
            ["PALE_VIOLET"] = 0x33,
            ["PALE_PINK"] = 0x34,
            ["PINK_2"] = 0x35,
            ["PEACH"] = 0x36,
            ["YELLOW_2"] = 0x37,
            ["PALE_GREEN"] = 0x38,
            ["PALE_GREEN_2"] = 0x39,
            ["PALE_CYAN"] = 0x3A,
            ["PALE_CYAN_2"] = 0x3B,
            ["PALE_VIOLET_2"] = 0x3C,
            ["PALE_BLUE_3"] = 0x3D,
            ["PALE_GRAY"] = 0x3E,
            ["WHITE_2"] = 0x3F
        };


        public static Dictionary<string, byte> GetConstants()
        {
            return new Dictionary<string, byte>(NesConstants);
        }
    }
}
