using System;
using System.Collections.Generic;

namespace XianXia.Data
{
    [Serializable]
    public class EnemyTable
    {
        public List<EnemyDef> enemies = new List<EnemyDef>();
        public List<EnemySpawnDef> spawns = new List<EnemySpawnDef>();

        // true = 每次进入场景，本场景的敌人全部复活（默认）
        // false = 打死的永久不再刷
        public bool respawnOnEnter = true;
    }

    [Serializable]
    public class EnemyDef
    {
        public string id;
        public string name;
        public string sprite;
        public int hp = 30;
        public int damage = 6;
        public float speed = 2.4f;
        public float aggroRange = 9f;      // 多远开始追（世界单位）
        public float attackRange = 0.95f;  // 多近开始打
        public float attackInterval = 1.2f;
        public int exp = 10;
        public int resonance = 1;          // 掉几个残响
        public int materials = 0;          // 掉几个突破素材

        // 击退度：0 = 一打就飞，1 = 纹丝不动。越厉害的怪给得越高。
        public float knockbackResist;
    }

    [Serializable]
    public class EnemySpawnDef
    {
        public string id;
        public string scene;
        public string enemy;
        public int imageX;
        public int imageY;
    }
}
