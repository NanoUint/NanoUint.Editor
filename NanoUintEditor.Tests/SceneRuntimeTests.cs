using System.Threading;
using System.Windows.Controls;
using NanoUint;
using NanoUint.Rendering;
using NanoUintEditor;
using Xunit;

namespace NanoUintEditor.Tests;

/// <summary>Integration tests for the play-mode converter (WPF objects require an STA thread).</summary>
public class SceneRuntimeTests
{
    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) throw error;
        return result!;
    }

    [Fact]
    public void Build_ConstructsEngineScene()
    {
        RunSta(() =>
        {
            var canvas = new Canvas { Width = 960, Height = 540 };
            var host = new ScenePreviewHost(canvas, 1920, 1080);
            var objects = new List<SceneObject>
            {
                new()
                {
                    Id = "a", Name = "Btn", X = 960, Y = 540, Width = 200, Height = 80, SortingOrder = 5,
                    Components = new List<ComponentData>
                    {
                        new() { Type = "Transform" },
                        new() { Type = "SpriteRenderer", Props = new() { ["sprite"] = "", ["color"] = "#FF0000" } },
                        new() { Type = "SpriteButton", Props = new() { ["sprite"] = "", ["hoverSprite"] = "", ["onClick"] = "GoNext();" } },
                    },
                },
                new()
                {
                    Id = "b", Name = "Child", ParentId = "a", X = 10, Y = 10,
                    Components = new List<ComponentData> { new() { Type = "Transform" } },
                },
            };
            SceneRuntime.Build(host, objects);

            var gos = host.Scene.RootObjects.ToList();
            Assert.Equal(3, gos.Count); // __CanvasScaler + Btn + Child
            var btn = gos.First(g => g.Name == "Btn");
            Assert.NotNull(btn.GetComponent<SpriteRenderer>());
            Assert.NotNull(btn.GetComponent<SpriteButton>());
            Assert.NotNull(btn.GetComponent<PassiveButton>()); // added automatically by SpriteButton.Awake
            Assert.Equal(0.5f, btn.Transform.X, 2);  // 960/1920 normalized
            Assert.Equal(0.5f, btn.Transform.Y, 2);  // 540/1080
            Assert.Equal(5, btn.Transform.SortingOrder);

            var child = gos.First(g => g.Name == "Child");
            Assert.Same(btn.Transform, child.Transform.Parent);
            Assert.Equal(10.0 / 1920.0, child.Transform.X, 4);

            host.Stop();
            return true;
        });
    }

    [Fact]
    public void Rebuild_ReplacesPreviousObjects()
    {
        RunSta(() =>
        {
            var canvas = new Canvas { Width = 640, Height = 360 };
            var host = new ScenePreviewHost(canvas, 1920, 1080);
            var objs1 = new List<SceneObject>
            {
                new() { Id = "x", Name = "First", Components = new List<ComponentData> { new() { Type = "Transform" } } },
            };
            var objs2 = new List<SceneObject>
            {
                new() { Id = "y", Name = "Second", Components = new List<ComponentData> { new() { Type = "Transform" } } },
            };
            SceneRuntime.Build(host, objs1);
            SceneRuntime.Build(host, objs2);
            var names = host.Scene.RootObjects.Select(g => g.Name).ToList();
            Assert.DoesNotContain("First", names);
            Assert.Contains("Second", names);
            Assert.Contains("__CanvasScaler", names); // scaler is retained
            host.Stop();
            return true;
        });
    }
}
