using NanoUintEditor;
using Xunit;

namespace NanoUintEditor.Tests;

/// <summary>Tests for SceneModel pure hierarchy logic: world position, cycle guard, dangling fix, cascade delete.</summary>
public class SceneModelTests
{
    private static SceneObject Obj(string id, string? parent = null, double x = 0, double y = 0) =>
        new() { Id = id, Name = id, ParentId = parent, X = x, Y = y };

    [Fact]
    public void WorldX_AccumulatesParents()
    {
        var objects = new List<SceneObject>
        {
            Obj("a", null, 100, 10),
            Obj("b", "a", 50, 20),
            Obj("c", "b", 25, 5),
        };
        Assert.Equal(175, SceneModel.WorldX(objects, objects[2]));
        Assert.Equal(35, SceneModel.WorldY(objects, objects[2]));
        Assert.Equal(100, SceneModel.WorldX(objects, objects[0]));
    }

    [Fact]
    public void WorldX_WithCycle_DoesNotHang()
    {
        var objects = new List<SceneObject>
        {
            Obj("a", "b", 1, 1),
            Obj("b", "a", 2, 2),
        };
        Assert.Equal(3, SceneModel.WorldX(objects, objects[0])); // stops when the cycle is reached
        Assert.Equal(3, SceneModel.WorldY(objects, objects[1]));
    }

    [Fact]
    public void IsDescendant_DetectsAncestry()
    {
        var objects = new List<SceneObject>
        {
            Obj("root", null),
            Obj("child", "root"),
            Obj("grand", "child"),
            Obj("other", null),
        };
        Assert.True(SceneModel.IsDescendant(objects, "root", "grand"));
        Assert.True(SceneModel.IsDescendant(objects, "child", "grand"));
        Assert.False(SceneModel.IsDescendant(objects, "grand", "root"));
        Assert.False(SceneModel.IsDescendant(objects, "other", "child"));
        Assert.False(SceneModel.IsDescendant(objects, "root", "root"));
    }

    [Fact]
    public void ValidateHierarchy_FixesDanglingParent()
    {
        var objects = new List<SceneObject> { Obj("a", "ghost", 0, 0) };
        SceneModel.ValidateHierarchy(objects);
        Assert.Null(objects[0].ParentId);
    }

    [Fact]
    public void ValidateHierarchy_FixesCycle()
    {
        var objects = new List<SceneObject>
        {
            Obj("a", "b"), Obj("b", "a"), Obj("c", "a"),
        };
        SceneModel.ValidateHierarchy(objects);
        Assert.Null(objects[0].ParentId);   // a→b→a is a cycle, a is detached
        Assert.Equal("a", objects[1].ParentId); // b pointing at a is fine
        Assert.Equal("a", objects[2].ParentId); // c is unaffected
    }

    [Fact]
    public void ValidateHierarchy_KeepsValidTree()
    {
        var objects = new List<SceneObject>
        {
            Obj("a", null), Obj("b", "a"), Obj("c", "b"),
        };
        SceneModel.ValidateHierarchy(objects);
        Assert.Equal("a", objects[1].ParentId);
        Assert.Equal("b", objects[2].ParentId);
    }

    [Fact]
    public void CollectDescendants_GathersAll()
    {
        var objects = new List<SceneObject>
        {
            Obj("root", null), Obj("c1", "root"), Obj("c2", "root"),
            Obj("g1", "c1"), Obj("gg", "g1"),
        };
        var list = new List<SceneObject>();
        SceneModel.CollectDescendants(objects, objects[0], list);
        Assert.Equal(4, list.Count);
        Assert.Contains(list, o => o.Id == "gg");
        Assert.DoesNotContain(list, o => o.Id == "root");
    }

    [Fact]
    public void GetChildren_DirectOnly()
    {
        var objects = new List<SceneObject>
        {
            Obj("root", null), Obj("c1", "root"), Obj("g1", "c1"),
        };
        var children = SceneModel.GetChildren(objects, "root").ToList();
        Assert.Single(children);
        Assert.Equal("c1", children[0].Id);
    }
}
