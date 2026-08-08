// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Visualizer.Rendering;

/// <summary>
/// Colour conversions. Everything the rasterizer touches is linear; everything
/// written down as a hex literal is sRGB.
/// </summary>
/// <remarks>
/// Mixing the two is what makes a naive software renderer look muddy: averaging
/// sRGB values is not averaging light, so shading gradients and the downsample
/// that resolves the supersampled buffer both come out wrong.
/// </remarks>
internal static class Rgb
{
    /// <summary>Converts an <c>0xRRGGBB</c> literal to linear colour.</summary>
    /// <param name="hex">The sRGB value.</param>
    /// <returns>The same colour, linear.</returns>
    public static Vector3 FromHex(uint hex) => ToLinear(new Vector3(
        ((hex >> 16) & 0xFF) / 255.0f,
        ((hex >> 8) & 0xFF) / 255.0f,
        (hex & 0xFF) / 255.0f));

    /// <summary>Converts an sRGB colour in 0..1 to linear.</summary>
    /// <param name="srgb">The sRGB colour.</param>
    /// <returns>The linear colour.</returns>
    public static Vector3 ToLinear(Vector3 srgb) => new(
        ToLinear(srgb.X),
        ToLinear(srgb.Y),
        ToLinear(srgb.Z));

    /// <summary>Converts one linear channel to sRGB.</summary>
    /// <param name="value">The linear value.</param>
    /// <returns>The sRGB value.</returns>
    public static float ToSrgb(float value)
    {
        value = Math.Clamp(value, 0.0f, 1.0f);
        return value <= 0.0031308f
            ? value * 12.92f
            : (1.055f * MathF.Pow(value, 1.0f / 2.4f)) - 0.055f;
    }

    private static float ToLinear(float value) =>
        value <= 0.04045f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
}

/// <summary>
/// The lighting and background every scene shares, so that the whole gallery
/// looks like one set of photographs rather than eight unrelated ones.
/// </summary>
internal sealed record RenderStyle
{
    /// <summary>Gets the studio defaults.</summary>
    public static RenderStyle Default { get; } = new();

    /// <summary>Gets the colour at the top of the backdrop.</summary>
    public Vector3 BackgroundTop { get; init; } = Rgb.FromHex(0x243040);

    /// <summary>Gets the colour at the bottom of the backdrop.</summary>
    public Vector3 BackgroundBottom { get; init; } = Rgb.FromHex(0x0C1017);

    /// <summary>Gets the direction towards the key light.</summary>
    public Vector3 LightDirection { get; init; } = Vector3.Normalize(new Vector3(0.46f, 0.70f, 0.42f));

    /// <summary>Gets the colour and intensity of the key light.</summary>
    public Vector3 LightColor { get; init; } = Rgb.FromHex(0xFFF1DC) * 1.05f;

    /// <summary>Gets the ambient light arriving from above.</summary>
    public Vector3 SkyLight { get; init; } = Rgb.FromHex(0x9FB6D6) * 0.42f;

    /// <summary>Gets the ambient light bounced from below.</summary>
    public Vector3 GroundBounce { get; init; } = Rgb.FromHex(0x4A3F38) * 0.30f;

    /// <summary>Gets the colour distant geometry fades towards.</summary>
    public Vector3 FogColor { get; init; } = Rgb.FromHex(0x1B2431);

    /// <summary>Gets how quickly geometry fades with distance.</summary>
    public float FogDensity { get; init; } = 0.020f;

    /// <summary>Gets the strength of the specular highlight.</summary>
    public float SpecularStrength { get; init; } = 0.18f;

    /// <summary>Gets how tight the specular highlight is.</summary>
    public float Shininess { get; init; } = 36.0f;

    /// <summary>
    /// Gets the height of the plane that catches shadows, or
    /// <see langword="null"/> for a scene that casts none.
    /// </summary>
    public float? ShadowPlaneY { get; init; }

    /// <summary>Gets how much a shadow darkens what it falls on.</summary>
    public float ShadowStrength { get; init; } = 0.62f;

    /// <summary>Gets how strongly the image darkens towards its corners.</summary>
    public float Vignette { get; init; } = 0.32f;
}

/// <summary>
/// The colours bodies are drawn in.
/// </summary>
/// <remarks>
/// Box3D suggests a colour for every shape it draws, and a debug view would use
/// it as is. These pictures are documentation rather than diagnostics, so the
/// scenes name their own instead and the engine's suggestion is the fallback.
/// </remarks>
internal static class Palette
{
    /// <summary>Gets the colour used for static geometry.</summary>
    public static Vector3 Static { get; } = Rgb.FromHex(0x76808C);

    /// <summary>Gets the colour used for the ground.</summary>
    public static Vector3 Ground { get; } = Rgb.FromHex(0x464E59);

    /// <summary>Gets the colour used for terrain, which needs to read as ground rather than as backdrop.</summary>
    public static Vector3 Terrain { get; } = Rgb.FromHex(0x7C7A6E);

    /// <summary>Gets the accent colour, for whatever the scene is about.</summary>
    public static Vector3 Accent { get; } = Rgb.FromHex(0xE8833A);

    /// <summary>Gets the secondary accent colour.</summary>
    public static Vector3 Cool { get; } = Rgb.FromHex(0x49B0BE);

    /// <summary>Gets the colour used for rays and other overlays.</summary>
    public static Vector3 Ray { get; } = Rgb.FromHex(0xF5D76E);

    /// <summary>Gets the colour used to mark a hit.</summary>
    public static Vector3 Hit { get; } = Rgb.FromHex(0xFF5C5C);

    private static readonly Vector3[] Wheel =
    [
        Rgb.FromHex(0xE8833A),
        Rgb.FromHex(0x49B0BE),
        Rgb.FromHex(0xD9CFC1),
        Rgb.FromHex(0xE4C441),
        Rgb.FromHex(0x9B7EDE),
        Rgb.FromHex(0x6FBF73),
        Rgb.FromHex(0xE0655B),
        Rgb.FromHex(0x5B8FD9),
    ];

    /// <summary>Picks a colour that will not clash with its neighbours.</summary>
    /// <param name="index">Any integer; adjacent values give distinct colours.</param>
    /// <returns>The colour.</returns>
    public static Vector3 Cycle(int index) => Wheel[(int)((uint)index % (uint)Wheel.Length)];

    /// <summary>Converts a colour the engine suggested into a linear colour.</summary>
    /// <param name="color">The colour Box3D handed to the drawer.</param>
    /// <returns>The linear colour.</returns>
    public static Vector3 FromDebugColor(DebugColor color)
    {
        (float r, float g, float b) = color.ToUnitRgb();
        return Rgb.ToLinear(new Vector3(r, g, b));
    }
}
