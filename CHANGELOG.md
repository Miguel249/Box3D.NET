# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Android and iOS.** The packages now carry native binaries for
  `android-arm64`, `android-x64` and iOS, alongside the six desktop runtimes.

  Android needs nothing from the consumer: the `.so` is resolved through the
  ordinary NuGet runtime-identifier mechanism and packed into the `.apk`, and
  the existing `net8.0` assembly serves it unchanged. Only the 64-bit ABIs are
  shipped. `armeabi-v7a` is deliberately absent — Google Play has required
  64-bit for years, and Box3D disables NEON on armv7, which has no divide or
  square root, so that ABI would ship a slower scalar build for devices that
  cannot be published to anyway.

  iOS is different in kind. Apple does not allow an application to load a
  dynamic library that is not a signed framework in its bundle, so Box3D is
  shipped as a static archive in an `xcframework` and linked into the
  application by a `.targets` file in `Box3D.NET.Native`. The binding therefore
  names `__Internal` rather than `box3d` there, which needs a target framework
  of its own: the packages now also target `net10.0-ios`. Every other platform
  is still served by `net8.0`. .NET 8's and 9's iOS workloads are out of support
  and the SDK refuses to build `net8.0-ios` at all, so 10 is the floor for iOS
  and only for iOS.

  That framework is off unless asked for, with `-p:Box3DTargetApple=true`, so
  building this repository still needs nothing beyond the .NET 8 SDK. The iOS
  workload has no Linux host pack — `dotnet workload install ios` fails there
  rather than installing something unusable — and making an iOS framework a
  hard requirement for building at all would have broken the platform most of
  CI runs on. The packaging jobs turn it on, on a runner that can.

  Both are verified in CI against the packed `.nupkg` rather than the
  repository: a real Android application is built and its `.apk` opened to
  confirm `libbox3d.so` is inside it, and a real iOS application is built and
  its executable read with `nm` to confirm Box3D's own native symbols are
  defined in it — 415 of them, in the run this release was cut from. A clean
  build proves nothing on iOS by itself: a P/Invoke to `__Internal` is resolved
  at run time, so an application the archive never reached builds and launches
  like a correct one.

  Only symbols named `_b3…` are counted as evidence of the link. An AOT-compiled
  method carries its parameter types in its mangled name, so `b3BodyId` and
  `b3ShapeDef` occur inside managed symbols such as
  `_Box3D_NET_Box3D_Body__ctor_Box3D_Native_b3BodyId`, which are in the binary
  whether or not the archive survived; counting those would have counted the
  wrong thing. They are counted separately instead, because they answer the
  other question — whether the trimmer kept Box3D's C# at all — and that
  distinction is what tells the two failures apart.

  Neither platform runs a simulation on a device, and the platform table in the
  README says so rather than implying otherwise.

### Changed

- `tools/build-native.ps1` now takes the target platform from the runtime
  identifier instead of from the host, since the two stopped being the same
  thing. It also builds each target in its own CMake tree — a shared one caches
  the first target's toolchain and either fails on the second or, worse,
  produces a binary for the wrong target under the right name — and finds the
  Android NDK and the CMake and Ninja bundled with the Android SDK on its own.
  Android binaries are stripped, which takes each one from about 6 MB to under
  900 KB.

- The project now states in the README and in both package descriptions that it
  was built with AI assistance.

## [0.3.0] - 2026-08-08

A hardening release. Nothing here is a new capability; it is the release that
went looking for the ways the existing ones could be wrong, and found one that
mattered.

### Fixed

- **A stale `Body`, `Shape` or `Joint` handle no longer reads freed memory.**
  This is the reason 0.3.0 exists. Box3D resolves an id by indexing into the
  world's arrays and asserting on the way past that the id is live, and those
  assertions are compiled out of the release binary this package ships. Nothing
  stood between a dead handle and the read. Measured on `win-x64` against the
  shipped 0.2.0 build:

  | | Before |
  | --- | --- |
  | `default(Body).Position` | access violation, `0xC0000005` |
  | `body.Position` after `body.Destroy()` | access violation |
  | `body.Position` after `world.Dispose()` | access violation |
  | `body.Destroy()` twice | access violation |
  | `body.AddSphere(...)` after `Destroy()` | access violation |
  | `default(Shape).Friction` | access violation |
  | `shape.Friction` after `shape.Destroy()` | returned a value from the freed slot |
  | a handle whose index had been reused | returned the *replacement* body's position, in silence |

  Every member of `Body`, `Shape`, `Joint` and the nine specific joint handles
  that dereferences a handle now asks `b3Body_IsValid`, `b3Shape_IsValid` or
  `b3Joint_IsValid` first, and throws `InvalidOperationException` if the answer
  is no. `IsValid`, handle conversions such as `hinge.AsJoint`, equality and
  `ToString` are unchanged and still never throw, so checking a handle is always
  safe. `HandleSafetyTests` covers every way a dead handle can be produced.

  The check costs 2.07 ns against the 2.66 ns of the `b3Body_GetPosition` it
  guards, measured over twenty million iterations. Declaring the predicates a
  second time with `[SuppressGCTransition]` was measured too — it brought the
  check to 1.65 ns — and rejected, because 0.4 ns does not pay for a
  hand-written duplicate of a generated binding.

  `Box3D.NET.Native` and `Box3D.Interop` are unchanged and still validate
  nothing. They are the C API, and its contract is that a handle is valid.

- The `RevoluteJoint` example in the README called `world.CastRay` and
  `world.CastRayClosest`, neither of which exists; the methods are `Raycast` and
  `RaycastClosest`.

- The visualizer's `chain` scene described itself as twelve links. It builds
  nine and a weight.

### Changed

- **Breaking, deliberately.** Members that previously killed the process or
  returned another object's state when given a dead handle now throw
  `InvalidOperationException`. No signature changed, so this does not break
  compilation and package validation against 0.2.0 reports no difference; code
  that was relying on the old behaviour was relying on undefined behaviour. Code
  that reads handles out of end-touch or sensor-end events should check
  `IsValid` first, which the documentation already said to do.

- `PackageValidationBaselineVersion` is 0.2.0. It had been left at 0.1.0 while
  0.2.0 shipped, which is a gap rather than extra strictness: comparing against
  0.1.0 says nothing about whether a member 0.2.0 added has survived.

- The README no longer describes the library as "allocation-free". The precise
  claim, and the one the tests now enforce, is that there are no managed
  allocations on the simulation hot path.

- Each animation in the gallery is capped at seventy frames. The sampling stride
  is derived per scene from how long it runs, and the frame delay from the same
  stride, so a longer scene samples itself more coarsely instead of producing a
  heavier file that plays at the same speed.

- The image sources in the README are absolute `raw.githubusercontent.com` URLs
  rather than repository-relative paths, so they render on nuget.org, which
  serves the README from its own domain. `tools/set-repository.ps1` rewrites them
  along with the rest of the repository URLs.

### Added

- `AllocationTests` holds the hot paths to allocating exactly zero managed
  bytes, measured with `GC.GetAllocatedBytesForCurrentThread`: `Step`, body
  reads and writes, body creation and destruction, shape access, both ray cast
  forms, overlap, all six event lists, a whole step-and-drain frame, joint
  access, and the character mover's collide and cast. Each test also asserts
  that the scene it measures is doing real work, so a path that stops finding
  anything fails rather than reporting a flattering zero.

- `GeometryOwnershipTests` covers what the ownership documentation claims: one
  mesh behind two hundred shapes, one height field behind sixty-four, hulls
  outliving the hull that created them, every safe teardown order, and ten
  thousand build-and-release cycles per geometry kind with Box3D's own live byte
  count required to return exactly to where it started.

- `HandleSafetyTests` covers the defect above, and pins the one case that cannot
  be caught: a `b3BodyId` records which world *slot* it came from but not that
  world's generation, so a handle held past `world.Dispose()` is
  indistinguishable from a handle into whatever world next takes the slot.
  That is a limit of the C id, not of this binding, and it is documented rather
  than hidden.

- `tools/verify-package.ps1` installs the packed `.nupkg` into a project that
  has never heard of this repository and runs a scene through it —
  framework-dependent, trimmed, and NativeAOT. Building the solution proves
  nothing about the package: a project reference resolves assemblies from `bin/`
  and copies the native library through a build target, so it never exercises
  the `runtimes/<rid>/native` layout, the NuGet asset resolution or the loader.
  CI runs it on all six supported runtime identifiers, and the release workflow
  runs it before publishing.

- CI inspects the packed `.nupkg` files: every declared runtime's native library
  present, no doubled `runtimes/` path, no build leftovers, README, licence,
  third-party notices, icon and symbol packages all there. The release workflow
  does the same on the exact files it is about to push, which cannot be
  withdrawn once pushed.

- CI fails if a native runtime is missing before packing. The native matrix runs
  with `fail-fast` disabled so one broken platform still reports the others, and
  the cost of that was that `pack` would happily produce a package missing a
  platform and call it a success. The release workflow already checked this; CI
  did not.

- A thread safety section in the README, stating for each operation whether it
  is safe concurrently, why world creation is guarded by this library rather
  than by Box3D, and what `WorkerCount` does and does not buy.

- `Workload.RequireAwake` and `Workload.RequireHits` assert that a benchmark
  scene is doing the work it claims before anything is measured. A physics
  benchmark fails quietly: Box3D skips sleeping bodies entirely, so a scene that
  settled during warm-up reports an excellent number that means nothing.

- `EventBenchmarks` measures draining the events separately from stepping, so
  the enumeration's own cost and allocation can be seen rather than being
  swamped by the step, and drains all six event lists rather than two.

- `QueryBenchmarks` compares `RaycastClosest` against the same query through the
  C API, so the wrapper's share of a query is read off rather than assumed.

- Baked compound shapes. `CompoundBuilder` collects spheres, capsules, hulls and
  meshes, `Build` bakes them into a `CompoundGeometry`, and `Body.AddCompound`
  attaches the lot as one shape. A baked compound is a single broad-phase proxy
  no matter how many children it holds, which is what makes it worth having for
  static scenery built out of hundreds of pieces, and is also why Box3D allows it
  on static bodies only — for anything that moves, attach the shapes to the body
  one at a time. Everything in the definition is cloned, so the hulls and meshes
  may be released as soon as `Build` returns; the compound itself is borrowed by
  its shape and must outlive it, like a mesh or a height field.
- `NativeInterop.ToNativePointer(CompoundGeometry)`. The C API has no
  `b3Shape_GetCompound`, so a compound is the one piece of geometry that cannot
  be read back from the shape carrying it; anything that needs its children has
  to be handed the pointer by whoever baked it.
- A visualizer, in `src/Box3D.NET.Visualizer`, and the gallery it produces in
  `docs/gallery.md`. It is a console application with a software rasterizer and
  its own PNG and GIF writers, no dependencies beyond the base class library,
  and it reaches the simulation only through `IDebugDrawer` and
  `IDebugShapeFactory`. That constraint is the point: the library is
  deliberately renderer-agnostic, so the way to show the drawing interface is
  usable is to write a renderer against it with no privileged access. Nine
  scenes cover stacking, mixed shapes, contacts and broad-phase bounds, joints,
  a wheeled vehicle, ray casts, the character mover, a baked compound and a
  height field.
- The shape factory tessellates spheres and capsules from the values Box3D
  passes it, and reads hulls, meshes and height fields back out of the engine
  through `Box3D.Interop`. A height field is drawn from the compressed grid the
  engine actually collides against, quantization included, rather than from the
  array it was built from.
- The visualizer draws the labels the engine emits through `DrawString`, from a
  5x7 bitmap font of the ninety-five printable ASCII characters, blitted in
  screen space with a one-pixel shadow. `--font-card` renders a proof sheet of
  every glyph.

### Performance

Measured on this machine, single-precision Release build, against the same
scene through the C API. See `docs/benchmarks.md` for the full conditions.

- A step is unchanged, at every scale and allocating nothing: 100 bodies
  75.29 µs through the C API against 75.48 µs wrapped, 1,000 bodies 746.82 µs
  against 745.40 µs, 10,000 bodies 7,704.64 µs against 7,723.55 µs.
- A ray cast is unchanged: `b3World_CastRayClosest` 175.4 ns against
  `RaycastClosest` 171.7 ns, allocating nothing.
- Reading a body position went to 9.496 ns against the C API's 8.998 ns. That
  0.5 ns is the validity check this release exists to add.
- Draining all six event lists after a step costs 164.5 ns and allocates
  nothing.

Two documented figures were found not to reproduce and have been corrected
rather than left standing:

- **Bulk body creation is 1.48x the C API, not the 1.13x recorded for 0.2.0.**
  Re-measured two ways, and checked against the published 0.2.0 package, which
  measures 1.54x on the same machine — so this is a documentation correction,
  not a regression. The validity check accounts for one to three points of it;
  the rest is the wrapper rebuilding `b3BodyDef` and `b3ShapeDef` per body where
  the C loop hoists them, which is what definitions being values costs. The
  claim that writing the definitions inline costs the same as hoisting them does
  not hold either: it is 1.65x against 1.48x.

- **The finite-value check is below the noise floor**, not "+0.11 ns". This run
  measured the native call *with* the check as faster than without it, which is
  impossible and settles the question in the other direction.

## [0.2.0] - 2026-08-08

### Added

- Debug draw reaches managed code. `IDebugDrawer` receives the primitives Box3D
  emits — segments, points, spheres, capsules, bounding boxes, oriented boxes,
  transforms and labels — and `PhysicsWorld.Draw` feeds them to it. The drawer
  is a value type taken by `ref`, reached through function pointers rather than
  delegates, so a drawn frame allocates nothing; a sample asserts that. Nothing
  here knows about any renderer.
- Shapes are drawn too, through `IDebugShapeFactory`. Box3D does not emit shape
  geometry as primitives: it asks the application to build a drawable once and
  then hands that opaque handle back every frame with a transform, which is what
  lets a renderer upload a mesh once instead of rebuilding it per frame. The
  factory is passed to `new PhysicsWorld(settings, factory)` rather than living
  on `WorldSettings`, because Box3D takes these callbacks in the world
  definition and because `WorldSettings` is compared field by field: a reference
  in it would stop two identical simulations comparing equal.
- The samples can be run one at a time. `--list` prints the sixteen of them and
  a name runs just that one; no argument still runs everything, which is what CI
  does.
- `ScaleBenchmarks` measures a whole frame at 100, 1,000 and 10,000 bodies
  through both the C API and the wrapper, plus stacked towers and joint chains.
  The wrapper's overhead on a step turns out not to be measurable.
- `abi/native-layout.json` records the size, alignment and every field offset of
  all 92 Box3D structs, as reported by the C compiler. `AbiTests` holds the
  managed mirrors to it and CI regenerates it, so a submodule bump that moves a
  field fails the build instead of silently reading the wrong bytes.
  `tools/dump-abi.ps1` produces the file.
- Packages are validated against the last published version at pack time, so a
  member removed or a signature changed is caught before it reaches a consumer
  rather than after.
- `BindingSource.Commit` records which Box3D revision the P/Invoke declarations
  were generated from, which the submodule pointer cannot answer for a package
  someone downloaded.

### Fixed

- The packages carry `LICENSE` and `THIRD-PARTY-NOTICES.txt`. They redistribute
  compiled Box3D binaries, and the MIT License requires its notice to accompany
  any substantial portion of the software, which a redistributed `box3d.dll` is.
  `PackageLicenseExpression` names this project's licence but puts no file in
  the package, so the obligation was unmet — while `LICENSE` stated that Box3D's
  terms accompanied the binaries. They do now.
- The generator no longer passes through a C type it does not recognise. That
  was safe for `b3`-prefixed aggregates, which have a verified mirror, and unsafe
  for anything else: an untaught C keyword was emitted verbatim as a C# type
  name, which compiles and reads the wrong width.
- The step benchmarks measured sleeping bodies, and so measured nothing. Box3D
  skips a body that has stopped moving, and every step benchmark settled its
  scene first, producing a frame cost that did not respond to body count: 214 ns
  at 100 bodies and 204 ns at 10,000. With sleep disabled those become 84 µs and
  9,842 µs.
- The README described the project as unpublished, and said the character mover,
  meshes and height fields were reachable only through the low-level layer. None
  of that had been true for some time.

## [0.1.0] - 2026-08-07

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
- `ConvexHull`, `CollisionMesh` and `HeightField`: the heavy geometry, with
  explicit ownership. Hulls are copied into the world and may be disposed at
  once; meshes and height fields are borrowed and must outlive their shapes.
- `Body.AddHull`, `AddMesh` and `AddHeightField`. The latter two reject
  non-static bodies, which Box3D only checks with an assertion that release
  builds compile out.
- The character mover primitives: `PhysicsWorld.CollideCapsule`,
  `CharacterMover.SolvePlanes` and `CharacterMover.ClipVelocity`, plus
  `PhysicsWorld.CastCapsule`. A complete kinematic controller built on them is
  in the samples; the policy it encodes stays out of the library.
- `Body.UserData`, `Shape.UserData`, `Joint.UserData` and
  `PhysicsWorld.UserData`.
- `Body.World`, `Shape.World` and `Joint.World`, returning a `WorldReference`.
- `CreateDynamicBody`, `CreateStaticBody` and `CreateKinematicBody`.
- All nine joint types — revolute, prismatic, distance, spherical, weld, wheel,
  motor, parallel and filter — each with its own handle and definition type
  covering limits, motors and springs.
- `Joint.FramesFromWorldAnchor`, which builds the matched pair of local frames
  from a world-space anchor and axis. Getting this wrong leaves a joint violated
  from the first step, and it is the part people most often get wrong by hand.
- Joint creation validates its bodies. Box3D asserts on a null, destroyed or
  self-referencing body, and asserts are compiled out of a release build, so the
  check has to happen on the managed side to produce a diagnosable error.

#### Documentation

- A navigable reference site built with DocFX from the XML documentation,
  published to GitHub Pages on every push so it cannot drift from the code.
- `docs/getting-started.md`: from installation to ray casts, events, filtering,
  joints and terrain, ending with the mistakes that cost the most time.
- `docs/architecture.md`: diagrams of the layer boundary, what owns what, a
  frame, the query callback machinery and the build pipeline.
- A package icon, generated by `tools/generate-icon.ps1` so it can be reviewed
  as code and regenerated at any size rather than committed as an opaque binary.

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

- Non-finite input is rejected on every path that reaches the solver: body and
  world creation, velocities, forces, impulses, teleports, gravity, shapes, ray
  casts and the time step. Measured at 0.11 ns per check.

### Fixed

- Creating or disposing a world from several threads at once could corrupt the
  engine's global table of worlds and leave the simulation spinning inside
  `Step` forever. Box3D picks a slot for a new world without synchronising and
  asks the caller to hold a mutex; `PhysicsWorld` now holds one across creation
  and disposal. Stepping, queries and body edits are untouched.
- The native libraries were packed one directory level too deep, as
  `runtimes/<rid>/native/<rid>/native/`, where NuGet resolves nothing. The
  package installed cleanly and then failed on the first call into the engine.
- The XML documentation and the AOT and trim analyzers were configured under a
  condition that always evaluated false, so none of them ever ran. Everything
  depending on `IsPackable` moved to `Directory.Build.targets`, and CI now
  asserts the gates directly instead of inferring them from a green build.
  That move fixed the analyzers but not the documentation: the SDK turns
  `GenerateDocumentationFile` into a path before `Directory.Build.targets` is
  imported, so the flag read true while the path stayed empty, no `.xml` was
  emitted and CS1591 never fired. `DocumentationFile` is now set explicitly,
  and CI asserts the path rather than the flag.
- Sphere, capsule and box constructors accepted an infinite extent, and
  `World.Step` accepted an infinite time step. `ThrowIfNegativeOrZero` passes
  an infinity through, since it is neither negative nor zero. Found by fuzzing.
- Tests that read the process-wide allocated byte count ran in parallel with
  tests that allocated, so the leak checks were measuring other threads. Tests
  touching the native library now share a non-parallel collection.

### Known limitations

- Baked compound shapes are only reachable through `Box3D.NET.Native`. So was
  debug draw at the time of this release; see Unreleased.
- The cylinder and cone hulls stand on the origin rather than being centred on
  it, and a height field grows from its body origin in positive x and z. Both
  follow Box3D and are documented rather than corrected.
- Single precision only. Box3D's large-world mode changes the ABI and would need
  a separate package.
[Unreleased]: https://github.com/Miguel249/Box3D.NET/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/Miguel249/Box3D.NET/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Miguel249/Box3D.NET/releases/tag/v0.1.0
