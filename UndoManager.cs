namespace NanoUintEditor;

/// <summary>Snapshot-based undo stack for the scene editor: each entry is a full clone of the object list.</summary>
public sealed class UndoManager
{
    private readonly Stack<List<SceneObject>> _undo = new();
    private readonly Stack<List<SceneObject>> _redo = new();
    private readonly int _capacity;

    public UndoManager(int capacity = 100) => _capacity = capacity;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records the state before a change. Clears the redo stack.</summary>
    public void Push(List<SceneObject> snapshot)
    {
        _undo.Push(snapshot);
        _redo.Clear();
        while (_undo.Count > _capacity)
        {
            var arr = _undo.ToArray();
            _undo.Clear();
            for (int i = _capacity - 1; i >= 0; i--) _undo.Push(arr[i]);
        }
    }

    /// <summary>Returns the previous state, or null when there is nothing to undo.</summary>
    public List<SceneObject>? Undo(List<SceneObject> current)
    {
        if (_undo.Count == 0) return null;
        _redo.Push(current);
        return _undo.Pop();
    }

    /// <summary>Returns the next state, or null when there is nothing to redo.</summary>
    public List<SceneObject>? Redo(List<SceneObject> current)
    {
        if (_redo.Count == 0) return null;
        _undo.Push(current);
        return _redo.Pop();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
