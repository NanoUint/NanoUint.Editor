using NanoUintEditor;
using Xunit;

namespace NanoUintEditor.Tests;

public class UndoManagerTests
{
    private static List<SceneObject> Snap(int n) =>
        Enumerable.Range(0, n).Select(i => new SceneObject { Id = $"o{i}", Name = $"O{i}", X = i * 10 }).ToList();

    [Fact]
    public void Undo_ReturnsLastSnapshot()
    {
        var um = new UndoManager();
        var s0 = Snap(2);
        var s1 = Snap(3);
        um.Push(s0);
        um.Push(s1);
        Assert.True(um.CanUndo);
        var restored = um.Undo(Snap(4));
        Assert.NotNull(restored);
        Assert.Equal(3, restored.Count);   // snapshot taken before the most recent operation
        Assert.True(um.CanUndo);
        var restored2 = um.Undo(Snap(4));
        Assert.NotNull(restored2);
        Assert.Equal(2, restored2.Count);
        Assert.False(um.CanUndo);
    }

    [Fact]
    public void Redo_RestoresAfterUndo()
    {
        var um = new UndoManager();
        var s0 = Snap(1);
        var s1 = Snap(5);
        um.Push(s0);
        um.Push(s1);
        var undoneState = Snap(2);       // simulates the current state (after the operation) before undo
        um.Undo(undoneState);
        Assert.True(um.CanRedo);
        var redone = um.Redo(Snap(1));   // redo restores the undone state
        Assert.NotNull(redone);
        Assert.Equal(2, redone.Count);
        Assert.Same(undoneState, redone);
        Assert.False(um.CanRedo);
    }

    [Fact]
    public void NewPush_ClearsRedo()
    {
        var um = new UndoManager();
        um.Push(Snap(1));
        um.Undo(Snap(2));
        Assert.True(um.CanRedo);
        um.Push(Snap(3));
        Assert.False(um.CanRedo);
    }

    [Fact]
    public void Capacity_TrimsOldest()
    {
        var um = new UndoManager(3);
        for (int i = 0; i < 6; i++) um.Push(Snap(i));
        // undo down to the capacity boundary: only the newest 3 remain (Snap(3..5))
        var r1 = um.Undo(Snap(99));
        var r2 = um.Undo(Snap(99));
        var r3 = um.Undo(Snap(99));
        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.NotNull(r3);
        Assert.Equal(5, r1.Count);
        Assert.Equal(4, r2.Count);
        Assert.Equal(3, r3.Count);
        Assert.Null(um.Undo(Snap(99)));
    }

    [Fact]
    public void CloneObjects_DeepCopiesComponents()
    {
        var objects = new List<SceneObject>
        {
            new()
            {
                Id = "a1", Name = "A", X = 1, ParentId = null,
                Components = new List<ComponentData>
                {
                    new() { Type = "Transform", Props = new() { ["X"] = 1.0, ["sprite"] = "x.png" } },
                },
            },
        };
        var clone = SceneModel.CloneObjects(objects);
        Assert.Single(clone);
        Assert.Equal("a1", clone[0].Id);
        Assert.NotSame(objects[0], clone[0]);
        Assert.NotSame(objects[0].Components[0], clone[0].Components[0]);
        clone[0].Components[0].Props["X"] = 999.0;
        Assert.Equal(1.0, objects[0].Components[0].Props["X"]); // the original object is unaffected
    }
}
