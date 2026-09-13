namespace NanoUintEditor;

/// <summary>State of the editor's play mode session.</summary>
public enum PlayModeState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>Event args for play mode state changes.</summary>
public sealed class PlayModeStateChangedEventArgs : EventArgs
{
    public PlayModeState OldState { get; init; }
    public PlayModeState NewState { get; init; }
    public int CurrentPageIndex { get; init; }
}

/// <summary>Event args for page changes during playback.</summary>
public sealed class PageChangedEventArgs : EventArgs
{
    public int OldPageIndex { get; init; }
    public int NewPageIndex { get; init; }
    public SlidePage Page { get; init; } = null!;
}

/// <summary>Runs a presentation of a SlideDocument in play mode using an independent engine context.
/// Manages page navigation, element visibility, transitions, and timing.</summary>
public sealed class PlayModeRunner
{
    private readonly SlideDocument _document;
    private PlayModeState _state = PlayModeState.Stopped;
    private int _currentPageIndex;
    private DateTime _pageStartTime;
    private readonly Dictionary<int, Dictionary<string, bool>> _elementVisibility = new();
    private readonly Random _random = new();

    public SlideDocument Document => _document;
    public PlayModeState State => _state;
    public int CurrentPageIndex => _currentPageIndex;
    public SlidePage? CurrentPage => _currentPageIndex >= 0 && _currentPageIndex < _document.Pages.Count
        ? _document.Pages[_currentPageIndex]
        : null;

    public bool CanGoNext => _currentPageIndex < _document.Pages.Count - 1;
    public bool CanGoPrevious => _currentPageIndex > 0;
    public bool CanGoFirst => _currentPageIndex > 0;
    public bool CanGoLast => _currentPageIndex < _document.Pages.Count - 1;
    public int PageCount => _document.Pages.Count;
    public TimeSpan Elapsed => DateTime.UtcNow - _pageStartTime;

    /// <summary>Fired when play mode state changes.</summary>
    public event EventHandler<PlayModeStateChangedEventArgs>? StateChanged;

    /// <summary>Fired when navigating to a different page.</summary>
    public event EventHandler<PageChangedEventArgs>? PageChanged;

    /// <summary>Fired when an element's visibility changes.</summary>
    public event Action<string, int, bool>? ElementVisibilityChanged;

    public PlayModeRunner(SlideDocument document)
    {
        _document = document;
        InitializeVisibility();
    }

    private void InitializeVisibility()
    {
        for (int i = 0; i < _document.Pages.Count; i++)
        {
            _elementVisibility[i] = new Dictionary<string, bool>();
            foreach (var elem in _document.Pages[i].Elements)
                _elementVisibility[i][elem.Id] = true;
        }
    }

    /// <summary>Starts playback from the first page (or current page if already started).</summary>
    public void Play()
    {
        if (_state == PlayModeState.Playing) return;

        if (_state == PlayModeState.Stopped)
            _currentPageIndex = 0;

        var old = _state;
        _state = PlayModeState.Playing;
        _pageStartTime = DateTime.UtcNow;
        OnStateChanged(old, _state);
    }

    /// <summary>Pauses playback (can resume).</summary>
    public void Pause()
    {
        if (_state != PlayModeState.Playing) return;
        var old = _state;
        _state = PlayModeState.Paused;
        OnStateChanged(old, _state);
    }

    /// <summary>Resumes from pause.</summary>
    public void Resume()
    {
        if (_state != PlayModeState.Paused) return;
        var old = _state;
        _state = PlayModeState.Playing;
        OnStateChanged(old, _state);
    }

    /// <summary>Stops playback and resets to page 0.</summary>
    public void Stop()
    {
        if (_state == PlayModeState.Stopped) return;
        var old = _state;
        _state = PlayModeState.Stopped;
        _currentPageIndex = 0;
        ResetAllVisibility();
        OnStateChanged(old, _state);
    }

    /// <summary>Advances to the next page. Returns false if at the end.</summary>
    public bool NextPage()
    {
        if (!CanGoNext) return false;
        NavigateTo(_currentPageIndex + 1);
        return true;
    }

    /// <summary>Goes to the previous page.</summary>
    public bool PreviousPage()
    {
        if (!CanGoPrevious) return false;
        NavigateTo(_currentPageIndex - 1);
        return true;
    }

    /// <summary>Goes to the first page.</summary>
    public void FirstPage()
    {
        if (_currentPageIndex == 0) return;
        NavigateTo(0);
    }

    /// <summary>Goes to the last page.</summary>
    public void LastPage()
    {
        if (_currentPageIndex >= _document.Pages.Count - 1) return;
        NavigateTo(_document.Pages.Count - 1);
    }

    /// <summary>Goes to a specific page by index.</summary>
    public bool GoToPage(int index)
    {
        if (index < 0 || index >= _document.Pages.Count) return false;
        NavigateTo(index);
        return true;
    }

    /// <summary>Goes to the page with the given name.</summary>
    public bool GoToPage(string pageName)
    {
        var index = _document.Pages.FindIndex(p => p.Name == pageName);
        if (index < 0) return false;
        NavigateTo(index);
        return true;
    }

    /// <summary>Toggles visibility of an element on the current page.</summary>
    public void ToggleElementVisibility(string elementId)
    {
        if (!_elementVisibility.ContainsKey(_currentPageIndex)) return;
        var vis = _elementVisibility[_currentPageIndex];
        if (!vis.ContainsKey(elementId)) return;

        vis[elementId] = !vis[elementId];
        ElementVisibilityChanged?.Invoke(elementId, _currentPageIndex, vis[elementId]);
    }

    /// <summary>Shows all elements on the current page.</summary>
    public void ShowAllElements()
    {
        if (!_elementVisibility.ContainsKey(_currentPageIndex)) return;
        var vis = _elementVisibility[_currentPageIndex];
        var page = _document.Pages[_currentPageIndex];
        foreach (var elem in page.Elements)
        {
            if (!vis.GetValueOrDefault(elem.Id, true))
            {
                vis[elem.Id] = true;
                ElementVisibilityChanged?.Invoke(elem.Id, _currentPageIndex, true);
            }
        }
    }

    /// <summary>Hides all elements on the current page.</summary>
    public void HideAllElements()
    {
        if (!_elementVisibility.ContainsKey(_currentPageIndex)) return;
        var vis = _elementVisibility[_currentPageIndex];
        var page = _document.Pages[_currentPageIndex];
        foreach (var elem in page.Elements)
        {
            if (vis.GetValueOrDefault(elem.Id, true))
            {
                vis[elem.Id] = false;
                ElementVisibilityChanged?.Invoke(elem.Id, _currentPageIndex, false);
            }
        }
    }

    /// <summary>Checks if an element is visible on the current page.</summary>
    public bool IsElementVisible(string elementId)
    {
        if (!_elementVisibility.ContainsKey(_currentPageIndex)) return true;
        return _elementVisibility[_currentPageIndex].GetValueOrDefault(elementId, true);
    }

    /// <summary>Returns the elements visible on the current page, sorted by ZIndex.</summary>
    public List<SlideElement> GetVisibleElements()
    {
        if (CurrentPage == null) return new List<SlideElement>();
        var vis = _elementVisibility.GetValueOrDefault(_currentPageIndex, new Dictionary<string, bool>());
        return CurrentPage.Elements
            .Where(e => vis.GetValueOrDefault(e.Id, true) && e.Visible)
            .OrderBy(e => e.ZIndex)
            .ToList();
    }

    /// <summary>Returns the page transition info for the current page.</summary>
    public (string type, float duration) GetPageTransition()
    {
        if (CurrentPage == null) return ("None", 0f);
        return (CurrentPage.TransitionType, CurrentPage.TransitionDuration);
    }

    private void NavigateTo(int index)
    {
        var oldIndex = _currentPageIndex;
        _currentPageIndex = index;
        _pageStartTime = DateTime.UtcNow;
        PageChanged?.Invoke(this, new PageChangedEventArgs
        {
            OldPageIndex = oldIndex,
            NewPageIndex = index,
            Page = _document.Pages[index]
        });
    }

    private void ResetAllVisibility()
    {
        for (int i = 0; i < _document.Pages.Count; i++)
        {
            foreach (var elem in _document.Pages[i].Elements)
                _elementVisibility[i][elem.Id] = true;
        }
    }

    private void OnStateChanged(PlayModeState oldState, PlayModeState newState)
    {
        StateChanged?.Invoke(this, new PlayModeStateChangedEventArgs
        {
            OldState = oldState,
            NewState = newState,
            CurrentPageIndex = _currentPageIndex
        });
    }
}
