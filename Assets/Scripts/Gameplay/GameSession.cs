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

        public int MaxHp => State.maxHp + AbilityMaxHpBonus() + RealmMaxHpBonus();

        // ---------------- 境界 ----------------

        public RealmDef CurrentRealm
        {
            get
            {
                var list = Content.Realms;
                if (list == null || list.Count == 0) return null;
                return list[Mathf.Clamp(State.realmIndex, 0, list.Count - 1)];
            }
        }

        public RealmDef NextRealm
        {
            get
            {
                var list = Content.Realms;
                if (list == null) return null;
                int i = State.realmIndex + 1;
                return (i >= 0 && i < list.Count) ? list[i] : null;
            }
        }

        public string RealmName => CurrentRealm != null ? CurrentRealm.name : "觉醒者";

        int RealmMaxHpBonus()
        {
            var r = CurrentRealm;
            return r != null ? r.maxHpBonus : 0;
        }

        int RealmDamageBonus()
        {
            var r = CurrentRealm;
            return r != null ? r.damageBonus : 0;
        }


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
            State.totalExp += amount;
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

        // ---------------- 资源 ----------------

        public string CollectDrop(bool resonance, int amount)
        {
            if (amount <= 0) return null;

            if (resonance)
            {
                State.resonanceCount += amount;
                return "<color=#8ad7ff>获得异能残响 ×" + amount + "</color>";
            }

            State.breakthroughMaterials += amount;
            return "<color=#ffd479>获得突破素材 ×" + amount + "</color>";
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

        // ---------------- 任务 ----------------

        public QuestDef MainQuest => Content.MainQuest;
        public bool QuestAccepted => State.questState == "进行中";
        public bool QuestCompleted => State.questState == "已完成";

        public string QuestProgressText()
        {
            var q = MainQuest;
            if (q == null) return "";

            if (State.questState == "已完成") return "<color=#7ee787>「" + q.name + "」已完成</color>";
            if (State.questState == "进行中")
                return "「" + q.name + "」　清剿异兽 " + State.questKills + " / " + q.targetCount;
            return "「" + q.name + "」　未接取";
        }

        public int GetKillCount(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId) || State.killCounts == null) return 0;
            foreach (var k in State.killCounts)
                if (k != null && k.enemyId == enemyId) return k.count;
            return 0;
        }

        void AddKillCount(string enemyId)
        {
            if (State.killCounts == null) State.killCounts = new List<KillCount>();
            foreach (var k in State.killCounts)
                if (k != null && k.enemyId == enemyId) { k.count++; return; }
            State.killCounts.Add(new KillCount { enemyId = enemyId, count = 1 });
        }

        public string AcceptQuest()
        {
            var q = MainQuest;
            if (q == null) return null;

            if (State.questState == "进行中") return "这单委托已经在做了。";
            if (State.questState == "已完成") return "这单委托已经完成了。";

            State.questState = "进行中";

            // 用历史击杀数回填进度：避免"先打完再接任务"导致委托永远做不完
            State.questKills = Mathf.Min(GetKillCount(q.targetEnemy), q.targetCount);

            var msg = "<color=#8ad7ff>接取委托「" + q.name + "」：</color>" + q.desc;

            if (State.questKills >= q.targetCount)
            {
                State.questState = "已完成";
                msg += "\n\n<color=#7ee787>你之前已经清剿过了，这单当场结了。</color>";
                var expMsg = GainExp(q.rewardExp);
                if (!string.IsNullOrEmpty(expMsg)) msg += "　" + expMsg;
            }
            else if (State.questKills > 0)
            {
                msg += "\n\n（你的击杀记录已计入：" + State.questKills + " / " + q.targetCount + "）";
            }

            return msg;
        }

        // 击杀上报；没进展就返回 null
        public string ReportKill(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return null;

            // 不管任务接没接，都先记一份击杀数
            AddKillCount(enemyId);

            var q = MainQuest;
            if (q == null || State.questState != "进行中") return null;
            if (enemyId != q.targetEnemy) return null;

            State.questKills = Mathf.Min(GetKillCount(q.targetEnemy), q.targetCount);
            if (State.questKills < q.targetCount) return null;

            State.questState = "已完成";

            var msg = "<color=#7ee787>委托「" + q.name + "」完成</color>";
            var expMsg = GainExp(q.rewardExp);
            if (!string.IsNullOrEmpty(expMsg)) msg += "　" + expMsg;
            return msg;
        }

        // ---------------- 炼化 ----------------

        public bool CanRefine => State.resonanceCount > 0;

        public List<AbilityDef> RefinableAbilities()
        {
            var list = new List<AbilityDef>();
            foreach (var a in Content.Abilities)
            {
                if (a == null || !a.refinable) continue;
                if (State.abilities.Contains(a.id)) continue;
                list.Add(a);
            }
            return list;
        }

        public string Refine(string abilityId)
        {
            if (State.resonanceCount <= 0) return null;

            var def = Content.GetAbility(abilityId);
            if (def == null || !def.refinable) return null;
            if (State.abilities.Contains(abilityId)) return null;

            State.resonanceCount--;
            State.refineCount++;
            State.abilities.Add(abilityId);
            ClampHp();

            var msg = "<color=#7ee787>炼化成功，获得异能「" + def.name + "」</color>";
            var expMsg = GainExp(20);
            if (!string.IsNullOrEmpty(expMsg)) msg += "\n" + expMsg;
            return msg;
        }

        // ---------------- 据点 ----------------

        public string HealFull()
        {
            State.hp = MaxHp;
            return "<color=#7ee787>躺了一会儿，气血全满（" + MaxHp + "）。</color>";
        }

        public string StorageText()
        {
            return "异能残响　×" + State.resonanceCount + "\n" +
                   "突破素材　×" + State.breakthroughMaterials + "\n" +
                   "炼化次数　" + State.refineCount + "\n" +
                   "累计经验　" + State.totalExp;
        }

        // ---------------- 战斗加成 ----------------

        public int TotalDamageBonus()
        {
            int sum = RealmDamageBonus();
            foreach (var id in State.abilities)
            {
                var a = Content.GetAbility(id);
                if (a != null) sum += a.damageBonus;
            }
            return sum;
        }

        public float TotalDamageReduction()
        {
            float sum = 0f;
            foreach (var id in State.abilities)
            {
                var a = Content.GetAbility(id);
                if (a != null) sum += a.damageReduction;
            }
            return Mathf.Clamp(sum, 0f, 0.8f);
        }

        public float TotalCooldownReduction()
        {
            float sum = 0f;
            foreach (var id in State.abilities)
            {
                var a = Content.GetAbility(id);
                if (a != null) sum += a.cooldownReduction;
            }
            return Mathf.Clamp(sum, 0f, 0.6f);
        }

        // ---------------- 突破条件 ----------------

        public const int BreakthroughExpNeed = 100;
        public const int BreakthroughMaterialNeed = 2;
        public const int BreakthroughRefineNeed = 1;

        static string Mark(bool ok)
        {
            return ok ? "<color=#7ee787>√</color>" : "<color=#ffa657>×</color>";
        }

        public string BreakthroughPanelText(out bool allOk)
        {
            bool expOk = State.totalExp >= BreakthroughExpNeed;
            bool refineOk = State.refineCount >= BreakthroughRefineNeed;
            bool matOk = State.breakthroughMaterials >= BreakthroughMaterialNeed;
            allOk = expOk && refineOk && matOk;

            return Mark(expOk) + " 累计经验　" + State.totalExp + " / " + BreakthroughExpNeed + "\n" +
                   Mark(refineOk) + " 炼化残响　" + State.refineCount + " / " + BreakthroughRefineNeed + "\n" +
                   Mark(matOk) + " 突破素材　" + State.breakthroughMaterials + " / " + BreakthroughMaterialNeed;
        }

        public bool CanBreakthrough()
        {
            bool allOk;
            BreakthroughPanelText(out allOk);
            return allOk && NextRealm != null;
        }

        // 真正的突破。返回给玩家看的文本；条件不足或已到顶就返回 null。
        public string DoBreakthrough()
        {
            if (!CanBreakthrough()) return null;

            var next = NextRealm;
            State.realmIndex++;

            // 上限涨多少，当前血量也补多少，别让突破看起来像被削了
            State.hp += next.maxHpBonus;
            ClampHp();

            return "<color=#ffd479><b>突破成功</b></color>\n\n" +
                   "你踏入了「<b>" + next.name + "</b>」。\n" +
                   next.desc + "\n\n" +
                   "<color=#7ee787>生命上限 +" + next.maxHpBonus +
                   "　普攻伤害 +" + next.damageBonus + "</color>";
        }

        // ---------------- 显示 ----------------

        public string StatusText()
        {
            return "<b>" + State.playerName + "</b>　　" +
                   "<color=#ffd479>" + RealmName + "</color>　Lv." + State.level +
                   "　　<color=#8ab4ff>生命 " + State.hp + " / " + MaxHp + "</color>" +
                   "　　经验 " + State.exp + " / " + ExpToNext + "\n" +
                   "异能　" + AbilityNames();
        }
    }
}
