using Microsoft.Xna.Framework;

namespace LumiaRoyale.Game
{
    public sealed class Unit
    {
        public CardDef Def;
        public Vector2 Pos;
        public int Hp;
        public bool IsEnemy;
        public float AttackTimer;
        public bool Alive => Hp > 0;
        public float Radius => 10f;

        public Unit(CardDef def, Vector2 pos, bool isEnemy)
        {
            Def = def;
            Pos = pos;
            Hp = def.Hp;
            IsEnemy = isEnemy;
            AttackTimer = 0f;
        }
    }
}
