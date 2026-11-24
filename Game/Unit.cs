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
        public float LaneX;
        public float WalkPhase;
        public float AttackAnim;
        public Vector2 Facing = new Vector2(0, 1);
        public bool IsBuilding => Def.Kind == CardKind.Building;
        public bool Alive => Hp > 0 && (!IsBuilding || LifeLeft > 0f);
        public float Radius => GetRadius();

        public Unit(CardDef def, Vector2 pos, bool isEnemy, float laneX)
        {
            Def = def;
            Pos = pos;
            Hp = MaxHp = def.Hp;
            IsEnemy = isEnemy;
            AttackTimer = 0f;
            HitFlash = 0f;
            LifeLeft = def.Lifetime;
            LaneX = laneX;
            WalkPhase = 0f;
            AttackAnim = 0f;
            Facing = isEnemy ? new Vector2(0, 1) : new Vector2(0, -1);
        }

        private float GetRadius()
        {
            if (IsBuilding) return 14f;
            if (Def.Id == "pulso") return 16f;
            return Def.Kind == CardKind.Melee ? 11f : 9f;
        }
    }
}
