namespace NanoUintEditor;

/// <summary>State of an active in-place text editing session.</summary>
public sealed class TextEditSession
{
    public string ElementId { get; init; } = "";
    public int PageIndex { get; init; }
    public string PropertyKey { get; init; } = "content";
    public string OriginalValue { get; init; } = "";
    public string CurrentValue { get; set; }
    public bool IsCommitted { get; private set; }
    public bool IsCancelled { get; private set; }

    public TextEditSession(string elementId, int pageIndex, string propertyKey, string originalValue)
    {
        ElementId = elementId;
        PageIndex = pageIndex;
        PropertyKey = propertyKey;
        OriginalValue = originalValue;
        CurrentValue = originalValue;
    }

    public void Commit() => IsCommitted = true;
    public void Cancel() { IsCancelled = true; CurrentValue = OriginalValue; }

    public IUndoableAction ToAction(SlideDocument doc)
    {
        var page = doc.Pages.ElementAtOrDefault(PageIndex);
        var elem = page?.Elements.FirstOrDefault(e => e.Id == ElementId);
        if (elem == null)
            return new CompositeAction { Description = "Text edit (no-op)" };

        return new SetPropertyAction<string>(elem, PropertyKey, OriginalValue, CurrentValue);
    }
}

/// <summary>Manages in-place text editing sessions for elements on the canvas.</summary>
public sealed class TextEditManager
{
    private readonly SlideDocument _doc;
    private readonly UndoManagerV2 _undo;
    private TextEditSession? _activeSession;

    public TextEditSession? ActiveSession => _activeSession;
    public bool IsEditing => _activeSession != null;

    /// <summary>Fired when an editing session starts.</summary>
    public event Action<TextEditSession>? SessionStarted;

    /// <summary>Fired when an editing session ends (committed or cancelled).</summary>
    public event Action<TextEditSession>? SessionEnded;

    public TextEditManager(SlideDocument doc, UndoManagerV2 undo)
    {
        _doc = doc;
        _undo = undo;
    }

    /// <summary>Starts editing a text property on the given element.</summary>
    public TextEditSession? StartEdit(
        SlidePage page,
        SlideElement element,
        string propertyKey = "content",
        int pageIndex = 0)
    {
        if (_activeSession != null) return null; // Already editing

        var value = element.Props.GetValueOrDefault(propertyKey)?.ToString() ?? "";
        var session = new TextEditSession(element.Id, pageIndex, propertyKey, value);
        _activeSession = session;
        SessionStarted?.Invoke(session);
        return session;
    }

    /// <summary>Updates the current text during editing.</summary>
    public void UpdateText(string text)
    {
        if (_activeSession == null) return;
        _activeSession.CurrentValue = text;

        // Live preview: update the element's property in real time
        var page = _doc.Pages.ElementAtOrDefault(_activeSession.PageIndex);
        var elem = page?.Elements.FirstOrDefault(e => e.Id == _activeSession.ElementId);
        if (elem != null)
            elem.Props[_activeSession.PropertyKey] = text;
    }

    /// <summary>Commits the current edit and pushes an undo action.</summary>
    public IUndoableAction? CommitEdit()
    {
        if (_activeSession == null) return null;

        var session = _activeSession;
        _activeSession = null;

        if (session.IsCancelled)
        {
            // Restore original value
            var page = _doc.Pages.ElementAtOrDefault(session.PageIndex);
            var elem = page?.Elements.FirstOrDefault(e => e.Id == session.ElementId);
            if (elem != null)
                elem.Props[session.PropertyKey] = session.OriginalValue;

            SessionEnded?.Invoke(session);
            return null;
        }

        session.Commit();
        if (session.CurrentValue != session.OriginalValue)
        {
            var action = session.ToAction(_doc);
            _undo.Push(action);
            SessionEnded?.Invoke(session);
            return action;
        }

        SessionEnded?.Invoke(session);
        return null;
    }

    /// <summary>Cancels the current edit, restoring the original value.</summary>
    public void CancelEdit()
    {
        if (_activeSession == null) return;
        _activeSession.Cancel();
        CommitEdit(); // Commits the cancel (restores original)
    }
}

/// <summary>Handles keyboard/mouse events during text editing.</summary>
public static class TextEditInputHandler
{
    /// <summary>Processes a key press during text editing. Returns true if handled.</summary>
    public static bool HandleKey(TextEditManager manager, string key, bool isCtrlDown)
    {
        if (!manager.IsEditing) return false;

        switch (key)
        {
            case "Escape":
                manager.CancelEdit();
                return true;
            case "Return":
                if (!isCtrlDown) // Enter commits; Ctrl+Enter for newline
                {
                    manager.CommitEdit();
                    return true;
                }
                return false; // Let Ctrl+Enter create newline
            default:
                return false;
        }
    }

    /// <summary>Returns the text editing cursor position (character index) given a canvas click point.</summary>
    public static int GetCaretPosition(
        SlideElement element,
        double canvasX,
        double canvasY,
        double zoom,
        string fontFamily = "Segoe UI",
        double fontSize = 24)
    {
        // Simplified: estimate character index from click X position relative to element
        var content = element.Props.GetValueOrDefault("content")?.ToString() ?? "";
        if (string.IsNullOrEmpty(content)) return 0;

        double relativeX = (canvasX - element.X) * zoom;
        double charWidth = fontSize * 0.6; // Approximate average character width
        int index = (int)Math.Round(relativeX / charWidth);
        return Math.Clamp(index, 0, content.Length);
    }
}
