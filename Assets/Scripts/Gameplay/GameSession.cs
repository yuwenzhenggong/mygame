using System.Collections.Generic;
using UnityEngine;
using XianXia.Data;

namespace XianXia
{
    public class EventResult
    {
        public string text;
        public string nextEventId;
    }

    // 全部玩法逻辑都在这里。不依赖 UI，所以能在命令行里直接跑自检。
    // 世界观的改动都收在这一个文件里：等级、经验、生命、异能。
    public class GameSession
    {
        public GameState State;
        public ContentLibrary Content;

        public GameSession(ContentLibrary content, GameState state)
        {
            Content = content;
            State = state ?? new GameState();
            if (State.abilities == null) State.abilities = new List<string>();
            if (State.usedInteractions == null) State.usedInteractions = new List<string>();
            ClampHp();
        }

        public int Level => State.level;
        public int ExpToNext => 100 * Mathf.Max(1, State.level);
        public bool IsDead => State.hp <= 0;

        int AbilityMaxHpBonus()
        {
            int sum = 0;
            foreach (var id in State.abilities)
            {
                var def = Content.GetAbility(id);
                if (def != null) sum += def.maxHpBonus;
            }
            return sum;
        }

        public int MaxHp => State.maxHp + AbilityMaxHpBonus();

        public bool HasAbility(string id)
        {
            return !string.IsNullOrEmpty(id) && State.abilities.Contains(id);
        }

        public string AbilityNames()
        {
            if (State.abilities == null || State.abilities.Count == 0) return "无";
            var names = new List<string>();
            foreach (var id in State.abilities)
            {
                var def = Content.GetAbility(id);
                names.Add(def != null ? def.name : id);
            }
            return string.Join("、", names.ToArray());
        }

        // ---------------- 数值 ----------------

        public string GainExp(int amount)
        {
            if (amount == 0) return null;

            State.exp += amount;
            int levelUps = 0;
            while (State.exp >= ExpToNext)
            {
                State.exp -= ExpToNext;
                State.level++;
                State.maxHp += 10;
                levelUps++;
            }

            if (levelUps > 0) State.hp = MaxHp;
            ClampHp();

            var msg = "经验 +" + amount;
            if (levelUps > 0) msg += "　<color=#7ee787>等级提升至 " + State.level + "</color>";
            return msg;
        }

        public string ChangeHp(int amount)
        {
            if (amount == 0) return null;
            State.hp += amount;
            ClampHp();
            return "生命 " + (amount > 0 ? "+" + amount : amount.ToString());
        }

        public string ChangeMaxHp(int amount)
        {
            if (amount == 0) return null;
            State.maxHp += amount;
            ClampHp();
            return "生命上限 " + (amount > 0 ? "+" + amount : amount.ToString());
        }

        public string LearnAbility(string id)
        {
            var def = Content.GetAbility(id);
            if (def == null)
            {
                Debug.LogWarning("[Session] 异能不存在: " + id);
                return null;
            }
            if (State.abilities.Contains(id)) return null;

            State.abilities.Add(id);
            ClampHp();
            return "<color=#7ee787>获得异能「" + def.name + "」</color>";
        }

        public void ClampHp()
        {
            int max = MaxHp;
            if (State.hp > max) State.hp = max;
            if (State.hp < 0) State.hp = 0;
        }

        // ---------------- 互动 ----------------

        public bool IsInteractionAvailable(InteractionDef def, out string reason)
        {
            reason = null;
            if (def == null)
            {
                reason = "无效的互动";
                return false;
            }

            if (def.onceOnly && State.usedInteractions.Contains(def.id))
            {
                reason = "已经互动过了";
                return false;
            }

            if (!string.IsNullOrEmpty(def.requireAbility) && !HasAbility(def.requireAbility))
            {
                var need = Content.GetAbility(def.requireAbility);
                reason = "需要异能「" + (need != null ? need.name : def.requireAbility) + "」";
                return false;
            }

            return true;
        }

        public void MarkInteractionUsed(InteractionDef def)
        {
            if (def == null || !def.onceOnly) return;
            if (!State.usedInteractions.Contains(def.id)) State.usedInteractions.Add(def.id);
        }

        // ---------------- 事件 ----------------

        public EventResult ResolveOption(EventDef ev, EventOption opt)
        {
            var result = new EventResult();
            var lines = new List<string>();

            if (!string.IsNullOrEmpty(opt.resultText)) lines.Add(opt.resultText);

            if (opt.effects != null)
            {
                foreach (var effect in opt.effects)
                {
                    var line = ApplyEffect(effect);
                    if (!string.IsNullOrEmpty(line)) lines.Add(line);
                    if (effect != null && effect.type == "goto") result.nextEventId = effect.key;
                }
            }

            ClampHp();
            result.text = string.Join("\n", lines.ToArray());
            return result;
        }

        string ApplyEffect(Effect effect)
        {
            if (effect == null || string.IsNullOrEmpty(effect.type)) return null;

            switch (effect.type)
            {
                case "exp": return GainExp(effect.value);
                case "hp": return ChangeHp(effect.value);
                case "maxhp": return ChangeMaxHp(effect.value);
                case "ability": return LearnAbility(effect.key);
                case "flag":
                    State.SetFlag(effect.key, effect.value == 0 ? 1 : effect.value);
                    return null;
                case "goto":
                    return null;
            }

            Debug.LogWarning("[Session] 未知效果类型: " + effect.type);
            return null;
        }

        // ---------------- 显示 ----------------

        public string StatusText()
        {
            return "<b>" + State.playerName + "</b>　　Lv." + State.level +
                   "　　<color=#8ab4ff>生命 " + State.hp + " / " + MaxHp + "</color>" +
                   "　　经验 " + State.exp + " / " + ExpToNext + "\n" +
                   "异能　" + AbilityNames();
        }
    }
}
