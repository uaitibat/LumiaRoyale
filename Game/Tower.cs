using Microsoft.Xna.Framework;

namespace LumiaRoyale.Game
{
    public sealed class Tower
    {
        public Vector2 Pos;
        public int Hp;
        public int MaxHp;
        public bool IsMain;
        public bool IsEnemy;
        public float Range;
        public int Damage;
        public float Cooldown;
        public float CooldownLeft;
        public float HitFlash;
        public bool Awake;
        public bool Alive => Hp > 0;

        public Tower(Vector2 pos, int hp, bool isMain, bool isEnemy)
        {
            Pos = pos;
            Hp = MaxHp = hp;
            IsMain = isMain;
            IsEnemy = isEnemy;
            Range = isMain ? 145f : 125f;
            Damage = isMain ? 26 : 20;
            Cooldown = 1.0f;
            HitFlash = 0f;
            Awake = !isMain; // king sleeps until damaged
        }
    }
}
