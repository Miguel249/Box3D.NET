// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

/*
 * Runtime marshalling is switched off for this assembly.
 *
 * Without this, the LibraryImport source generator falls back to the runtime
 * marshaller for any type it cannot prove is blittable, which means a per-call
 * stub that copies each structure field by field. For an API where a single
 * frame can issue thousands of calls carrying vectors, transforms and
 * definition structs, that overhead is the whole cost of the binding.
 *
 * Disabling it makes every P/Invoke a direct call with the arguments passed as
 * they already sit in memory, and turns any accidentally non-blittable type
 * into a compile-time error rather than a silent copy. It is also what lets
 * NativeAOT generate these calls without any reflection.
 *
 * The consequences, which the binding is written to respect:
 *
 *   - bool and char are no longer marshalled. Booleans cross the boundary as
 *     NativeBool, which is one byte and matches C's _Bool. char never crosses
 *     it at all.
 *   - [MarshalAs] is ignored on parameters and return values.
 *   - Strings are never passed as System.String. Box3D takes null-terminated
 *     UTF-8, so the binding uses byte* and the high-level layer converts.
 *   - Delegates cannot be passed. Callbacks are function pointers, which is
 *     what the AOT story wants anyway.
 */

[assembly: DisableRuntimeMarshalling]
