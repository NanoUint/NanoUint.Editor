using Xunit;

namespace NanoUintEditor.Tests;

public class SlideDocumentTests
{
    [Fact]
    public void SlideDocument_Default_HasVersionAndId()
    {
        var doc = new SlideDocument();
        Assert.Equal(1, doc.FormatVersion);
        Assert.False(string.IsNullOrEmpty(doc.DocumentId));
        Assert.Equal("Untitled", doc.Title);
    }

    [Fact]
    public void SlidePage_Default_HasId()
    {
        var page = new SlidePage();
        Assert.False(string.IsNullOrEmpty(page.Id));
        Assert.Equal("Page", page.Name);
        Assert.Equal("None", page.TransitionType);
    }

    [Fact]
    public void SlideElement_Default_HasId()
    {
        var elem = new SlideElement();
        Assert.False(string.IsNullOrEmpty(elem.Id));
        Assert.Equal("Element", elem.Name);
        Assert.Equal("Image", elem.Type);
        Assert.Equal(1.0, elem.Opacity);
        Assert.True(elem.Visible);
    }

    [Fact]
    public void SlideDocument_SerializeRoundTrip()
    {
        var doc = new SlideDocument
        {
            Title = "Test Doc",
            Theme = new SlideTheme { FontSize = 32 }
        };
        doc.Pages.Add(new SlidePage
        {
            Name = "Page 1",
            Elements = new List<SlideElement>
            {
                new() { Name = "BG", Type = "Image", Props = new() { ["spritePath"] = "bg.png" } },
                new() { Name = "Text1", Type = "Text", Props = new() { ["content"] = "Hello" } },
            }
        });

        var json = doc.ToJson();
        var loaded = SlideDocument.FromJson(json);

        Assert.NotNull(loaded);
        Assert.Equal("Test Doc", loaded!.Title);
        Assert.Single(loaded.Pages);
        Assert.Equal("Page 1", loaded.Pages[0].Name);
        Assert.Equal(2, loaded.Pages[0].Elements.Count);
        Assert.Equal("Image", loaded.Pages[0].Elements[0].Type);
        Assert.Equal("bg.png", loaded.Pages[0].Elements[0].Props["spritePath"]!.ToString());
    }

    [Fact]
    public void SlideDocument_InvalidJson_ReturnsNull()
    {
        var result = SlideDocument.FromJson("not json");
        Assert.Null(result);
    }

    [Fact]
    public void SlideDocument_Theme_Defaults()
    {
        var theme = new SlideTheme();
        Assert.Equal("Default", theme.Name);
        Assert.Equal(24, theme.FontSize);
    }

    [Fact]
    public void SlideDocument_Pages_ListIsEmpty()
    {
        var doc = new SlideDocument();
        Assert.Empty(doc.Pages);
        Assert.Null(doc.CurrentPage);
    }

    [Fact]
    public void SlideDocument_CurrentPageIndex_BoundsCheck()
    {
        var doc = new SlideDocument();
        doc.Pages.Add(new SlidePage());
        doc.CurrentPageIndex = 0;
        Assert.NotNull(doc.CurrentPage);
    }

    [Fact]
    public void ToSceneObjects_EmptyPage_ReturnsEmpty()
    {
        var doc = new SlideDocument();
        doc.Pages.Add(new SlidePage());
        var objects = doc.ToSceneObjects(0);
        Assert.Empty(objects);
    }

    [Fact]
    public void ToSceneObjects_ImageElement_CreatesSpriteRenderer()
    {
        var doc = new SlideDocument();
        var page = new SlidePage();
        page.Elements.Add(new SlideElement
        {
            Name = "BG",
            Type = "Image",
            X = 100, Y = 200,
            Props = new() { ["spritePath"] = "bg.png" }
        });
        doc.Pages.Add(page);

        var objects = doc.ToSceneObjects(0);
        Assert.Single(objects);
        Assert.Equal("BG", objects[0].Name);
        Assert.True(objects[0].HasComponent("SpriteRenderer"));
        Assert.Equal(100.0, objects[0].X);
    }

    [Fact]
    public void ToSceneObjects_TextElement_CreatesTextRenderer()
    {
        var doc = new SlideDocument();
        var page = new SlidePage();
        page.Elements.Add(new SlideElement
        {
            Name = "Label",
            Type = "Text",
            Props = new() { ["content"] = "Hello", ["fontSize"] = 32.0 }
        });
        doc.Pages.Add(page);

        var objects = doc.ToSceneObjects(0);
        Assert.Single(objects);
        Assert.True(objects[0].HasComponent("TextRenderer"));
        var tr = objects[0].GetComponent("TextRenderer");
        Assert.Equal("Hello", tr!.Props["text"]);
    }

    [Fact]
    public void FromSceneObjects_CreatesSlidePage()
    {
        var sceneObjs = new List<SceneObject>
        {
            new()
            {
                Name = "BG",
                Components = new List<ComponentData>
                {
                    new() { Type = "Transform", Props = new() { ["X"] = 0.5 } },
                    new() { Type = "SpriteRenderer", Props = new() { ["sprite"] = "bg.png" } },
                }
            }
        };

        var page = SlideDocumentExtensions.FromSceneObjects("Test", sceneObjs);
        Assert.Equal("Test", page.Name);
        Assert.Single(page.Elements);
        Assert.Equal("Image", page.Elements[0].Type);
        Assert.Equal("bg.png", page.Elements[0].Props["spritePath"]);
    }

    [Fact]
    public void SlideDocument_FormatVersion_IsOne()
    {
        Assert.Equal(1, DocumentFormat.CurrentVersion);
    }
}
