using Microsoft.Xna.Framework;

namespace LumiaRoyale.Game
{
    public sealed class Projectile
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public int Damage;
        public bool ByPlayer;
        public Color Color;
        public float Radius;
        public float Life;
        public Unit UnitTarget;
        public Tower TowerTarget;
        public bool Alive = true;
    }
}
