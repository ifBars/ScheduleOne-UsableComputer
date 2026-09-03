using System;
using System.Collections.Generic;
using UsableComputer.Logic;

#if IL2CPPMELON
using S1Quest = Il2CppScheduleOne.Quests.Quest;
using S1QuestEntry = Il2CppScheduleOne.Quests.QuestEntry;
using S1QuestState = Il2CppScheduleOne.Quests.EQuestState;
#elif MONOMELON
using S1Quest = ScheduleOne.Quests.Quest;
using S1QuestEntry = ScheduleOne.Quests.QuestEntry;
using S1QuestState = ScheduleOne.Quests.EQuestState;
#endif

namespace UsableComputer.Native;

/// <summary>
/// Narrow direct port of the native quest data used by the desktop Journal.
/// </summary>
internal static class JournalNativeAdapter
{
    internal static List<JournalQuestViewModel> ReadActiveQuests()
    {
        var result = new List<JournalQuestViewModel>();
        try
        {
            var activeQuests = S1Quest.ActiveQuests;
            if (activeQuests == null)
                return result;

            foreach (S1Quest quest in activeQuests)
            {
                if (quest == null || quest.State != S1QuestState.Active)
                    continue;

                result.Add(CreateViewModel(quest));
            }
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Journal read unavailable: {exception.Message}");
        }

        result.Sort((left, right) =>
        {
            int titleOrder = string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            return titleOrder != 0
                ? titleOrder
                : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    internal static bool TrySetTracked(string questId, bool tracked, out string message)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            message = "Select a quest first.";
            return false;
        }

        try
        {
            var quests = S1Quest.Quests;
            if (quests == null)
            {
                message = "The native quest list is unavailable.";
                return false;
            }

            foreach (S1Quest quest in quests)
            {
                if (quest == null || !string.Equals(quest.GUID.ToString(), questId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (quest.State != S1QuestState.Active)
                {
                    message = "Only active quests can be tracked here.";
                    return false;
                }

                quest.SetIsTracked(tracked);
                message = tracked ? "Quest tracked." : "Quest untracked.";
                return true;
            }
        }
        catch (Exception exception)
        {
            message = $"Native quest tracking failed: {exception.Message}";
            return false;
        }

        message = "The selected quest is no longer available.";
        return false;
    }

    private static JournalQuestViewModel CreateViewModel(S1Quest quest)
    {
        var entries = new List<JournalEntryViewModel>();
        if (quest.Entries != null)
        {
            foreach (S1QuestEntry entry in quest.Entries)
            {
                if (entry != null)
                    entries.Add(new JournalEntryViewModel(entry.Title, entry.State.ToString()));
            }
        }

        return new JournalQuestViewModel(
            quest.GUID.ToString(),
            quest.Title,
            quest.Subtitle,
            quest.Description,
            quest.State.ToString(),
            quest.IsTracked,
            entries);
    }
}
