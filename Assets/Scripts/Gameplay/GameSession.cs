using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Data;

namespace XianXia
{
    public struct BreakthroughResult
    {
        public bool ok;
        public string text;
    }

    public class EventResult
    {
        public string text;
        public string nextEventId;
    }

    // 全部玩法逻辑都在这里。它不依赖 UnityEngine 的 UI，
    // 所以可以在命令行里直接跑测试（见 ProjectSetup.SelfCheck）。
    public class GameSession
    {
        public GameState State;
        public ContentLibrary Content;

        public GameSession(ContentLibrary content, GameState state)
        {
            Content = content;
            State = state ?? new GameState();
            if (State.techniques == null) State.techniques = new List<string>();
            if (State.finishedEvents == null) State.finishedEvents = new List<string>();
            ClampHp();
        }

        public RealmDef CurrentRealm
        {
            get
            {
                int count = Content.Realms.Count;
                if (count == 0) return null;
                return Content.Realms[Mathf.Clamp(State.realmIndex, 0, count - 1)];
            }
        }

        public bool IsMaxRealm => CurrentRealm == null || State.realmIndex >= Content.Realms.Count - 1;

        int BonusSum(Func<TechniqueDef, int> selector)
        {
            int sum = 0;
            foreach (var id in State.techniques)
            {
                var t = Content.GetTechnique(id);
                if (t != null) sum += selector(t);
            }
            return sum;
        }

        public int MaxHp => (CurrentRealm != null ? CurrentRealm.maxHp : 100) + BonusSum(t => t.maxHpBonus);
        public int Attack => (CurrentRealm != null ? CurrentRealm.baseAttack : 0) + BonusSum(t => t.attackBonus);
        public int CultivateGain => 5 + State.realmIndex * 2 + BonusSum(t => t.cultivateBonus);

        // ---------------- 行动 ----------------

        public string Cultivate()
        {
            State.turn++;
            int gain = CultivateGain;
            State.cultivation += gain;
            return "第 " + State.turn + " 日：你静坐吐纳，修为 +" + gain + "。";
        }

        public BreakthroughResult TryBreakthrough()
        {
            var result = new BreakthroughResult();

            if (CurrentRealm == null)
            {
                result.text = "没有境界配置表，无法突破。";
                return result;
            }

            if (IsMaxRealm)
            {
                result.text = "你已至" + CurrentRealm.name + "，此界再无可破之境。";
                return result;
            }

            int need = CurrentRealm.qiRequired;
            if (State.cultivation < need)
            {
                result.text = "修为不足：需要 " + need + "，当前 " + State.cultivation + "。";
                return result;
            }

            State.cultivation -= need;
            State.realmIndex++;
            ClampHp();
            State.hp = MaxHp;

            result.ok = true;
            result.text = "天地灵气灌体……你踏入「" + CurrentRealm.name + "」！气血已完全恢复（" + MaxHp + "）。";
            return result;
        }

        public bool LearnTechnique(string id)
        {
            var def = Content.GetTechnique(id);
            if (def == null)
            {
                Debug.LogWarning("[GameSession] 功法不存在: " + id);
                return false;
            }
            if (State.techniques.Contains(id)) return false;

            State.techniques.Add(id);
            ClampHp();
            return true;
        }

        // ---------------- 事件 ----------------

        public EventDef PickEvent()
        {
            var pool = new List<EventDef>();
            int totalWeight = 0;

            foreach (var e in Content.Events)
            {
                if (e == null || !IsEligible(e)) continue;
                int w = e.weight > 0 ? e.weight : 1;
                pool.Add(e);
                totalWeight += w;
            }

            if (pool.Count == 0) return null;

            int roll = UnityEngine.Random.Range(0, totalWeight);
            foreach (var e in pool)
            {
                roll -= (e.weight > 0 ? e.weight : 1);
                if (roll < 0) return e;
            }
            return pool[pool.Count - 1];
        }

        public bool IsEligible(EventDef e)
        {
            if (e == null) return false;
            if (e.onceOnly && State.finishedEvents.Contains(e.id)) return false;

            var r = e.require;
            if (r == null) return true;

            if (State.realmIndex < r.minRealm) return false;
            // maxRealm 填 0 或未填 → 视为无上限（JsonUtility 不保证字段初始值被执行）
            if (r.maxRealm > 0 && State.realmIndex > r.maxRealm) return false;
            if (State.spiritStones < r.minSpiritStones) return false;
            if (!string.IsNullOrEmpty(r.hasFlag) && !State.HasFlag(r.hasFlag)) return false;
            if (!string.IsNullOrEmpty(r.notFlag) && State.HasFlag(r.notFlag)) return false;
            return true;
        }

        public EventResult ResolveOption(EventDef ev, EventOption opt)
        {
            var lines = new List<string>();
            var result = new EventResult();

            if (ev.onceOnly && !State.finishedEvents.Contains(ev.id))
                State.finishedEvents.Add(ev.id);

            if (!string.IsNullOrEmpty(opt.resultText)) lines.Add(opt.resultText);

            if (opt.effects != null)
            {
                foreach (var effect in opt.effects)
                {
                    string extra = ApplyEffect(effect);
                    if (!string.IsNullOrEmpty(extra)) lines.Add(extra);
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
                case "stat":
                    if (effect.key == "hp") { State.hp += effect.value; return null; }
                    if (effect.key == "cultivation") { State.cultivation += effect.value; return "（修为 " + Signed(effect.value) + "）"; }
                    if (effect.key == "spiritStones") { State.spiritStones += effect.value; return "（灵石 " + Signed(effect.value) + "）"; }
                    Debug.LogWarning("[GameSession] 未知 stat 键: " + effect.key);
                    return null;

                case "flag":
                    State.SetFlag(effect.key, effect.value == 0 ? 1 : effect.value);
                    return null;

                case "learn":
                    var tech = Content.GetTechnique(effect.key);
                    if (tech == null) { Debug.LogWarning("[GameSession] 未知功法: " + effect.key); return null; }
                    return LearnTechnique(effect.key) ? "（习得功法「" + tech.name + "」）" : "（你已学过「" + tech.name + "」）";

                case "goto":
                    return null;
            }

            Debug.LogWarning("[GameSession] 未知效果类型: " + effect.type);
            return null;
        }

        static string Signed(int v) => v >= 0 ? "+" + v : v.ToString();

        public void ClampHp()
        {
            int max = MaxHp;
            if (State.hp > max) State.hp = max;
            if (State.hp < 0) State.hp = 0;
        }

        public bool IsDead => State.hp <= 0;

        // ---------------- 显示 ----------------

        public string StatusText()
        {
            var realm = CurrentRealm;
            string realmName = realm != null ? realm.name : "？";
            string need = IsMaxRealm || realm == null ? "—" : realm.qiRequired.ToString();

            return "<b>" + State.playerName + "</b>　　<color=#8ab4ff>" + realmName + "</color>　　第 " + State.turn + " 日\n" +
                   "修为 " + State.cultivation + " / " + need +
                   "　　气血 " + State.hp + " / " + MaxHp +
                   "　　灵石 " + State.spiritStones +
                   "　　攻 " + Attack;
        }
    }
}
