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
        public Color Color { get; set; }
        public CardKind Kind { get; set; }

        public static CardDef[] StarterDeck()
        {
            return new[]
            {
                new CardDef { Id="sentinela", Name="Sentinela", Cost=3, Hp=280, Damage=34, Speed=32f, Range=24f, AttackCooldown=1.0f, Color=new Color(90, 170, 255), Kind=CardKind.Melee },
                new CardDef { Id="dardo", Name="Vigia",     Cost=2, Hp=130, Damage=26, Speed=38f, Range=105f, AttackCooldown=0.9f, Color=new Color(255, 190, 80), Kind=CardKind.Ranged },
                new CardDef { Id="pulso", Name="Colosso",   Cost=5, Hp=720, Damage=58, Speed=18f, Range=26f, AttackCooldown=1.35f, Color=new Color(170, 120, 255), Kind=CardKind.Melee },
                new CardDef { Id="faisca", Name="Faisca",   Cost=2, Hp=110, Damage=20, Speed=48f, Range=90f, AttackCooldown=0.65f, Color=new Color(120, 255, 180), Kind=CardKind.Ranged },
                new CardDef { Id="nova", Name="Nova",       Cost=4, Hp=0,   Damage=140, Speed=0f,  Range=0f, AttackCooldown=0f, AreaRadius=58f, Color=new Color(255, 130, 70), Kind=CardKind.Spell },
                new CardDef { Id="torreta", Name="Torreta", Cost=3, Hp=360, Damage=28, Speed=0f, Range=120f, AttackCooldown=0.85f, Lifetime=24f, Color=new Color(180, 200, 220), Kind=CardKind.Building }
            };
        }
    }
}
