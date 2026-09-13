namespace NanoUintEditor;

/// <summary>Snapshot-based scene undo/redo (stores deep-copied object lists, capacity-limited).</summary>
public class UndoManager
{
    private readonly Stack<List<SceneObject>> _undo = new();
    private readonly Stack<List<SceneObject>> _redo = new();
    private readonly int _capacity;

    public UndoManager(int capacity = 60) => _capacity = capacity;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records a snapshot taken before an operation. A new push clears the redo stack.</summary>
    public void Push(List<SceneObject> snapshot)
    {
        _undo.Push(snapshot);
        if (_undo.Count > _capacity)
        {
            // Keep the most recent _capacity snapshots (ToArray puts the stack top first)
            var arr = _undo.ToArray();
            _undo.Clear();
            for (int i = _capacity - 1; i >= 0; i--) _undo.Push(arr[i]);
        }
        _redo.Clear();
    }

    /// <summary>Undo: returns the snapshot to restore, or null when nothing can be undone.</summary>
    public List<SceneObject>? Undo(List<SceneObject> current)
    {
        if (_undo.Count == 0) return null;
        _redo.Push(current);
        return _undo.Pop();
    }

    /// <summary>Redo: returns the snapshot to restore, or null when nothing can be redone.</summary>
    public List<SceneObject>? Redo(List<SceneObject> current)
    {
        if (_redo.Count == 0) return null;
        _undo.Push(current);
        return _redo.Pop();
    }

    public void Clear() { _undo.Clear(); _redo.Clear(); }
}
