using Microsoft.Xna.Framework;

namespace LumiaRoyale.Game
{
    public enum UnitKind
    {
        Melee,
        Ranged
    }

    public sealed class CardDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Cost { get; set; }
        public int Hp { get; set; }
        public int Damage { get; set; }
        public float Speed { get; set; }
        public float Range { get; set; }
        public float AttackCooldown { get; set; }
        public Color Color { get; set; }
        public UnitKind Kind { get; set; }

        public static CardDef[] StarterDeck()
        {
            return new[]
            {
                new CardDef { Id="sentinela", Name="Sentinela", Cost=3, Hp=240, Damage=28, Speed=28f, Range=20f, AttackCooldown=1.0f, Color=new Color(90, 170, 255), Kind=UnitKind.Melee },
                new CardDef { Id="dardo", Name="Dardo Drone", Cost=2, Hp=120, Damage=22, Speed=38f, Range=90f, AttackCooldown=0.8f, Color=new Color(255, 190, 80), Kind=UnitKind.Ranged },
                new CardDef { Id="pulso", Name="Pulso", Cost=4, Hp=320, Damage=40, Speed=22f, Range=22f, AttackCooldown=1.2f, Color=new Color(170, 120, 255), Kind=UnitKind.Melee },
                new CardDef { Id="faísca", Name="Faísca", Cost=2, Hp=100, Damage=18, Speed=44f, Range=80f, AttackCooldown=0.7f, Color=new Color(120, 255, 180), Kind=UnitKind.Ranged }
            };
        }
    }
}
