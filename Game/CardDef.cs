using Microsoft.Xna.Framework;

namespace LumiaRoyale.Game
{
    public enum CardKind
    {
        Melee,
        Ranged,
        Spell,
        Building
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
        public float AreaRadius { get; set; }
        public float Lifetime { get; set; }
        public int SpawnCount { get; set; } = 1;
        public Color Color { get; set; }
        public CardKind Kind { get; set; }

        public static CardDef[] StarterDeck()
        {
            return new[]
            {
                new CardDef { Id="sentinela", Name="Sentinela", Cost=3, Hp=300, Damage=36, Speed=32f, Range=24f, AttackCooldown=0.95f, Color=new Color(90, 170, 255), Kind=CardKind.Melee },
                new CardDef { Id="dardo", Name="Vigia",     Cost=2, Hp=140, Damage=28, Speed=38f, Range=110f, AttackCooldown=0.9f, Color=new Color(255, 190, 80), Kind=CardKind.Ranged },
                new CardDef { Id="pulso", Name="Colosso",   Cost=5, Hp=780, Damage=64, Speed=18f, Range=28f, AttackCooldown=1.3f, Color=new Color(170, 120, 255), Kind=CardKind.Melee },
                new CardDef { Id="faisca", Name="Faisca",   Cost=2, Hp=120, Damage=20, Speed=50f, Range=92f, AttackCooldown=0.65f, Color=new Color(120, 255, 180), Kind=CardKind.Ranged },
                new CardDef { Id="nova", Name="Nova",       Cost=4, Hp=0,   Damage=150, Speed=0f,  Range=0f, AttackCooldown=0f, AreaRadius=60f, Color=new Color(255, 130, 70), Kind=CardKind.Spell },
                new CardDef { Id="torreta", Name="Torreta", Cost=3, Hp=380, Damage=30, Speed=0f, Range=122f, AttackCooldown=0.8f, Lifetime=26f, Color=new Color(180, 200, 220), Kind=CardKind.Building },
                new CardDef { Id="enxame", Name="Enxame",   Cost=3, Hp=75,  Damage=18, Speed=46f, Range=18f, AttackCooldown=0.7f, SpawnCount=4, Color=new Color(140, 220, 120), Kind=CardKind.Melee }
            };
        }
    }
}
