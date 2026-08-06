# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Box3D.NET.Native

- Blittable mirrors of every public Box3D type: identifiers, math types,
  enumerations, definition structs, events, queries, collision, the dynamic
  tree, the character mover and debug draw.
- 543 P/Invoke declarations covering `box3d.h`, `collision.h` and `types.h`,
  generated from the headers by `tools/generate-bindings.ps1` with the Doxygen
  comments converted to XML documentation.
- Hand-written bindings for `base.h`, `constants.h` and the exported math
  functions.
- C# ports of the `B3_INLINE` math helpers and the hull, mesh and height field
  accessors, which have internal linkage in C and cannot be bound.
- `NativeBool`, a one-byte boolean, so that definition structs stay blittable.
- Runtime marshalling disabled assembly-wide, making every call direct.

#### Box3D.NET

- `PhysicsWorld`, the only disposable type, with stepping, gravity, sleep and
  continuous collision settings, explosions, and access to buffered events.
- `Body` and `Shape` as handle value types, with transforms, velocities, forces,
  impulses, mass, sleep state and motion locks.
- `Sphere`, `Capsule` and `Box` geometry, with `Capsule.Upright` and
  `Box.FromSize` for the shapes people actually reach for.
- `BodyDefinition`, `ShapeDefinition`, `WorldSettings`, `PhysicsMaterial`,
  `CollisionFilter` and `QueryFilter` as records built from the engine defaults.
- Ray casts and overlap queries taking a struct callback, so a query allocates
  nothing, plus `CastRayClosest` for the common case.
- `WorldEvents`, exposing contact, sensor and body-move events as `ref struct`
  views over engine memory with struct enumerators.

#### Infrastructure

- Box3D pinned as an unmodified submodule at `3fc20f5`.
- `tools/build-native.ps1`, building the shared library and staging it under
  `runtimes/<rid>/native` for both packaging and local runs.
- Continuous integration building the native library for six platforms, testing
  on three, verifying formatting, verifying the generated bindings still match
  the submodule, and publishing the samples with NativeAOT.
- A release workflow driven by a version tag, which refuses to publish unless
  every platform's binary is present.
- Nine samples covering a basic world, dynamic and static bodies, collisions,
  ray casts, contact events, sensors, compound bodies, continuous collision and
  a kinematic character.

### Known limitations

- Joints, the character mover plane solver, debug draw, meshes, height fields
  and baked compounds are only reachable through `Box3D.NET.Native`.
- Single precision only. Box3D's large-world mode changes the ABI and would need
  a separate package.

[Unreleased]: https://github.com/box3d-net/Box3D.NET/commits/main
