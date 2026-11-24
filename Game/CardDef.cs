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
                new CardDef { Id="sentinela", Name="Sentinela", Cost=3, Hp=260, Damage=30, Speed=30f, Range=22f, AttackCooldown=1.0f, Color=new Color(90, 170, 255), Kind=CardKind.Melee },
                new CardDef { Id="dardo", Name="Dardo", Cost=2, Hp=120, Damage=24, Speed=40f, Range=95f, AttackCooldown=0.8f, Color=new Color(255, 190, 80), Kind=CardKind.Ranged },
                new CardDef { Id="pulso", Name="Pulso", Cost=4, Hp=360, Damage=42, Speed=24f, Range=24f, AttackCooldown=1.2f, Color=new Color(170, 120, 255), Kind=CardKind.Melee },
                new CardDef { Id="faisca", Name="Faísca", Cost=2, Hp=100, Damage=18, Speed=46f, Range=85f, AttackCooldown=0.7f, Color=new Color(120, 255, 180), Kind=CardKind.Ranged },
                new CardDef { Id="nova", Name="Nova", Cost=4, Hp=0, Damage=130, Speed=0f, Range=0f, AttackCooldown=0f, AreaRadius=56f, Color=new Color(255, 130, 70), Kind=CardKind.Spell },
                new CardDef { Id="torreta", Name="Torreta", Cost=3, Hp=340, Damage=26, Speed=0f, Range=115f, AttackCooldown=0.9f, Lifetime=22f, Color=new Color(180, 200, 220), Kind=CardKind.Building }
            };
        }
    }
}
