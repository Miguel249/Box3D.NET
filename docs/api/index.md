# API reference

Two namespaces, and you almost certainly want the first.

## Box3D

The idiomatic surface. Start at `PhysicsWorld`, then `Body` and `Shape`.

Everything here validates its input, manages nothing you did not ask it to
manage, and allocates nothing on the simulation path.

## Box3D.Native

A literal mirror of the Box3D C API: the same names, the same signatures, no
abstraction. Reach for it when you need one of the roughly 580 exported
functions the idiomatic surface does not cover yet.

Nothing here validates anything or manages a lifetime. Passing an invalid
identifier crashes the process rather than raising an exception.

## Box3D.Interop

The one sanctioned bridge between the two, as extension methods. Importing this
namespace is what makes reaching for the C layer visible in your own source.