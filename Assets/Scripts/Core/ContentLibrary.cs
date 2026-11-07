using System.Collections.Generic;
using XianXia.Data;

namespace XianXia
{
    // 把所有 JSON 配置表读进内存，并提供按 id 查询。
    public class ContentLibrary
    {
        public List<AbilityDef> Abilities = new List<AbilityDef>();
        public List<InteractionDef> Interactions = new List<InteractionDef>();
        public List<EventDef> Events = new List<EventDef>();
        public List<QuestDef> Quests = new List<QuestDef>();
        public List<RealmDef> Realms = new List<RealmDef>();

        readonly Dictionary<string, AbilityDef> _abilityById = new Dictionary<string, AbilityDef>();
        readonly Dictionary<string, InteractionDef> _interactionById = new Dictionary<string, InteractionDef>();
        readonly Dictionary<string, EventDef> _eventById = new Dictionary<string, EventDef>();

        public static ContentLibrary Load()
        {
            var lib = new ContentLibrary();

            var abilityTable = DataLoader.Load<AbilityTable>("Data/abilities");
            if (abilityTable != null && abilityTable.abilities != null) lib.Abilities = abilityTable.abilities;

            var interactionTable = DataLoader.Load<InteractionTable>("Data/interactables");
            if (interactionTable != null && interactionTable.interactables != null) lib.Interactions = interactionTable.interactables;

            var eventTable = DataLoader.Load<EventTable>("Data/events");
            if (eventTable != null && eventTable.events != null) lib.Events = eventTable.events;

            var questTable = DataLoader.Load<QuestTable>("Data/quests");
            if (questTable != null && questTable.quests != null) lib.Quests = questTable.quests;

            var realmTable = DataLoader.Load<RealmTable>("Data/realms");
            if (realmTable != null && realmTable.realms != null) lib.Realms = realmTable.realms;

            foreach (var a in lib.Abilities)
                if (a != null && !string.IsNullOrEmpty(a.id)) lib._abilityById[a.id] = a;

            foreach (var i in lib.Interactions)
                if (i != null && !string.IsNullOrEmpty(i.id)) lib._interactionById[i.id] = i;

            foreach (var e in lib.Events)
                if (e != null && !string.IsNullOrEmpty(e.id)) lib._eventById[e.id] = e;

            return lib;
        }

        public AbilityDef GetAbility(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            AbilityDef a;
            return _abilityById.TryGetValue(id, out a) ? a : null;
        }

        public InteractionDef GetInteraction(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            InteractionDef i;
            return _interactionById.TryGetValue(id, out i) ? i : null;
        }

        public EventDef GetEvent(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EventDef e;
            return _eventById.TryGetValue(id, out e) ? e : null;
        }

        // 本 Demo 只有一条主线任务
        public QuestDef MainQuest
        {
            get { return Quests != null && Quests.Count > 0 ? Quests[0] : null; }
        }
    }
}
