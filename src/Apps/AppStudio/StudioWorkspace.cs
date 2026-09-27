using System;
using System.Collections.Generic;

namespace UsableComputer.Apps.AppStudio;

internal sealed class StudioDraft
{
    internal StudioDraft(string name, string source) { Name = name; Source = SavedSource = source; }
    internal string Name { get; }
    internal string Source { get; set; }
    internal string SavedSource { get; set; }
    internal bool IsModified => Source != SavedSource;
}

internal sealed class StudioWorkspace
{
    private readonly List<StudioDraft> _drafts = new();
    internal StudioDraft Open(string name, string source)
    {
        foreach (StudioDraft draft in _drafts)
            if (draft.Name == name) return draft;
        if (_drafts.Count >= 16) throw new InvalidOperationException("The draft limit has been reached.");
        var created = new StudioDraft(name, source);
        _drafts.Add(created);
        return created;
    }
    internal StudioDraft Next(StudioDraft current, int direction)
    {
        int index = _drafts.IndexOf(current);
        if (index < 0) throw new InvalidOperationException("Draft is not in this workspace.");
        return _drafts[(index + direction % _drafts.Count + _drafts.Count) % _drafts.Count];
    }
}
