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
        public float LifeLeft;
        public bool IsBuilding => Def.Kind == CardKind.Building;
        public bool Alive => Hp > 0 && (!IsBuilding || LifeLeft > 0f);
        public float Radius => IsBuilding ? 14f : (Def.Kind == CardKind.Melee ? 11f : 9f);

        public Unit(CardDef def, Vector2 pos, bool isEnemy)
        {
            Def = def;
            Pos = pos;
            Hp = MaxHp = def.Hp;
            IsEnemy = isEnemy;
            AttackTimer = 0f;
            HitFlash = 0f;
            LifeLeft = def.Lifetime;
        }
    }
}
