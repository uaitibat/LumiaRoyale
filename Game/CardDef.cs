using Microsoft.Xna.Framework;

namespace LumiaRoyale.Game
{
    // Categorias do elenco. O comportamento do motor vem das flags da carta
    // (SpellEffect, Structure, Flying...), não da categoria em si.
    public enum CardKind
    {
        Melee,
        Ranged,
        Tank,
        Fast,
        Magic,
        Air,
        Special
    }

    public sealed class CardDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Sprite { get; set; } = "";
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

        // true = conjura e desaparece (não cria unidade).
        public bool SpellEffect { get; set; }
        // true = construção com duração limitada.
        public bool Structure { get; set; }
        // true = atravessa o rio sem usar as pontes.
        public bool Flying { get; set; }
        // true = só ataca construções e torres.
        public bool TargetsBuildingsOnly { get; set; }
        // true = resposta rápida a alvos grandes (usada pela IA).
        public bool Swarm { get; set; }

        // Alcance longo dispara projétil; curto é corpo a corpo.
        public bool UsesProjectile => Range >= 60f;

        public static CardDef[] AllCards()
        {
            return new[]
            {
                // Corpo a corpo (6)
                new CardDef { Id="sentinela", Name="Sentinela", Sprite="Puny/Soldier-Blue.png", Cost=3, Hp=300, Damage=36, Speed=32f, Range=24f, AttackCooldown=0.95f, Color=new Color(90, 170, 255), Kind=CardKind.Melee },
                new CardDef { Id="guerreiro", Name="Guerreiro", Sprite="Puny/Monsters/Monster-BlondeDog.png", Cost=4, Hp=420, Damage=50, Speed=34f, Range=26f, AttackCooldown=0.95f, Color=new Color(210, 95, 85), Kind=CardKind.Melee },
                new CardDef { Id="espadachim", Name="Espadachim", Sprite="Puny/Soldier-Yellow.png", Cost=3, Hp=280, Damage=42, Speed=38f, Range=22f, AttackCooldown=0.8f, Color=new Color(120, 210, 130), Kind=CardKind.Melee },
                new CardDef { Id="orc", Name="Orc", Sprite="Puny/Orc-Grunt.png", Cost=3, Hp=330, Damage=46, Speed=34f, Range=24f, AttackCooldown=1f, Color=new Color(130, 170, 105), Kind=CardKind.Melee },
                new CardDef { Id="esqueleto", Name="Esqueleto", Sprite="Puny/Monsters/Monster-NavyRat.png", Cost=2, Hp=130, Damage=24, Speed=40f, Range=20f, AttackCooldown=0.75f, Color=new Color(225, 225, 210), Kind=CardKind.Melee },
                new CardDef { Id="minotauro", Name="Minotauro", Sprite="Puny/Monsters/Monster-BrownBoar.png", Cost=5, Hp=720, Damage=78, Speed=26f, Range=30f, AttackCooldown=1.2f, Color=new Color(190, 135, 90), Kind=CardKind.Melee },

                // Atiradores (5)
                new CardDef { Id="dardo", Name="Vigia", Sprite="Puny/Archer-Green.png", Cost=2, Hp=140, Damage=28, Speed=38f, Range=110f, AttackCooldown=0.9f, Color=new Color(255, 190, 80), Kind=CardKind.Ranged },
                new CardDef { Id="faisca", Name="Faisca", Sprite="Puny/Mage-Cyan.png", Cost=2, Hp=120, Damage=20, Speed=50f, Range=92f, AttackCooldown=0.65f, Color=new Color(120, 255, 180), Kind=CardKind.Ranged },
                new CardDef { Id="eco", Name="Eco", Sprite="Puny/Mage-Red.png", Cost=4, Hp=210, Damage=55, Speed=24f, Range=115f, AttackCooldown=1.6f, AreaRadius=34f, Color=new Color(255, 110, 180), Kind=CardKind.Ranged },
                new CardDef { Id="atiradora", Name="Atiradora", Sprite="Puny/Archer-Purple.png", Cost=3, Hp=150, Damage=36, Speed=36f, Range=130f, AttackCooldown=1f, Color=new Color(255, 215, 120), Kind=CardKind.Ranged },
                new CardDef { Id="bruxa", Name="Bruxa", Sprite="Puny/Human-Worker-Red.png", Cost=4, Hp=180, Damage=46, Speed=30f, Range=100f, AttackCooldown=1.1f, AreaRadius=20f, Color=new Color(190, 120, 240), Kind=CardKind.Ranged },

                // Tanques (4)
                new CardDef { Id="pulso", Name="Colosso", Sprite="Puny/Orc-Soldier-Cyan.png", Cost=5, Hp=780, Damage=64, Speed=18f, Range=28f, AttackCooldown=1.3f, Color=new Color(170, 120, 255), Kind=CardKind.Tank, TargetsBuildingsOnly=true },
                new CardDef { Id="ogre", Name="Ogre", Sprite="Puny/Monsters/Monster-GrayBoar.png", Cost=5, Hp=900, Damage=72, Speed=18f, Range=30f, AttackCooldown=1.3f, Color=new Color(165, 190, 110), Kind=CardKind.Tank },
                new CardDef { Id="rocha", Name="Rocha", Sprite="Puny/Human-Soldier-Cyan.png", Cost=6, Hp=1200, Damage=62, Speed=14f, Range=28f, AttackCooldown=1.5f, Color=new Color(155, 160, 170), Kind=CardKind.Tank },
                new CardDef { Id="couraca", Name="Couraca", Sprite="Puny/Human-Soldier-Red.png", Cost=6, Hp=1050, Damage=88, Speed=16f, Range=30f, AttackCooldown=1.4f, Color=new Color(110, 150, 190), Kind=CardKind.Tank },

                // Rápidas (4)
                new CardDef { Id="enxame", Name="Enxame", Sprite="Puny/Monsters/Monster-PurpleSpider.png", Cost=3, Hp=75, Damage=18, Speed=46f, Range=18f, AttackCooldown=0.7f, SpawnCount=4, Color=new Color(140, 220, 120), Kind=CardKind.Fast, Swarm=true },
                new CardDef { Id="lobo", Name="Lobo", Sprite="Puny/Monsters/Monster-GrayWolf.png", Cost=2, Hp=110, Damage=30, Speed=70f, Range=20f, AttackCooldown=0.6f, Color=new Color(190, 190, 205), Kind=CardKind.Fast },
                new CardDef { Id="licantropo", Name="Licantropo", Sprite="Puny/Monsters/Monster-BlackPanther.png", Cost=3, Hp=240, Damage=38, Speed=64f, Range=24f, AttackCooldown=0.7f, Color=new Color(150, 120, 190), Kind=CardKind.Fast },
                new CardDef { Id="felino", Name="Felino", Sprite="Puny/Monsters/Monster-OrangeCat.png", Cost=2, Hp=130, Damage=26, Speed=68f, Range=20f, AttackCooldown=0.6f, Color=new Color(235, 175, 105), Kind=CardKind.Fast },

                // Mágicas (4)
                new CardDef { Id="nova", Name="Nova", Sprite="Puny/Mage-Red.png", Cost=4, Hp=0, Damage=150, Speed=0f, Range=0f, AttackCooldown=0f, AreaRadius=60f, Color=new Color(255, 130, 70), Kind=CardKind.Magic, SpellEffect=true },
                new CardDef { Id="relampago", Name="Relampago", Sprite="Puny/Mage-Cyan.png", Cost=3, Hp=0, Damage=110, Speed=0f, Range=0f, AttackCooldown=0f, AreaRadius=45f, Color=new Color(120, 170, 255), Kind=CardKind.Magic, SpellEffect=true },
                new CardDef { Id="fogo", Name="Fogo", Sprite="Puny/Mage-Cyan.png", Cost=4, Hp=180, Damage=58, Speed=28f, Range=100f, AttackCooldown=1.2f, AreaRadius=24f, Color=new Color(255, 120, 60), Kind=CardKind.Magic },
                new CardDef { Id="gelo", Name="Gelo", Sprite="Puny/Mage-Cyan.png", Cost=4, Hp=220, Damage=40, Speed=26f, Range=105f, AttackCooldown=1.1f, AreaRadius=30f, Color=new Color(150, 225, 255), Kind=CardKind.Magic },

                // Aéreas (3)
                new CardDef { Id="morcego", Name="Morcego", Sprite="Puny/Monsters/Monster-BrownSpider.png", Cost=2, Hp=90, Damage=22, Speed=62f, Range=18f, AttackCooldown=0.6f, Color=new Color(130, 105, 170), Kind=CardKind.Air, Flying=true },
                new CardDef { Id="harpia", Name="Harpia", Sprite="Puny/Archer-Green.png", Cost=3, Hp=170, Damage=34, Speed=52f, Range=90f, AttackCooldown=0.85f, Color=new Color(205, 170, 120), Kind=CardKind.Air, Flying=true },
                new CardDef { Id="dragao", Name="Dragao", Sprite="Puny/Orc-Soldier-Red.png", Cost=6, Hp=680, Damage=86, Speed=40f, Range=60f, AttackCooldown=1.2f, Color=new Color(230, 90, 80), Kind=CardKind.Air, Flying=true },

                // Especiais (4)
                new CardDef { Id="torreta", Name="Torreta", Sprite="Torreta.png", Cost=3, Hp=380, Damage=30, Speed=0f, Range=122f, AttackCooldown=0.8f, Lifetime=26f, Color=new Color(180, 200, 220), Kind=CardKind.Special, Structure=true },
                new CardDef { Id="muralha", Name="Muralha", Sprite="Puny/Human-Worker-Cyan.png", Cost=2, Hp=520, Damage=0, Speed=0f, Range=0f, AttackCooldown=1f, Lifetime=18f, Color=new Color(200, 165, 125), Kind=CardKind.Special, Structure=true },
                new CardDef { Id="guardiao", Name="Guardiao", Sprite="Puny/Orc-Soldier-Cyan.png", Cost=4, Hp=620, Damage=44, Speed=0f, Range=110f, AttackCooldown=1f, Lifetime=30f, Color=new Color(115, 180, 120), Kind=CardKind.Special, Structure=true },
                new CardDef { Id="genio", Name="Genio", Sprite="Puny/Mage-Cyan.png", Cost=6, Hp=0, Damage=220, Speed=0f, Range=0f, AttackCooldown=0f, AreaRadius=72f, Color=new Color(110, 225, 220), Kind=CardKind.Special, SpellEffect=true }
            };
        }

        public static string[] StarterIds()
        {
            return new[] { "sentinela", "dardo", "pulso", "faisca", "nova", "torreta", "enxame", "eco" };
        }

        public static CardDef[] StarterDeck()
        {
            var all = AllCards();
            var ids = StarterIds();
            var deck = new CardDef[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                deck[i] = Find(all, ids[i]);
            return deck;
        }

        public static CardDef Find(CardDef[] cards, string id)
        {
            if (cards == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i].Id == id) return cards[i];
            return null;
        }

        public static string CategoryLabel(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Melee: return "CORPO";
                case CardKind.Ranged: return "ATIRADOR";
                case CardKind.Tank: return "TANQUE";
                case CardKind.Fast: return "RAPIDO";
                case CardKind.Magic: return "MAGIA";
                case CardKind.Air: return "AEREO";
                default: return "ESPECIAL";
            }
        }
    }
}
