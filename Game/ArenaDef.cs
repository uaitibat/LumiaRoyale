namespace LumiaRoyale.Game
{
    // Cenário de arena. O primeiro mapa define o formato: rio no meio e duas
    // pontes alinhadas aos lanes. Cada mapa novo só informa onde esses
    // elementos estão na arte, para o motor e o desenho continuarem iguais.
    public sealed class ArenaDef
    {
        public string Id { get; set; } = "";
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public string Asset { get; set; } = "";

        public float RiverNorm { get; set; }       // centro vertical do rio na imagem
        public float RiverHalfNorm { get; set; }   // meia altura lógica da faixa do rio
        public float LaneLeftNorm { get; set; }    // centro da ponte esquerda
        public float LaneRightNorm { get; set; }   // centro da ponte direita
        public float BridgeHalfNorm { get; set; }  // meia largura utilizável da ponte

        public string Title { get { return "ARENA " + Number + " - " + Name; } }

        private static readonly ArenaDef[] Catalog =
        {
            new ArenaDef
            {
                Id = "vale",
                Number = 1,
                Name = "VALE DO VOID",
                Asset = "Assets/ArenaForest.png",
                RiverNorm = 0.4927f,
                RiverHalfNorm = 0.060f,
                LaneLeftNorm = 0.25f,
                LaneRightNorm = 0.75f,
                BridgeHalfNorm = 0.068f
            },
            new ArenaDef
            {
                Id = "deserto",
                Number = 2,
                Name = "DESERTO DOURADO",
                Asset = "Assets/ArenaDesert.png",
                RiverNorm = 0.4710f,
                RiverHalfNorm = 0.056f,
                LaneLeftNorm = 0.19f,
                LaneRightNorm = 0.81f,
                BridgeHalfNorm = 0.090f
            }
        };

        public static ArenaDef[] All { get { return Catalog; } }

        public static ArenaDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Catalog.Length; i++)
                if (Catalog[i].Id == id) return Catalog[i];
            return null;
        }
    }
}
