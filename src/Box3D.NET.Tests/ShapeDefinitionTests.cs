// SPDX-License-Identifier: MIT

using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Covers the friction and restitution shortcuts on <see cref="ShapeDefinition"/>.
/// </summary>
[Collection(NativeCollection.Name)]
public class ShapeDefinitionTests
{
    [NativeFact]
    public void The_friction_shortcut_changes_only_the_friction()
    {
        ShapeDefinition def = ShapeDefinition.Default with { Friction = 0.7f };

        Assert.Equal(0.7f, def.Friction);
        Assert.Equal(PhysicsMaterial.Default with { Friction = 0.7f }, def.Material);
    }

    [NativeFact]
    public void The_restitution_shortcut_changes_only_the_restitution()
    {
        ShapeDefinition def = ShapeDefinition.Default with { Restitution = 0.8f };

        Assert.Equal(0.8f, def.Restitution);
        Assert.Equal(PhysicsMaterial.Default with { Restitution = 0.8f }, def.Material);
    }

    [NativeFact]
    public void A_shortcut_is_the_same_definition_as_the_nested_form()
    {
        ShapeDefinition shortcut = ShapeDefinition.Default with { Friction = 0.7f, Restitution = 0.2f };
        ShapeDefinition nested = ShapeDefinition.Default with
        {
            Material = PhysicsMaterial.Default with { Friction = 0.7f, Restitution = 0.2f },
        };

        // Nothing is stored outside Material, so equality and hashing agree.
        Assert.Equal(nested, shortcut);
        Assert.Equal(nested.GetHashCode(), shortcut.GetHashCode());
    }

    [NativeFact]
    public void A_shortcut_after_a_material_refines_it()
    {
        var ice = PhysicsMaterial.Default with { Friction = 0.02f, UserMaterialId = 7 };

        ShapeDefinition def = ShapeDefinition.Default with { Material = ice, Restitution = 0.5f };

        Assert.Equal(0.02f, def.Friction);
        Assert.Equal(0.5f, def.Restitution);
        Assert.Equal(7UL, def.Material.UserMaterialId);
    }

    [NativeFact]
    public void The_shortcuts_reach_the_shape()
    {
        using var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody();

        Shape shape = body.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with { Friction = 0.7f, Restitution = 0.3f });

        Assert.Equal(0.7f, shape.Friction);
        Assert.Equal(0.3f, shape.Restitution);
    }
}
