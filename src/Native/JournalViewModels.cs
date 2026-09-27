using System;
using System.Collections.Generic;

namespace UsableComputer.Native;

internal sealed class JournalEntryViewModel
{
    internal JournalEntryViewModel(string title, string state)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Untitled entry" : title;
        State = string.IsNullOrWhiteSpace(state) ? "Unknown" : state;
    }

    internal string Title { get; }

    internal string State { get; }
}

internal sealed class JournalQuestViewModel
{
    internal JournalQuestViewModel(
        string id,
        string title,
        string subtitle,
        string description,
        string state,
        bool isTracked,
        IReadOnlyList<JournalEntryViewModel> entries)
    {
        Id = id;
        Title = string.IsNullOrWhiteSpace(title) ? "Untitled quest" : title;
        Subtitle = subtitle ?? string.Empty;
        Description = description ?? string.Empty;
        State = string.IsNullOrWhiteSpace(state) ? "Unknown" : state;
        IsTracked = isTracked;
        Entries = entries ?? Array.Empty<JournalEntryViewModel>();
    }

    internal string Id { get; }

    internal string Title { get; }

    internal string Subtitle { get; }

    internal string Description { get; }

    internal string State { get; }

    internal bool IsTracked { get; }

    internal IReadOnlyList<JournalEntryViewModel> Entries { get; }
}
