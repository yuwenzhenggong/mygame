using System.Collections.Generic;
using XianXia.Data;

namespace XianXia
{
    // 把所有 JSON 配置表读进内存，并提供按 id 查询。
    public class ContentLibrary
    {
        public List<RealmDef> Realms = new List<RealmDef>();
        public List<TechniqueDef> Techniques = new List<TechniqueDef>();
        public List<EventDef> Events = new List<EventDef>();

        readonly Dictionary<string, TechniqueDef> _techById = new Dictionary<string, TechniqueDef>();
        readonly Dictionary<string, EventDef> _eventById = new Dictionary<string, EventDef>();

        public static ContentLibrary Load()
        {
            var lib = new ContentLibrary();

            var realmTable = DataLoader.Load<RealmTable>("Data/realms");
            if (realmTable != null && realmTable.realms != null) lib.Realms = realmTable.realms;

            var techTable = DataLoader.Load<TechniqueTable>("Data/techniques");
            if (techTable != null && techTable.techniques != null) lib.Techniques = techTable.techniques;

            var eventTable = DataLoader.Load<EventTable>("Data/events");
            if (eventTable != null && eventTable.events != null) lib.Events = eventTable.events;

            foreach (var t in lib.Techniques)
                if (t != null && !string.IsNullOrEmpty(t.id)) lib._techById[t.id] = t;

            foreach (var e in lib.Events)
                if (e != null && !string.IsNullOrEmpty(e.id)) lib._eventById[e.id] = e;

            return lib;
        }

        public TechniqueDef GetTechnique(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            TechniqueDef t;
            return _techById.TryGetValue(id, out t) ? t : null;
        }

        public EventDef GetEvent(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EventDef e;
            return _eventById.TryGetValue(id, out e) ? e : null;
        }
    }
}
