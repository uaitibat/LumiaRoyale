using Microsoft.Xna.Framework;

namespace LumiaRoyale.Game
{
    public sealed class Unit
    {
        public CardDef Def;
        public Vector2 Pos;
        public int Hp;
        public int MaxHp;
        public bool IsEnemy;
        public float AttackTimer;
        public float HitFlash;
        public bool Alive => Hp > 0;
        public float Radius => Def.Kind == UnitKind.Melee ? 11f : 9f;

        public Unit(CardDef def, Vector2 pos, bool isEnemy)
        {
            Def = def;
            Pos = pos;
            Hp = MaxHp = def.Hp;
            IsEnemy = isEnemy;
            AttackTimer = 0f;
            HitFlash = 0f;
        }
    }
}
