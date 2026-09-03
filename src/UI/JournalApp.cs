using System;
using System.Collections.Generic;
using System.Text;
using UsableComputer.API;
using UsableComputer.Logic;
using UsableComputer.Native;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class JournalApp : IDesktopAppSession
{
    private UiListenerRegistry _rowListeners = new();
    private readonly List<JournalQuestViewModel> _quests = new();
    private RectTransform? _questList;
    private ScrollRect? _questScroll;
    private S1Text? _status;
    private S1Text? _detailTitle;
    private S1Text? _detailSubtitle;
    private S1Text? _detailDescription;
    private S1Text? _detailEntries;
    private S1Text? _trackLabel;
    private Button? _trackButton;
    private string? _selectedQuestId;
    private float _nextRefresh;
    private bool _disposed;

    internal JournalApp(DesktopAppContext context)
    {
        Build(context.Container, context.Listeners);
    }

    public void OnOpened()
    {
        RefreshFromNative(forceRows: true);
    }

    public void OnClosed()
    {
    }

    public void OnTick()
    {
        if (_disposed || Time.unscaledTime < _nextRefresh)
            return;

        _nextRefresh = Time.unscaledTime + 1f;
        RefreshFromNative(forceRows: false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _rowListeners.Dispose();
    }

    private void Build(Transform parent, UiListenerRegistry listeners)
    {
        S1Text heading = UiFactory.CreateText(
            parent,
            "Heading",
            "Journal",
            21f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetTopRect(heading.rectTransform, 32f, -10f);

        _status = UiFactory.CreateText(
            parent,
            "Status",
            "Reading active quests...",
            13f,
            UiFactory.TextMuted,
            GetHeadingAlignment());
        SetTopRect(_status.rectTransform, 22f, -42f);

        GameObject listPanel = UiFactory.CreatePanel(parent, "QuestList", UiFactory.SurfaceRaised);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0.42f, 1f);
        listRect.offsetMin = new Vector2(8f, 8f);
        listRect.offsetMax = new Vector2(-6f, -74f);

        S1Text listHeading = UiFactory.CreateText(
            listPanel.transform,
            "ListHeading",
            "Active quests",
            15f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetTopRect(listHeading.rectTransform, 26f, -10f);

        _questScroll = listPanel.AddComponent<ScrollRect>();
        _questScroll.horizontal = false;
        _questScroll.vertical = true;
        _questScroll.inertia = true;
        _questScroll.scrollSensitivity = 28f;
        _questScroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = new GameObject("QuestViewport");
        viewportObject.transform.SetParent(listPanel.transform, false);
        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-8f, -42f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject listContent = new GameObject("QuestRows");
        listContent.transform.SetParent(viewportObject.transform, false);
        _questList = listContent.AddComponent<RectTransform>();
        _questList.anchorMin = new Vector2(0f, 1f);
        _questList.anchorMax = new Vector2(1f, 1f);
        _questList.pivot = new Vector2(0.5f, 1f);
        _questList.sizeDelta = new Vector2(0f, 1f);
        _questList.anchoredPosition = Vector2.zero;
        _questScroll.viewport = viewportRect;
        _questScroll.content = _questList;

        GameObject detailPanel = UiFactory.CreatePanel(parent, "QuestDetails", UiFactory.SurfaceRaised);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.42f, 0f);
        detailRect.anchorMax = Vector2.one;
        detailRect.offsetMin = new Vector2(6f, 8f);
        detailRect.offsetMax = new Vector2(-8f, -74f);

        _detailTitle = UiFactory.CreateText(
            detailPanel.transform,
            "DetailTitle",
            "Select a quest",
            19f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetTopRect(_detailTitle.rectTransform, 30f, -10f);

        _detailSubtitle = UiFactory.CreateText(
            detailPanel.transform,
            "DetailSubtitle",
            "",
            14f,
            UiFactory.TextMuted,
            GetHeadingAlignment());
        SetTopRect(_detailSubtitle.rectTransform, 24f, -44f);

        _detailDescription = UiFactory.CreateText(
            detailPanel.transform,
            "DetailDescription",
            "",
            15f,
            UiFactory.TextPrimary,
            GetBodyAlignment());
        _detailDescription.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        _detailDescription.rectTransform.anchorMax = new Vector2(1f, 1f);
        _detailDescription.rectTransform.offsetMin = new Vector2(14f, -24f);
        _detailDescription.rectTransform.offsetMax = new Vector2(-14f, -76f);

        _detailEntries = UiFactory.CreateText(
            detailPanel.transform,
            "DetailEntries",
            "",
            14f,
            UiFactory.TextMuted,
            GetBodyAlignment());
        _detailEntries.rectTransform.anchorMin = Vector2.zero;
        _detailEntries.rectTransform.anchorMax = Vector2.one;
        _detailEntries.rectTransform.offsetMin = new Vector2(14f, 56f);
        _detailEntries.rectTransform.offsetMax = new Vector2(-14f, -190f);

        _trackButton = UiFactory.CreateButton(
            detailPanel.transform,
            "Track",
            "Track",
            UiFactory.Accent,
            out S1Text trackLabel);
        _trackLabel = trackLabel;
        RectTransform trackRect = _trackButton.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(112f, 32f);
        trackRect.anchoredPosition = new Vector2(-12f, 24f);
        listeners.Add(ToggleTracking, _trackButton.onClick);

        UiFactory.SetLayerRecursively(listPanel, Constants.UiLayer);
        UiFactory.SetLayerRecursively(detailPanel, Constants.UiLayer);
    }

    private void RefreshFromNative(bool forceRows)
    {
        List<JournalQuestViewModel> fresh = JournalNativeAdapter.ReadActiveQuests();
        bool rowsChanged = forceRows || !SameQuestIds(_quests, fresh);
        _quests.Clear();
        _quests.AddRange(fresh);

        if (_selectedQuestId == null || FindSelectedQuest() == null)
            _selectedQuestId = _quests.Count > 0 ? _quests[0].Id : null;

        if (rowsChanged)
            RebuildQuestRows();

        UpdateDetail();
    }

    private void RebuildQuestRows()
    {
        if (_questList == null)
            return;

        _rowListeners.Dispose();
        _rowListeners = new UiListenerRegistry();
        _questList.sizeDelta = new Vector2(0f, Mathf.Max(1f, (_quests.Count * 40f) + 8f));
        _questList.anchoredPosition = Vector2.zero;
        if (_questScroll != null)
        {
            _questScroll.StopMovement();
            _questScroll.verticalNormalizedPosition = 1f;
        }

        for (int index = _questList.childCount - 1; index >= 0; index--)
        {
            Transform child = _questList.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }

        for (int index = 0; index < _quests.Count; index++)
        {
            JournalQuestViewModel quest = _quests[index];
            Button button = UiFactory.CreateButton(
                _questList,
                $"Quest_{index}",
                quest.Title,
                UiFactory.SurfaceInset,
                out _);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 34f);
            rect.anchoredPosition = new Vector2(0f, -index * 40f);
            string questId = quest.Id;
            _rowListeners.Add(() => SelectQuest(questId), button.onClick);
            UiFactory.SetLayerRecursively(button.gameObject, Constants.UiLayer);
        }

        if (_status != null)
        {
            _status.text = _quests.Count == 0
                ? "No active quests are available."
                : $"{_quests.Count} active quest{(_quests.Count == 1 ? string.Empty : "s")}";
        }
    }

    private void SelectQuest(string questId)
    {
        _selectedQuestId = questId;
        UpdateDetail();
    }

    private void ToggleTracking()
    {
        JournalQuestViewModel? selected = FindSelectedQuest();
        if (selected == null)
            return;

        bool tracked = !selected.IsTracked;
        JournalNativeAdapter.TrySetTracked(selected.Id, tracked, out string message);
        if (_status != null)
            _status.text = message;

        RefreshFromNative(forceRows: false);
    }

    private void UpdateDetail()
    {
        JournalQuestViewModel? selected = FindSelectedQuest();
        if (_detailTitle == null ||
            _detailSubtitle == null ||
            _detailDescription == null ||
            _detailEntries == null ||
            _trackButton == null ||
            _trackLabel == null)
        {
            return;
        }

        if (selected == null)
        {
            _detailTitle.text = "Select a quest";
            _detailSubtitle.text = string.Empty;
            _detailDescription.text = "The native Journal has no active quest to display.";
            _detailEntries.text = string.Empty;
            _trackButton.gameObject.SetActive(false);
            return;
        }

        _detailTitle.text = selected.Title;
        _detailSubtitle.text = string.IsNullOrWhiteSpace(selected.Subtitle)
            ? $"State: {selected.State}"
            : $"{selected.Subtitle}\nState: {selected.State}";
        _detailDescription.text = string.IsNullOrWhiteSpace(selected.Description)
            ? "No description supplied by the native quest."
            : selected.Description;
        _detailEntries.text = FormatEntries(selected.Entries);
        _trackLabel.text = selected.IsTracked ? "Untrack" : "Track";
        _trackButton.gameObject.SetActive(true);
    }

    private JournalQuestViewModel? FindSelectedQuest()
    {
        if (_selectedQuestId == null)
            return null;

        foreach (JournalQuestViewModel quest in _quests)
        {
            if (string.Equals(quest.Id, _selectedQuestId, StringComparison.OrdinalIgnoreCase))
                return quest;
        }

        return null;
    }

    private static bool SameQuestIds(
        IReadOnlyList<JournalQuestViewModel> left,
        IReadOnlyList<JournalQuestViewModel> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Id, right[index].Id, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string FormatEntries(IReadOnlyList<JournalEntryViewModel> entries)
    {
        if (entries.Count == 0)
            return "No quest entries.";

        var builder = new StringBuilder("Entries\n");
        foreach (JournalEntryViewModel entry in entries)
            builder.Append("[").Append(entry.State).Append("] ").Append(entry.Title).Append('\n');
        return builder.ToString().TrimEnd();
    }

    private static void SetTopRect(RectTransform rectTransform, float height, float y)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(-24f, height);
        rectTransform.anchoredPosition = new Vector2(0f, y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetHeadingAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetBodyAlignment() => Il2CppTMPro.TextAlignmentOptions.TopLeft;
#else
    private static TMPro.TextAlignmentOptions GetHeadingAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions GetBodyAlignment() => TMPro.TextAlignmentOptions.TopLeft;
#endif
}
