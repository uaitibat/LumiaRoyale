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
        public bool Alive => Hp > 0;

        public Tower(Vector2 pos, int hp, bool isMain, bool isEnemy)
        {
            Pos = pos;
            Hp = MaxHp = hp;
            IsMain = isMain;
            IsEnemy = isEnemy;
            Range = isMain ? 120f : 105f;
            Damage = isMain ? 24 : 18;
            Cooldown = 1.0f;
        }
    }
}
