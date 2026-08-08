// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Visualizer.Rendering;

/// <summary>
/// Where the picture is taken from.
/// </summary>
/// <remarks>
/// Right-handed with y up, which is the convention Box3D uses, so nothing is
/// mirrored on the way in. <see cref="Matrix4x4.CreateLookAt"/> and
/// <see cref="Matrix4x4.CreatePerspectiveFieldOfView"/> do the work; the
/// framework types have the same layout as the engine's, so a position read out
/// of a body goes straight into the transform with no conversion.
/// </remarks>
internal readonly record struct Camera
{
    /// <summary>Creates a camera.</summary>
    /// <param name="eye">Where the camera is.</param>
    /// <param name="target">What it looks at.</param>
    /// <param name="fieldOfViewDegrees">The vertical field of view.</param>
    /// <param name="near">The near plane distance.</param>
    /// <param name="far">The far plane distance.</param>
    public Camera(Vector3 eye, Vector3 target, float fieldOfViewDegrees = 38.0f, float near = 0.1f, float far = 400.0f)
    {
        Eye = eye;
        Target = target;
        FieldOfViewDegrees = fieldOfViewDegrees;
        Near = near;
        Far = far;
    }

    /// <summary>Gets where the camera is.</summary>
    public Vector3 Eye { get; init; }

    /// <summary>Gets what the camera looks at.</summary>
    public Vector3 Target { get; init; }

    /// <summary>Gets the vertical field of view in degrees.</summary>
    public float FieldOfViewDegrees { get; init; }

    /// <summary>Gets the near plane distance.</summary>
    public float Near { get; init; }

    /// <summary>Gets the far plane distance.</summary>
    public float Far { get; init; }

    /// <summary>
    /// Places a camera on a sphere around a point, which is how every scene in
    /// this project frames itself.
    /// </summary>
    /// <param name="target">The point to look at.</param>
    /// <param name="distance">How far away to stand.</param>
    /// <param name="yawDegrees">The angle around the up axis; zero looks along negative z.</param>
    /// <param name="pitchDegrees">How far above the target to rise.</param>
    /// <param name="fieldOfViewDegrees">The vertical field of view.</param>
    /// <returns>The camera.</returns>
    public static Camera Orbit(
        Vector3 target,
        float distance,
        float yawDegrees,
        float pitchDegrees,
        float fieldOfViewDegrees = 38.0f)
    {
        float yaw = yawDegrees * (MathF.PI / 180.0f);
        float pitch = pitchDegrees * (MathF.PI / 180.0f);

        Vector3 direction = new(
            MathF.Cos(pitch) * MathF.Sin(yaw),
            MathF.Sin(pitch),
            MathF.Cos(pitch) * MathF.Cos(yaw));

        return new Camera(target + (direction * distance), target, fieldOfViewDegrees);
    }

    /// <summary>Builds the combined view and projection transform.</summary>
    /// <param name="aspect">The width divided by the height of the target.</param>
    /// <returns>The transform taking a world position to clip space.</returns>
    public Matrix4x4 ViewProjection(float aspect)
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfViewDegrees * (MathF.PI / 180.0f),
            aspect,
            Near,
            Far);

        return view * projection;
    }
}
