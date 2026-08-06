// SPDX-License-Identifier: MIT
// Mirror of b3HexColor in include/box3d/types.h.
// Generated from the header to avoid transcription errors; regenerate rather than hand-edit.

namespace Box3D.Native;

/// <summary>
/// Colours used by debug draw. Mirror of <c>b3HexColor</c>.
/// </summary>
/// <remarks>
/// The names mostly match the SVG named colours. The low 24 bits are RGB; the
/// high byte may carry a <see cref="b3DebugMaterial"/> preset.
/// </remarks>
public enum b3HexColor
{
    /// <summary>The colour AliceBlue (0xF0F8FF).</summary>
    b3_colorAliceBlue = 0xF0F8FF,

    /// <summary>The colour AntiqueWhite (0xFAEBD7).</summary>
    b3_colorAntiqueWhite = 0xFAEBD7,

    /// <summary>The colour Aqua (0x00FFFF).</summary>
    b3_colorAqua = 0x00FFFF,

    /// <summary>The colour Aquamarine (0x7FFFD4).</summary>
    b3_colorAquamarine = 0x7FFFD4,

    /// <summary>The colour Azure (0xF0FFFF).</summary>
    b3_colorAzure = 0xF0FFFF,

    /// <summary>The colour Beige (0xF5F5DC).</summary>
    b3_colorBeige = 0xF5F5DC,

    /// <summary>The colour Bisque (0xFFE4C4).</summary>
    b3_colorBisque = 0xFFE4C4,

    /// <summary>The colour Black (0x000000).</summary>
    b3_colorBlack = 0x000000,

    /// <summary>The colour BlanchedAlmond (0xFFEBCD).</summary>
    b3_colorBlanchedAlmond = 0xFFEBCD,

    /// <summary>The colour Blue (0x0000FF).</summary>
    b3_colorBlue = 0x0000FF,

    /// <summary>The colour BlueViolet (0x8A2BE2).</summary>
    b3_colorBlueViolet = 0x8A2BE2,

    /// <summary>The colour Brown (0xA52A2A).</summary>
    b3_colorBrown = 0xA52A2A,

    /// <summary>The colour Burlywood (0xDEB887).</summary>
    b3_colorBurlywood = 0xDEB887,

    /// <summary>The colour CadetBlue (0x5F9EA0).</summary>
    b3_colorCadetBlue = 0x5F9EA0,

    /// <summary>The colour Chartreuse (0x7FFF00).</summary>
    b3_colorChartreuse = 0x7FFF00,

    /// <summary>The colour Chocolate (0xD2691E).</summary>
    b3_colorChocolate = 0xD2691E,

    /// <summary>The colour Coral (0xFF7F50).</summary>
    b3_colorCoral = 0xFF7F50,

    /// <summary>The colour CornflowerBlue (0x6495ED).</summary>
    b3_colorCornflowerBlue = 0x6495ED,

    /// <summary>The colour Cornsilk (0xFFF8DC).</summary>
    b3_colorCornsilk = 0xFFF8DC,

    /// <summary>The colour Crimson (0xDC143C).</summary>
    b3_colorCrimson = 0xDC143C,

    /// <summary>The colour Cyan (0x00FFFF).</summary>
    b3_colorCyan = 0x00FFFF,

    /// <summary>The colour DarkBlue (0x00008B).</summary>
    b3_colorDarkBlue = 0x00008B,

    /// <summary>The colour DarkCyan (0x008B8B).</summary>
    b3_colorDarkCyan = 0x008B8B,

    /// <summary>The colour DarkGoldenRod (0xB8860B).</summary>
    b3_colorDarkGoldenRod = 0xB8860B,

    /// <summary>The colour DarkGray (0xA9A9A9).</summary>
    b3_colorDarkGray = 0xA9A9A9,

    /// <summary>The colour DarkGreen (0x006400).</summary>
    b3_colorDarkGreen = 0x006400,

    /// <summary>The colour DarkKhaki (0xBDB76B).</summary>
    b3_colorDarkKhaki = 0xBDB76B,

    /// <summary>The colour DarkMagenta (0x8B008B).</summary>
    b3_colorDarkMagenta = 0x8B008B,

    /// <summary>The colour DarkOliveGreen (0x556B2F).</summary>
    b3_colorDarkOliveGreen = 0x556B2F,

    /// <summary>The colour DarkOrange (0xFF8C00).</summary>
    b3_colorDarkOrange = 0xFF8C00,

    /// <summary>The colour DarkOrchid (0x9932CC).</summary>
    b3_colorDarkOrchid = 0x9932CC,

    /// <summary>The colour DarkRed (0x8B0000).</summary>
    b3_colorDarkRed = 0x8B0000,

    /// <summary>The colour DarkSalmon (0xE9967A).</summary>
    b3_colorDarkSalmon = 0xE9967A,

    /// <summary>The colour DarkSeaGreen (0x8FBC8F).</summary>
    b3_colorDarkSeaGreen = 0x8FBC8F,

    /// <summary>The colour DarkSlateBlue (0x483D8B).</summary>
    b3_colorDarkSlateBlue = 0x483D8B,

    /// <summary>The colour DarkSlateGray (0x2F4F4F).</summary>
    b3_colorDarkSlateGray = 0x2F4F4F,

    /// <summary>The colour DarkTurquoise (0x00CED1).</summary>
    b3_colorDarkTurquoise = 0x00CED1,

    /// <summary>The colour DarkViolet (0x9400D3).</summary>
    b3_colorDarkViolet = 0x9400D3,

    /// <summary>The colour DeepPink (0xFF1493).</summary>
    b3_colorDeepPink = 0xFF1493,

    /// <summary>The colour DeepSkyBlue (0x00BFFF).</summary>
    b3_colorDeepSkyBlue = 0x00BFFF,

    /// <summary>The colour DimGray (0x696969).</summary>
    b3_colorDimGray = 0x696969,

    /// <summary>The colour DodgerBlue (0x1E90FF).</summary>
    b3_colorDodgerBlue = 0x1E90FF,

    /// <summary>The colour FireBrick (0xB22222).</summary>
    b3_colorFireBrick = 0xB22222,

    /// <summary>The colour FloralWhite (0xFFFAF0).</summary>
    b3_colorFloralWhite = 0xFFFAF0,

    /// <summary>The colour ForestGreen (0x228B22).</summary>
    b3_colorForestGreen = 0x228B22,

    /// <summary>The colour Fuchsia (0xFF00FF).</summary>
    b3_colorFuchsia = 0xFF00FF,

    /// <summary>The colour Gainsboro (0xDCDCDC).</summary>
    b3_colorGainsboro = 0xDCDCDC,

    /// <summary>The colour GhostWhite (0xF8F8FF).</summary>
    b3_colorGhostWhite = 0xF8F8FF,

    /// <summary>The colour Gold (0xFFD700).</summary>
    b3_colorGold = 0xFFD700,

    /// <summary>The colour GoldenRod (0xDAA520).</summary>
    b3_colorGoldenRod = 0xDAA520,

    /// <summary>The colour Gray (0x808080).</summary>
    b3_colorGray = 0x808080,

    /// <summary>The colour Green (0x008000).</summary>
    b3_colorGreen = 0x008000,

    /// <summary>The colour GreenYellow (0xADFF2F).</summary>
    b3_colorGreenYellow = 0xADFF2F,

    /// <summary>The colour HoneyDew (0xF0FFF0).</summary>
    b3_colorHoneyDew = 0xF0FFF0,

    /// <summary>The colour HotPink (0xFF69B4).</summary>
    b3_colorHotPink = 0xFF69B4,

    /// <summary>The colour IndianRed (0xCD5C5C).</summary>
    b3_colorIndianRed = 0xCD5C5C,

    /// <summary>The colour Indigo (0x4B0082).</summary>
    b3_colorIndigo = 0x4B0082,

    /// <summary>The colour Ivory (0xFFFFF0).</summary>
    b3_colorIvory = 0xFFFFF0,

    /// <summary>The colour Khaki (0xF0E68C).</summary>
    b3_colorKhaki = 0xF0E68C,

    /// <summary>The colour Lavender (0xE6E6FA).</summary>
    b3_colorLavender = 0xE6E6FA,

    /// <summary>The colour LavenderBlush (0xFFF0F5).</summary>
    b3_colorLavenderBlush = 0xFFF0F5,

    /// <summary>The colour LawnGreen (0x7CFC00).</summary>
    b3_colorLawnGreen = 0x7CFC00,

    /// <summary>The colour LemonChiffon (0xFFFACD).</summary>
    b3_colorLemonChiffon = 0xFFFACD,

    /// <summary>The colour LightBlue (0xADD8E6).</summary>
    b3_colorLightBlue = 0xADD8E6,

    /// <summary>The colour LightCoral (0xF08080).</summary>
    b3_colorLightCoral = 0xF08080,

    /// <summary>The colour LightCyan (0xE0FFFF).</summary>
    b3_colorLightCyan = 0xE0FFFF,

    /// <summary>The colour LightGoldenRodYellow (0xFAFAD2).</summary>
    b3_colorLightGoldenRodYellow = 0xFAFAD2,

    /// <summary>The colour LightGray (0xD3D3D3).</summary>
    b3_colorLightGray = 0xD3D3D3,

    /// <summary>The colour LightGreen (0x90EE90).</summary>
    b3_colorLightGreen = 0x90EE90,

    /// <summary>The colour LightPink (0xFFB6C1).</summary>
    b3_colorLightPink = 0xFFB6C1,

    /// <summary>The colour LightSalmon (0xFFA07A).</summary>
    b3_colorLightSalmon = 0xFFA07A,

    /// <summary>The colour LightSeaGreen (0x20B2AA).</summary>
    b3_colorLightSeaGreen = 0x20B2AA,

    /// <summary>The colour LightSkyBlue (0x87CEFA).</summary>
    b3_colorLightSkyBlue = 0x87CEFA,

    /// <summary>The colour LightSlateGray (0x778899).</summary>
    b3_colorLightSlateGray = 0x778899,

    /// <summary>The colour LightSteelBlue (0xB0C4DE).</summary>
    b3_colorLightSteelBlue = 0xB0C4DE,

    /// <summary>The colour LightYellow (0xFFFFE0).</summary>
    b3_colorLightYellow = 0xFFFFE0,

    /// <summary>The colour Lime (0x00FF00).</summary>
    b3_colorLime = 0x00FF00,

    /// <summary>The colour LimeGreen (0x32CD32).</summary>
    b3_colorLimeGreen = 0x32CD32,

    /// <summary>The colour Linen (0xFAF0E6).</summary>
    b3_colorLinen = 0xFAF0E6,

    /// <summary>The colour Magenta (0xFF00FF).</summary>
    b3_colorMagenta = 0xFF00FF,

    /// <summary>The colour Maroon (0x800000).</summary>
    b3_colorMaroon = 0x800000,

    /// <summary>The colour MediumAquaMarine (0x66CDAA).</summary>
    b3_colorMediumAquaMarine = 0x66CDAA,

    /// <summary>The colour MediumBlue (0x0000CD).</summary>
    b3_colorMediumBlue = 0x0000CD,

    /// <summary>The colour MediumOrchid (0xBA55D3).</summary>
    b3_colorMediumOrchid = 0xBA55D3,

    /// <summary>The colour MediumPurple (0x9370DB).</summary>
    b3_colorMediumPurple = 0x9370DB,

    /// <summary>The colour MediumSeaGreen (0x3CB371).</summary>
    b3_colorMediumSeaGreen = 0x3CB371,

    /// <summary>The colour MediumSlateBlue (0x7B68EE).</summary>
    b3_colorMediumSlateBlue = 0x7B68EE,

    /// <summary>The colour MediumSpringGreen (0x00FA9A).</summary>
    b3_colorMediumSpringGreen = 0x00FA9A,

    /// <summary>The colour MediumTurquoise (0x48D1CC).</summary>
    b3_colorMediumTurquoise = 0x48D1CC,

    /// <summary>The colour MediumVioletRed (0xC71585).</summary>
    b3_colorMediumVioletRed = 0xC71585,

    /// <summary>The colour MidnightBlue (0x191970).</summary>
    b3_colorMidnightBlue = 0x191970,

    /// <summary>The colour MintCream (0xF5FFFA).</summary>
    b3_colorMintCream = 0xF5FFFA,

    /// <summary>The colour MistyRose (0xFFE4E1).</summary>
    b3_colorMistyRose = 0xFFE4E1,

    /// <summary>The colour Moccasin (0xFFE4B5).</summary>
    b3_colorMoccasin = 0xFFE4B5,

    /// <summary>The colour NavajoWhite (0xFFDEAD).</summary>
    b3_colorNavajoWhite = 0xFFDEAD,

    /// <summary>The colour Navy (0x000080).</summary>
    b3_colorNavy = 0x000080,

    /// <summary>The colour OldLace (0xFDF5E6).</summary>
    b3_colorOldLace = 0xFDF5E6,

    /// <summary>The colour Olive (0x808000).</summary>
    b3_colorOlive = 0x808000,

    /// <summary>The colour OliveDrab (0x6B8E23).</summary>
    b3_colorOliveDrab = 0x6B8E23,

    /// <summary>The colour Orange (0xFFA500).</summary>
    b3_colorOrange = 0xFFA500,

    /// <summary>The colour OrangeRed (0xFF4500).</summary>
    b3_colorOrangeRed = 0xFF4500,

    /// <summary>The colour Orchid (0xDA70D6).</summary>
    b3_colorOrchid = 0xDA70D6,

    /// <summary>The colour PaleGoldenRod (0xEEE8AA).</summary>
    b3_colorPaleGoldenRod = 0xEEE8AA,

    /// <summary>The colour PaleGreen (0x98FB98).</summary>
    b3_colorPaleGreen = 0x98FB98,

    /// <summary>The colour PaleTurquoise (0xAFEEEE).</summary>
    b3_colorPaleTurquoise = 0xAFEEEE,

    /// <summary>The colour PaleVioletRed (0xDB7093).</summary>
    b3_colorPaleVioletRed = 0xDB7093,

    /// <summary>The colour PapayaWhip (0xFFEFD5).</summary>
    b3_colorPapayaWhip = 0xFFEFD5,

    /// <summary>The colour PeachPuff (0xFFDAB9).</summary>
    b3_colorPeachPuff = 0xFFDAB9,

    /// <summary>The colour Peru (0xCD853F).</summary>
    b3_colorPeru = 0xCD853F,

    /// <summary>The colour Pink (0xFFC0CB).</summary>
    b3_colorPink = 0xFFC0CB,

    /// <summary>The colour Plum (0xDDA0DD).</summary>
    b3_colorPlum = 0xDDA0DD,

    /// <summary>The colour PowderBlue (0xB0E0E6).</summary>
    b3_colorPowderBlue = 0xB0E0E6,

    /// <summary>The colour Purple (0x800080).</summary>
    b3_colorPurple = 0x800080,

    /// <summary>The colour RebeccaPurple (0x663399).</summary>
    b3_colorRebeccaPurple = 0x663399,

    /// <summary>The colour Red (0xFF0000).</summary>
    b3_colorRed = 0xFF0000,

    /// <summary>The colour RosyBrown (0xBC8F8F).</summary>
    b3_colorRosyBrown = 0xBC8F8F,

    /// <summary>The colour RoyalBlue (0x4169E1).</summary>
    b3_colorRoyalBlue = 0x4169E1,

    /// <summary>The colour SaddleBrown (0x8B4513).</summary>
    b3_colorSaddleBrown = 0x8B4513,

    /// <summary>The colour Salmon (0xFA8072).</summary>
    b3_colorSalmon = 0xFA8072,

    /// <summary>The colour SandyBrown (0xF4A460).</summary>
    b3_colorSandyBrown = 0xF4A460,

    /// <summary>The colour SeaGreen (0x2E8B57).</summary>
    b3_colorSeaGreen = 0x2E8B57,

    /// <summary>The colour SeaShell (0xFFF5EE).</summary>
    b3_colorSeaShell = 0xFFF5EE,

    /// <summary>The colour Sienna (0xA0522D).</summary>
    b3_colorSienna = 0xA0522D,

    /// <summary>The colour Silver (0xC0C0C0).</summary>
    b3_colorSilver = 0xC0C0C0,

    /// <summary>The colour SkyBlue (0x87CEEB).</summary>
    b3_colorSkyBlue = 0x87CEEB,

    /// <summary>The colour SlateBlue (0x6A5ACD).</summary>
    b3_colorSlateBlue = 0x6A5ACD,

    /// <summary>The colour SlateGray (0x708090).</summary>
    b3_colorSlateGray = 0x708090,

    /// <summary>The colour Snow (0xFFFAFA).</summary>
    b3_colorSnow = 0xFFFAFA,

    /// <summary>The colour SpringGreen (0x00FF7F).</summary>
    b3_colorSpringGreen = 0x00FF7F,

    /// <summary>The colour SteelBlue (0x4682B4).</summary>
    b3_colorSteelBlue = 0x4682B4,

    /// <summary>The colour Tan (0xD2B48C).</summary>
    b3_colorTan = 0xD2B48C,

    /// <summary>The colour Teal (0x008080).</summary>
    b3_colorTeal = 0x008080,

    /// <summary>The colour Thistle (0xD8BFD8).</summary>
    b3_colorThistle = 0xD8BFD8,

    /// <summary>The colour Tomato (0xFF6347).</summary>
    b3_colorTomato = 0xFF6347,

    /// <summary>The colour Turquoise (0x40E0D0).</summary>
    b3_colorTurquoise = 0x40E0D0,

    /// <summary>The colour Violet (0xEE82EE).</summary>
    b3_colorViolet = 0xEE82EE,

    /// <summary>The colour Wheat (0xF5DEB3).</summary>
    b3_colorWheat = 0xF5DEB3,

    /// <summary>The colour White (0xFFFFFF).</summary>
    b3_colorWhite = 0xFFFFFF,

    /// <summary>The colour WhiteSmoke (0xF5F5F5).</summary>
    b3_colorWhiteSmoke = 0xF5F5F5,

    /// <summary>The colour Yellow (0xFFFF00).</summary>
    b3_colorYellow = 0xFFFF00,

    /// <summary>The colour YellowGreen (0x9ACD32).</summary>
    b3_colorYellowGreen = 0x9ACD32,

    /// <summary>The colour Box2DRed (0xDC3132).</summary>
    b3_colorBox2DRed = 0xDC3132,

    /// <summary>The colour Box2DBlue (0x30AEBF).</summary>
    b3_colorBox2DBlue = 0x30AEBF,

    /// <summary>The colour Box2DGreen (0x8CC924).</summary>
    b3_colorBox2DGreen = 0x8CC924,

    /// <summary>The colour Box2DYellow (0xFFEE8C).</summary>
    b3_colorBox2DYellow = 0xFFEE8C,
}
