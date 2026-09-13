namespace NanoUintEditor;

/// <summary>A single undoable action (command pattern).</summary>
public interface IUndoableAction
{
    string Description { get; }
    void Execute();
    void Undo();
    void Redo();

    /// <summary>Whether this action can be merged with a subsequent action of the same type.</summary>
    bool CanMergeWith(IUndoableAction other) => false;

    /// <summary>Merge the other action into this one (absorbs the change).</summary>
    void MergeWith(IUndoableAction other) { }
}

/// <summary>A composite action that bundles multiple actions as one undo step.</summary>
public sealed class CompositeAction : IUndoableAction
{
    private readonly List<IUndoableAction> _actions = new();
    public string Description { get; set; } = "";
    public IReadOnlyList<IUndoableAction> Actions => _actions;

    public void Add(IUndoableAction action) => _actions.Add(action);

    public void Execute()
    {
        foreach (var a in _actions) a.Execute();
    }

    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--) _actions[i].Undo();
    }

    public void Redo()
    {
        foreach (var a in _actions) a.Redo();
    }
}

/// <summary>Action that moves an element to a new position (supports merge for drag).</summary>
public sealed class MoveElementAction : IUndoableAction
{
    private readonly SlideDocument? _doc;
    private readonly SlideElement? _elem;
    internal readonly string ElementId;
    internal readonly int PageIndex;
    private readonly double _oldX, _oldY;
    private double _newX, _newY;

    public string Description => "Move element";
    public bool IsDragAction { get; set; }

    /// <summary>Creates a move action that looks up the element by ID from the document.</summary>
    public MoveElementAction(SlideDocument doc, string elementId, int pageIndex,
        double oldX, double oldY, double newX, double newY)
    {
        _doc = doc;
        ElementId = elementId;
        PageIndex = pageIndex;
        _oldX = oldX; _oldY = oldY;
        _newX = newX; _newY = newY;
    }

    /// <summary>Creates a move action with a direct element reference (no doc lookup needed).</summary>
    public MoveElementAction(SlideElement element, double oldX, double oldY, double newX, double newY)
    {
        _elem = element;
        ElementId = element.Id;
        PageIndex = -1;
        _oldX = oldX; _oldY = oldY;
        _newX = newX; _newY = newY;
    }

    public void Execute() => ApplyPosition(_newX, _newY);
    public void Undo() => ApplyPosition(_oldX, _oldY);
    public void Redo() => ApplyPosition(_newX, _newY);

    public bool CanMergeWith(IUndoableAction other)
    {
        return other is MoveElementAction m
            && m.ElementId == ElementId
            && m.PageIndex == PageIndex
            && IsDragAction && m.IsDragAction;
    }

    public void MergeWith(IUndoableAction other)
    {
        if (other is MoveElementAction m)
            SetNewPosition(m._newX, m._newY);
    }

    internal void SetNewPosition(double x, double y) { _newX = x; _newY = y; }

    private void ApplyPosition(double x, double y)
    {
        var elem = _elem;
        if (elem == null && _doc != null && PageIndex >= 0 && PageIndex < _doc.Pages.Count)
            elem = _doc.Pages[PageIndex].Elements.FirstOrDefault(e => e.Id == ElementId);
        if (elem != null) { elem.X = x; elem.Y = y; }
    }
}

/// <summary>Action that adds an element to a page.</summary>
public sealed class AddElementAction : IUndoableAction
{
    private readonly SlideDocument _doc;
    private readonly SlidePage _page;
    private readonly SlideElement _element;
    public string Description => $"Add '{_element.Name}'";

    public AddElementAction(SlideDocument doc, SlidePage page, SlideElement element)
    {
        _doc = doc; _page = page; _element = element;
    }

    public void Execute() => _page.Elements.Add(_element);
    public void Undo() => _page.Elements.Remove(_element);
    public void Redo() => _page.Elements.Add(_element);
}

/// <summary>Action that removes an element from a page.</summary>
public sealed class RemoveElementAction : IUndoableAction
{
    private readonly SlideDocument _doc;
    private readonly SlidePage _page;
    private readonly SlideElement _element;
    private readonly int _index;
    public string Description => $"Remove '{_element.Name}'";

    public RemoveElementAction(SlideDocument doc, SlidePage page, SlideElement element)
    {
        _doc = doc; _page = page; _element = element;
        _index = page.Elements.IndexOf(element);
    }

    public void Execute() => _page.Elements.Remove(_element);
    public void Undo()
    {
        if (_index >= 0 && _index <= _page.Elements.Count)
            _page.Elements.Insert(_index, _element);
        else
            _page.Elements.Add(_element);
    }
    public void Redo() => _page.Elements.Remove(_element);
}

/// <summary>Action that modifies an element property.</summary>
public sealed class SetPropertyAction<T> : IUndoableAction
{
    private readonly SlideElement _element;
    private readonly string _propName;
    private readonly T _oldValue;
    private readonly T _newValue;
    public string Description => $"Set {_propName}";

    public SetPropertyAction(SlideElement element, string propName, T oldValue, T newValue)
    {
        _element = element; _propName = propName;
        _oldValue = oldValue; _newValue = newValue;
    }

    public void Execute() => ApplyValue(_newValue);
    public void Undo() => ApplyValue(_oldValue);
    public void Redo() => ApplyValue(_newValue);

    private void ApplyValue(T value)
    {
        if (_element.Props.ContainsKey(_propName))
            _element.Props[_propName] = value;
        else
            _element.Props.Add(_propName, value);
    }
}

/// <summary>Action that adds a page.</summary>
public sealed class AddPageAction : IUndoableAction
{
    private readonly SlideDocument _doc;
    private readonly SlidePage _page;
    private readonly int _index;
    public string Description => $"Add page '{_page.Name}'";

    public AddPageAction(SlideDocument doc, SlidePage page, int index = -1)
    {
        _doc = doc; _page = page;
        _index = index >= 0 ? index : doc.Pages.Count;
    }

    public void Execute()
    {
        if (_index <= _doc.Pages.Count)
            _doc.Pages.Insert(_index, _page);
        else
            _doc.Pages.Add(_page);
    }
    public void Undo() => _doc.Pages.Remove(_page);
    public void Redo() => Execute();
}

/// <summary>Action that removes a page.</summary>
public sealed class RemovePageAction : IUndoableAction
{
    private readonly SlideDocument _doc;
    private readonly SlidePage _page;
    private readonly int _index;
    public string Description => $"Remove page '{_page.Name}'";

    public RemovePageAction(SlideDocument doc, SlidePage page)
    {
        _doc = doc; _page = page;
        _index = doc.Pages.IndexOf(page);
    }

    public void Execute() => _doc.Pages.Remove(_page);
    public void Undo()
    {
        if (_index >= 0 && _index <= _doc.Pages.Count)
            _doc.Pages.Insert(_index, _page);
        else
            _doc.Pages.Add(_page);
    }
    public void Redo() => _doc.Pages.Remove(_page);
}

/// <summary>Transaction scope for batching actions. Disposing commits or reverts.</summary>
public sealed class UndoTransaction : IDisposable
{
    private readonly UndoManagerV2 _manager;
    private readonly CompositeAction _composite;
    private bool _committed;

    internal UndoTransaction(UndoManagerV2 manager, string description)
    {
        _manager = manager;
        _composite = new CompositeAction { Description = description };
    }

    public void AddAction(IUndoableAction action) => _composite.Add(action);

    public void Dispose()
    {
        if (!_committed && _composite.Actions.Count > 0)
        {
            _manager.CommitTransaction(_composite);
            _committed = true;
        }
    }

    public void Revert()
    {
        if (!_committed)
        {
            _composite.Undo();
            _committed = true;
        }
    }
}

/// <summary>Command-based undo/redo manager with merge window support.</summary>
public class UndoManagerV2
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();
    private readonly int _capacity;
    private readonly TimeSpan _mergeWindow;

    /// <summary>Maximum time between consecutive actions for auto-merge.</summary>
    public TimeSpan MergeWindow
    {
        get => _mergeWindow;
        init => _mergeWindow = value;
    }

    private DateTime _lastPushTime = DateTime.MinValue;

    public UndoManagerV2(int capacity = 100, int mergeWindowMs = 500)
    {
        _capacity = capacity;
        _mergeWindow = TimeSpan.FromMilliseconds(mergeWindowMs);
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string UndoDescription => _undo.Count > 0 ? _undo.Peek().Description : "";
    public string RedoDescription => _redo.Count > 0 ? _redo.Peek().Description : "";

    /// <summary>Pushes an action (executed separately). Supports auto-merge for drag actions.</summary>
    public void Push(IUndoableAction action)
    {
        var now = DateTime.UtcNow;
        if (_undo.Count > 0
            && (now - _lastPushTime) < _mergeWindow
            && _undo.Peek().CanMergeWith(action))
        {
            _undo.Peek().MergeWith(action);
        }
        else
        {
            _undo.Push(action);
            TrimToCapacity();
        }
        _lastPushTime = now;
        _redo.Clear();
    }

    /// <summary>Begins a scoped transaction. Actions added via the transaction are batched.</summary>
    public UndoTransaction Begin(string description) => new(this, description);

    internal void CommitTransaction(CompositeAction composite)
    {
        Push(composite);
    }

    /// <summary>Undo the last action. Returns the action or null.</summary>
    public IUndoableAction? Undo()
    {
        if (_undo.Count == 0) return null;
        var action = _undo.Pop();
        action.Undo();
        _redo.Push(action);
        return action;
    }

    /// <summary>Redo the last undone action. Returns the action or null.</summary>
    public IUndoableAction? Redo()
    {
        if (_redo.Count == 0) return null;
        var action = _redo.Pop();
        action.Redo();
        _undo.Push(action);
        return action;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private void TrimToCapacity()
    {
        if (_undo.Count > _capacity)
        {
            var arr = _undo.ToArray();
            _undo.Clear();
            for (int i = _capacity - 1; i >= 0; i--) _undo.Push(arr[i]);
        }
    }
}
