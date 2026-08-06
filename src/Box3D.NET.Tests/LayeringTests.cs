// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Enforces the boundary between the idiomatic API and the raw binding.
/// </summary>
/// <remarks>
/// <para>
/// The layering rule is that <c>Box3D.NET</c> may depend on
/// <c>Box3D.NET.Native</c> internally but must never name one of its types in
/// public API. Otherwise every consumer that touches a handle picks up a
/// compile-time dependency on the C ABI, and the two packages can no longer
/// version independently.
/// </para>
/// <para>
/// This is a rule that decays quietly: one convenient property is all it takes,
/// and nothing fails. So it is checked mechanically over the built assembly
/// rather than trusted to review.
/// </para>
/// </remarks>
public class LayeringTests
{
    private const string NativeNamespace = "Box3D.Native";

    // Box3D.Interop is the sanctioned escape hatch. Its whole purpose is to
    // convert between the two layers, so it is the one place allowed to name
    // both, and it is a separate namespace precisely so that using it is a
    // visible decision in the consumer's source.
    private const string InteropNamespace = "Box3D.Interop";

    private static IEnumerable<Type> PublicTypesOfIdiomaticApi =>
        typeof(PhysicsWorld).Assembly
            .GetExportedTypes()
            .Where(t => t.Namespace is not null && !t.Namespace.StartsWith(InteropNamespace, StringComparison.Ordinal));

    private static bool IsNativeType(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        // Unwrap arrays, by-ref parameters, pointers and generic arguments so
        // that Span<b3BodyId> or b3BodyId* is caught as readily as b3BodyId.
        while (type.HasElementType)
        {
            type = type.GetElementType();
            if (type is null)
            {
                return false;
            }
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(IsNativeType))
        {
            return true;
        }

        return type.Namespace?.StartsWith(NativeNamespace, StringComparison.Ordinal) == true;
    }

    [Fact]
    public void No_public_property_exposes_a_native_type()
    {
        List<string> offenders = [];

        foreach (Type type in PublicTypesOfIdiomaticApi)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (IsNativeType(property.PropertyType))
                {
                    offenders.Add($"{type.Name}.{property.Name} returns {property.PropertyType.Name}");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_public_method_takes_or_returns_a_native_type()
    {
        List<string> offenders = [];

        foreach (Type type in PublicTypesOfIdiomaticApi)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // Property accessors are covered by the test above.
                if (method.IsSpecialName)
                {
                    continue;
                }

                if (IsNativeType(method.ReturnType))
                {
                    offenders.Add($"{type.Name}.{method.Name} returns {method.ReturnType.Name}");
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    if (IsNativeType(parameter.ParameterType))
                    {
                        offenders.Add($"{type.Name}.{method.Name} takes {parameter.ParameterType.Name} {parameter.Name}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_public_field_exposes_a_native_type()
    {
        List<string> offenders = [];

        foreach (Type type in PublicTypesOfIdiomaticApi)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (IsNativeType(field.FieldType))
                {
                    offenders.Add($"{type.Name}.{field.Name} is {field.FieldType.Name}");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_public_type_derives_from_or_implements_a_native_type()
    {
        List<string> offenders = [];

        foreach (Type type in PublicTypesOfIdiomaticApi)
        {
            if (IsNativeType(type.BaseType))
            {
                offenders.Add($"{type.Name} derives from {type.BaseType!.Name}");
            }

            foreach (Type contract in type.GetInterfaces().Where(IsNativeType))
            {
                offenders.Add($"{type.Name} implements {contract.Name}");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_interop_namespace_is_the_only_place_that_bridges_the_layers()
    {
        // The counterpart to the tests above: the escape hatch must actually
        // exist, or sealing the layers would leave callers with no way down.
        Type[] interopTypes = typeof(PhysicsWorld).Assembly
            .GetExportedTypes()
            .Where(t => t.Namespace == InteropNamespace)
            .ToArray();

        Assert.NotEmpty(interopTypes);

        bool bridges = interopTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Any(m => IsNativeType(m.ReturnType) || m.GetParameters().Any(p => IsNativeType(p.ParameterType)));

        Assert.True(bridges, $"{InteropNamespace} exists but converts nothing");
    }
}
