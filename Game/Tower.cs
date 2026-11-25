using System;
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
        public int Level;
        public bool Alive => Hp > 0;

        public Tower(Vector2 pos, int hp, bool isMain, bool isEnemy, int level = 1)
        {
            Pos = pos;
            Hp = MaxHp = hp;
            IsMain = isMain;
            IsEnemy = isEnemy;
            Level = Math.Max(1, level);
            Range = isMain ? 145f : 125f;
            Damage = isMain ? 26 : 20;
            Cooldown = 1.0f;
            HitFlash = 0f;
            Awake = !isMain; // king sleeps until damaged
        }
    }

    // Vida das torres por nível. Nível 1 é o ponto de partida (mais fraco);
    // cada nível seguinte dá um passo previsível pra progressão futura.
    public static class TowerBalance
    {
        public const int BaseLevel = 1;

        private static int Step(int level)
        {
            return Math.Max(0, level - BaseLevel);
        }

        public static int SideHp(int level)
        {
            return 520 + Step(level) * 80;
        }

        public static int KingHp(int level)
        {
            return 820 + Step(level) * 120;
        }
    }
}
