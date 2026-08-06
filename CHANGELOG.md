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
- All nine joint types — revolute, prismatic, distance, spherical, weld, wheel,
  motor, parallel and filter — each with its own handle and definition type
  covering limits, motors and springs.
- `Joint.FramesFromWorldAnchor`, which builds the matched pair of local frames
  from a world-space anchor and axis. Getting this wrong leaves a joint violated
  from the first step, and it is the part people most often get wrong by hand.
- Joint creation validates its bodies. Box3D asserts on a null, destroyed or
  self-referencing body, and asserts are compiled out of a release build, so the
  check has to happen on the managed side to produce a diagnosable error.

#### Infrastructure

- Box3D pinned as an unmodified submodule at `3fc20f5`.
- `tools/build-native.ps1`, building the shared library and staging it under
  `runtimes/<rid>/native` for both packaging and local runs.
- Continuous integration building the native library for six platforms, testing
  on three, verifying formatting, verifying the generated bindings still match
  the submodule, and publishing the samples with NativeAOT.
- A release workflow driven by a version tag, which refuses to publish unless
  every platform's binary is present.
- Twelve samples covering a basic world, dynamic and static bodies, collisions,
  ray casts, contact events, sensors, compound bodies, continuous collision, a
  kinematic character, a hinged door, a hanging chain and a wheeled vehicle.

### Changed

- `PhysicsWorld.Id`, `Body.Id`, `Shape.Id` and the joint `Id` properties are
  now internal. Conversions live in the new `Box3D.Interop` namespace, so the
  idiomatic surface no longer names a native type. **Breaking.**
- Spatial queries renamed to `Raycast` and `RaycastClosest`, matching the
  `RaycastHit` and `IRaycastCallback` types they already used. **Breaking.**
- Engine defaults are read once into a cache rather than on every definition
  construction and conversion, which removed two P/Invokes per body creation.
- Creating a body or shape no longer reserves a name buffer it does not need.

### Fixed

- The XML documentation and the AOT and trim analyzers were configured under a
  condition that always evaluated false, so none of them ever ran. Everything
  depending on `IsPackable` moved to `Directory.Build.targets`, and CI now
  asserts the gates directly instead of inferring them from a green build.
- Tests that read the process-wide allocated byte count ran in parallel with
  tests that allocated, so the leak checks were measuring other threads. Tests
  touching the native library now share a non-parallel collection.

### Known limitations

- The character mover plane solver, debug draw, meshes, height fields and baked
  compounds are only reachable through `Box3D.NET.Native`.
- Single precision only. Box3D's large-world mode changes the ABI and would need
  a separate package.

[Unreleased]: https://github.com/box3d-net/Box3D.NET/commits/main
