using System;
using System.Collections.Generic;
using System.Globalization;

namespace UsableComputer.Logic;

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

internal sealed class ProductViewModel
{
    internal ProductViewModel(
        string id,
        string name,
        string productType,
        float marketValue,
        bool isListed,
        bool isFavourited)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        ProductType = string.IsNullOrWhiteSpace(productType) ? "Product" : productType;
        MarketValue = marketValue;
        IsListed = isListed;
        IsFavourited = isFavourited;
    }

    internal string Id { get; }

    internal string Name { get; }

    internal string ProductType { get; }

    internal float MarketValue { get; }

    internal bool IsListed { get; }

    internal bool IsFavourited { get; }

    internal string ValueLabel => MarketValue.ToString("0.##", CultureInfo.InvariantCulture);
}
