using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace LumiaRoyale.Game
{
    public sealed class ArenaGame : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private Texture2D _archerBackTex;
        private Texture2D _punyArcherTex;
        private Texture2D _punyWarriorTex;
        private Texture2D _punySoldierTex;
        private Texture2D _punyMageTex;
        private Texture2D _punyArcherPurpleTex;
        private Texture2D _punyMageRedTex;
        private Texture2D _colossoTex;
        private Texture2D _torretaTex;
        private Texture2D _arenaTex;
        private Texture2D _homeBgTex;
        private Texture2D _homeBannerTex;

        private float _animTime;

        // SE98 / Void theme palette (from ~/.themes/se98-wm-theme-main)
        private static readonly Color Se98Face = new Color(212, 208, 200);      // #D4D0C8
        private static readonly Color Se98Light = new Color(255, 255, 255);     // highlight
        private static readonly Color Se98Shadow = new Color(148, 145, 140);    // dark shadow
        private static readonly Color Se98Dark = new Color(90, 88, 84);
        private static readonly Color Se98Selected = new Color(10, 36, 106);    // #0A246A
        private static readonly Color Se98TitleA = new Color(161, 196, 233);    // #A1C4E9
        private static readonly Color Se98TitleB = new Color(0, 0, 96);         // #000060
        private static readonly Color Se98Text = new Color(0, 0, 0);
        // Única cor da área de deploy: vermelho translúcido.
        private static readonly Color SpawnAreaFill = new Color(255, 70, 70, 38);
        private static readonly Color SpawnAreaBorder = new Color(255, 110, 110, 150);
        private static readonly Color HomeBackground = new Color(8, 12, 26);
        private static readonly Color HomeGrid = new Color(40, 60, 100, 45);

        // Puny Characters sheet: 32x32 cells, 8 rows = animations, 24 cols = 8 directions * 3 frames.
        private const int PunyCell = 32;
        private const int PunyFrontDir = 4; // S (de frente para a câmera)
        private const int BannerW = 280;
        private const int BannerH = 60;

        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<Tower> _towers = new List<Tower>();
        private readonly List<Fx> _fx = new List<Fx>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly Random _rng = new Random();

        private CardDef[] _deck;
        private CardDef[] _hand;
        private int _nextDraw;
        private float _mana;
        private const float ManaMax = 10f;
        private int _selectedHand = -1;

        private float _enemyMana;
        private float _enemySpawnTimer;
        private string _status = "Toque numa carta e depois na sua metade";
        private string _matchState = ""; // "intro", "home", "playing", "paused", "win", "lose"

        private Rectangle _arenaRect;
        private float _statusTimer;
        private float _timeLeft = 180f;
        private int _playerCrowns;
        private int _enemyCrowns;
        private int _playerTowersLost;
        private int _enemyTowersLost;
        private int _towerLevel = TowerBalance.BaseLevel;
        private float _introTime;
        private int _difficulty; // 0 = Novato, 1 = Veterano, 2 = Mestre

        // Progresso local (sem rede, salvo no LocalSettings).
        private int _trophies;
        private int _wins;
        private int _losses;
        private int _crowns;
        private int _coins;
        private int _gems;
        private int[] _chestProgress = new int[4];
        private bool[] _chestOpened = new bool[4];
        private int _homeTab; // 0 = batalha, 1 = cartas
        private int _selectedDeckCard = -1;
        private float _cardsSlide;   // 0 = painel fechado, 1 = aberto
        private bool _cardsDragging;
        private string _homeToast = "";
        private float _homeToastTimer;

        private struct Fx
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
            public string Kind; // "hit", "spark", "damage", "death", "spawn"
            public string Text;
        }

        private float _shake;
        private Vector2 _shakeOffset;

        public ArenaGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30.0);

            // Use real screen pixels (Lumia 640 => ~720x1280 portrait)
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;

            var di = DisplayInformation.GetForCurrentView();
            var b = CoreWindow.GetForCurrentThread().Bounds;
            int screenW = Math.Max(1, (int)(b.Width * di.RawPixelsPerViewPixel));
            int screenH = Math.Max(1, (int)(b.Height * di.RawPixelsPerViewPixel));
            _graphics.PreferredBackBufferWidth = screenW;
            _graphics.PreferredBackBufferHeight = screenH;
            _graphics.SupportedOrientations = DisplayOrientation.Portrait;
            _graphics.IsFullScreen = true;
        }

        protected override void Initialize()
        {
            TouchPanel.EnabledGestures = GestureType.Tap | GestureType.HorizontalDrag | GestureType.DragComplete;
            base.Initialize();
            LoadProgress();
            ResetMatch();
            _matchState = "intro";
            _introTime = 0f;
            _status = "";
        }

        private static int ReadInt(IDictionary<string, object> values, string key)
        {
            object raw;
            if (values.TryGetValue(key, out raw) && raw is int) return (int)raw;
            return 0;
        }

        private static bool ReadBool(IDictionary<string, object> values, string key)
        {
            object raw;
            if (values.TryGetValue(key, out raw) && raw is bool) return (bool)raw;
            return false;
        }

        private bool IsChestReady(int i)
        {
            return i >= 0 && i < _chestProgress.Length && !_chestOpened[i] && _chestProgress[i] >= 3;
        }

        private void LoadProgress()
        {
            var v = ApplicationData.Current.LocalSettings.Values;
            _trophies = ReadInt(v, "trophies");
            _wins = ReadInt(v, "wins");
            _losses = ReadInt(v, "losses");
            _crowns = ReadInt(v, "crowns");
            _coins = ReadInt(v, "coins");
            _gems = ReadInt(v, "gems");
            int total = 0;
            for (int i = 0; i < _chestProgress.Length; i++)
            {
                _chestProgress[i] = ReadInt(v, "chestProgress" + i);
                total += _chestProgress[i];
            }
            // Migra saves antigos (contador único) para o progresso por baú.
            if (total == 0)
            {
                int oldWins = ReadInt(v, "chestWins");
                bool oldReady = ReadBool(v, "chestReady");
                if (oldWins > 0 || oldReady)
                    _chestProgress[0] = oldReady ? 3 : Math.Min(3, oldWins);
            }
            for (int i = 0; i < _chestOpened.Length; i++)
                _chestOpened[i] = ReadBool(v, "chestOpened" + i);
            _towerLevel = TowerBalance.BaseLevel + _trophies / 150;
        }

        private void SaveProgress()
        {
            var v = ApplicationData.Current.LocalSettings.Values;
            v["trophies"] = _trophies;
            v["wins"] = _wins;
            v["losses"] = _losses;
            v["crowns"] = _crowns;
            v["coins"] = _coins;
            v["gems"] = _gems;
            for (int i = 0; i < _chestProgress.Length; i++)
                v["chestProgress" + i] = _chestProgress[i];
            for (int i = 0; i < _chestOpened.Length; i++)
                v["chestOpened" + i] = _chestOpened[i];
        }

        private void ShowHomeToast(string text)
        {
            _homeToast = text;
            _homeToastTimer = 2.2f;
        }

        // Cada vitória enche o baú; abrir dá troféus. Nível das torres segue os troféus.
        private void RecordResult(bool won)
        {
            if (won)
            {
                _wins++;
                _trophies += 30;
                _coins += 50;
                for (int i = 0; i < _chestProgress.Length; i++)
                {
                    if (_chestOpened[i] || _chestProgress[i] >= 3) continue;
                    _chestProgress[i]++;
                    if (_chestProgress[i] >= 3)
                        ShowHomeToast("BAU " + (i + 1) + " PRONTO!");
                    break;
                }
            }
            else
            {
                _losses++;
                _trophies = Math.Max(0, _trophies - 10);
            }

            _crowns += _playerCrowns;
            int newLevel = TowerBalance.BaseLevel + _trophies / 150;
            if (newLevel > _towerLevel)
            {
                _towerLevel = newLevel;
                _status = "NIVEL " + _towerLevel + "!";
                _statusTimer = 3f;
            }
            SaveProgress();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _archerBackTex = LoadTex("Assets/Sprites/arqueira_back.png");
            _punyArcherTex = LoadTex("Assets/Sprites/Puny/Archer-Green.png");
            _punyWarriorTex = LoadTex("Assets/Sprites/Puny/Warrior-Red.png");
            _punySoldierTex = LoadTex("Assets/Sprites/Puny/Soldier-Blue.png");
            _punyMageTex = LoadTex("Assets/Sprites/Puny/Mage-Cyan.png");
            _punyArcherPurpleTex = LoadTex("Assets/Sprites/Puny/Archer-Purple.png");
            _punyMageRedTex = LoadTex("Assets/Sprites/Puny/Mage-Red.png");
            _colossoTex = LoadTex("Assets/Sprites/Colosso.png");
            _torretaTex = LoadTex("Assets/Sprites/Torreta.png");
            _homeBgTex = LoadTex("Assets/HomeBackground.jpg");
            _homeBannerTex = LoadBannerTexture("Assets/HomeBanner.jpg");
        }

        // Estandarte/estampa de bandeira: foto espelhada, ponta em "<" e moldura embutida.
        private Texture2D LoadBannerTexture(string relativePath)
        {
            var src = LoadTex(relativePath);
            if (src == null) return null;
            int srcW = src.Width;
            int srcH = src.Height;
            var all = new Color[srcW * srcH];
            src.GetData(all);
            // Centro no eixo X e base alinhada com a base do banner no eixo Y.
            float aspect = (float)BannerW / BannerH;
            int w = srcW;
            int h = srcH;
            if ((float)srcW / srcH > aspect) w = (int)(srcH * aspect);
            else h = (int)(srcW / aspect);
            int left = (srcW - w) / 2;
            int top = srcH - h;
            var raw = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    raw[y * w + x] = all[(y + top) * srcW + (x + left)];
            var data = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    data[y * w + x] = raw[y * w + (w - 1 - x)];

            int notch = Math.Max(24, w / 5);
            float half = h / 2f;
            var cuts = new int[h];
            for (int y = 0; y < h; y++)
            {
                int cut = w - notch + (int)(notch * Math.Abs(y - half) / half);
                cuts[y] = Math.Max(0, Math.Min(w, cut));
            }

            for (int y = 0; y < h; y++)
            {
                int cut = cuts[y];
                for (int x = 0; x < w; x++)
                {
                    if (x >= cut)
                    {
                        data[y * w + x] = new Color(0, 0, 0, 0);
                        continue;
                    }
                    bool border = x < 3 || y < 3 || y >= h - 3 || x >= cut - 3;
                    bool shine = !border && (x < 6 || y < 6 || y >= h - 6 || x >= cut - 6);
                    if (border)
                        data[y * w + x] = new Color(18, 24, 38);
                    else if (shine)
                    {
                        var c = data[y * w + x];
                        data[y * w + x] = new Color(ClampByte(c.R + 40), ClampByte(c.G + 48), ClampByte(c.B + 60));
                    }
                }
            }

            var tex = new Texture2D(GraphicsDevice, w, h);
            tex.SetData(data);
            return tex;
        }

        private Texture2D LoadTex(string relativePath)
        {
            try
            {
                var installed = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                var path = System.IO.Path.Combine(installed, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                using (var s = System.IO.File.OpenRead(path))
                    return Texture2D.FromStream(GraphicsDevice, s);
            }
            catch { }
            try
            {
                using (var s = TitleContainer.OpenStream(relativePath))
                    return Texture2D.FromStream(GraphicsDevice, s);
            }
            catch { return null; }
        }

        private void ResetMatch()
        {
            _units.Clear();
            _towers.Clear();
            _fx.Clear();
            _projectiles.Clear();
            _deck = CardDef.StarterDeck().OrderBy(_ => _rng.Next()).ToArray();
            _hand = new CardDef[4];
            for (int i = 0; i < 4; i++) _hand[i] = _deck[i % _deck.Length];
            _nextDraw = 4 % _deck.Length;
            _mana = 5f;
            _enemyMana = 5f;
            _enemySpawnTimer = 2f;
            _selectedHand = -1;
            _status = "Toque numa carta e depois na sua metade";
            _matchState = "playing";
            _statusTimer = 3f;
            _timeLeft = 180f;
            _playerCrowns = 0;
            _enemyCrowns = 0;
            _playerTowersLost = 0;
            _enemyTowersLost = 0;

            int w = GraphicsDevice.Viewport.Width;
            int h = GraphicsDevice.Viewport.Height;
            int margin = Math.Max(10, w / 48);
            int arenaTop = Math.Max(80, h / 14);
            int arenaBottom = h - Math.Max(140, h / 8);
            _arenaRect = new Rectangle(margin, arenaTop, w - margin * 2, arenaBottom - arenaTop);
            BuildArenaTexture();

            float leftLaneX = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float rightLaneX = _arenaRect.Left + _arenaRect.Width * 0.75f;
            float centerX = _arenaRect.Left + _arenaRect.Width * 0.5f;

            int sideHp = TowerBalance.SideHp(_towerLevel);
            int kingHp = TowerBalance.KingHp(_towerLevel);

            // Enemy towers (top): side towers forward (closer to river), king receded (closer to top)
            _towers.Add(new Tower(new Vector2(leftLaneX, arenaTop + 165), sideHp, false, true, _towerLevel));
            _towers.Add(new Tower(new Vector2(rightLaneX, arenaTop + 165), sideHp, false, true, _towerLevel));
            _towers.Add(new Tower(new Vector2(centerX, arenaTop + 80), kingHp, true, true, _towerLevel));
            // Player towers (bottom): side towers forward (closer to river), king receded (closer to bottom)
            _towers.Add(new Tower(new Vector2(leftLaneX, arenaBottom - 165), sideHp, false, false, _towerLevel));
            _towers.Add(new Tower(new Vector2(rightLaneX, arenaBottom - 165), sideHp, false, false, _towerLevel));
            _towers.Add(new Tower(new Vector2(centerX, arenaBottom - 80), kingHp, true, false, _towerLevel));
        }

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0.1f) dt = 0.1f;

            HandleInput();
            UpdateFx(dt);
            if (_statusTimer > 0) _statusTimer -= dt;
            if (_homeToastTimer > 0) _homeToastTimer -= dt;

            if (_matchState == "intro")
            {
                _introTime += dt;
                if (_introTime >= 2.6f) { _matchState = "home"; _introTime = 0f; }
                base.Update(gameTime);
                return;
            }

            if (_matchState != "playing") { base.Update(gameTime); return; }

            _timeLeft -= dt;
            if (_timeLeft <= 0f) { _timeLeft = 0f; EndByCrowns(); base.Update(gameTime); return; }

            float manaRate = _timeLeft <= 60f ? 0.85f : 0.55f;
            _mana = Math.Min(ManaMax, _mana + dt * manaRate);
            float enemyRate = manaRate * (_difficulty == 0 ? 0.85f : (_difficulty == 2 ? 1.15f : 1f));
            _enemyMana = Math.Min(ManaMax, _enemyMana + dt * enemyRate);

            UpdateEnemy(dt);
            UpdateUnits(dt);
            UpdateTowers(dt);
            UpdateProjectiles(dt);

            for (int i = _units.Count - 1; i >= 0; i--)
            {
                if (_units[i].Alive) continue;
                var u = _units[i];
                Color deathColor = u.IsEnemy ? new Color(255, 120, 120) : new Color(120, 190, 255);
                AddBurst(u.Pos, deathColor, u.IsBuilding ? 10 : 8, 55f);
                AddFx(u.Pos, deathColor, u.IsBuilding ? 26f : 20f, 0.35f, "death");
                _units.RemoveAt(i);
            }
            CheckEnd();
            base.Update(gameTime);
        }

        private void EndByCrowns()
        {
            if (_playerCrowns > _enemyCrowns) { _matchState = "win"; _status = "Vitoria por coroas!"; }
            else if (_enemyCrowns > _playerCrowns) { _matchState = "lose"; _status = "Derrota por coroas"; }
            else
            {
                int p = _towers.Where(t => !t.IsEnemy && t.Alive).Sum(t => t.Hp);
                int e = _towers.Where(t => t.IsEnemy && t.Alive).Sum(t => t.Hp);
                if (p > e) { _matchState = "win"; _status = "Vitoria por vida!"; }
                else if (e > p) { _matchState = "lose"; _status = "Derrota por vida"; }
                else { _matchState = "lose"; _status = "Empate - reinicie"; }
            }
            RecordResult(_matchState == "win");
        }

        private void UpdateFx(float dt)
        {
            _animTime += dt;
            if (!_cardsDragging)
            {
                float target = _homeTab == 1 ? 1f : 0f;
                _cardsSlide += (target - _cardsSlide) * Math.Min(1f, dt * 12f);
            }
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                f.Life -= dt;
                f.Pos += f.Vel * dt;
                f.Vel *= Math.Max(0f, 1f - dt * 2.2f);
                f.Size += dt * (f.Kind == "damage" || f.Kind == "spark" ? 0f : (f.Kind == "hit" ? 18f : 34f));
                _fx[i] = f;
                if (f.Life <= 0) _fx.RemoveAt(i);
            }
            foreach (var u in _units)
            {
                if (u.HitFlash > 0) u.HitFlash -= dt;
                if (u.AttackAnim > 0) u.AttackAnim -= dt;
            }
            foreach (var t in _towers) if (t.HitFlash > 0) t.HitFlash -= dt;

            _shake = Math.Max(0f, _shake - dt * 2.6f);
            float amp = _shake * 5f;
            _shakeOffset = amp <= 0.01f
                ? Vector2.Zero
                : new Vector2((float)(_rng.NextDouble() * 2.0 - 1.0) * amp,
                              (float)(_rng.NextDouble() * 2.0 - 1.0) * amp);
        }

        private void AddFx(Vector2 pos, Color c, float size, float life, string kind)
        {
            AddFx(pos, c, size, life, kind, Vector2.Zero, null);
        }

        private void AddFx(Vector2 pos, Color c, float size, float life, string kind, Vector2 vel, string text)
        {
            if (_fx.Count > 240) _fx.RemoveAt(0); // teto pra não pesar no Lumia
            _fx.Add(new Fx
            {
                Pos = pos,
                Vel = vel,
                Life = life,
                MaxLife = life,
                Size = size,
                Color = c,
                Kind = kind,
                Text = text
            });
        }

        private void AddDamageNumber(Vector2 pos, int dmg, Color color)
        {
            if (dmg <= 0) return;
            float dx = (float)(_rng.NextDouble() * 2.0 - 1.0) * 9f;
            AddFx(pos + new Vector2(0f, -18f), Color.Lerp(Color.White, color, 0.45f),
                  0f, 0.55f, "damage", new Vector2(dx, -30f), dmg.ToString());
        }

        private void AddBurst(Vector2 pos, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = MathHelper.TwoPi * i / count + (float)_rng.NextDouble() * 0.5f;
                float v = speed * (0.6f + (float)_rng.NextDouble() * 0.7f);
                AddFx(pos, color, 5f + (i % 2) * 2f, 0.28f, "spark",
                      new Vector2((float)Math.Cos(ang) * v, (float)Math.Sin(ang) * v), null);
            }
        }

        private void DamageUnit(Unit u, int dmg, Color attackerColor)
        {
            if (u == null || !u.Alive || dmg <= 0) return;
            u.Hp -= dmg;
            u.HitFlash = 0.16f;
            AddDamageNumber(u.Pos, dmg, attackerColor);
            AddFx(u.Pos, Color.White, 9f, 0.12f, "hit");
        }

        private void CheckEnd()
        {
            if (_matchState != "playing") return;
            bool playerMain = _towers.Any(t => !t.IsEnemy && t.IsMain && t.Alive);
            bool enemyMain = _towers.Any(t => t.IsEnemy && t.IsMain && t.Alive);
            if (!playerMain) EndByKing(false);
            else if (!enemyMain) EndByKing(true);
        }

        // Rei caído encerra na hora, sem depender de coroas ou do cronômetro.
        private void EndByKing(bool playerWon)
        {
            if (_matchState != "playing") return;
            _matchState = playerWon ? "win" : "lose";
            _status = playerWon ? "VITORIA! REI CAIU" : "DERROTA! REI CAIU";
            RecordResult(playerWon);
        }

        private void AwardCrown(bool enemyTowerDestroyed)
        {
            if (enemyTowerDestroyed) { _playerCrowns++; _enemyTowersLost++; }
            else { _enemyCrowns++; _playerTowersLost++; }
        }

        private void HandleInput()
        {
            while (TouchPanel.IsGestureAvailable)
            {
                var g = TouchPanel.ReadGesture();

                // Arrastar da borda esquerda abre o painel de cartas; arrastar de volta fecha.
                if (_matchState == "home")
                {
                    if (g.GestureType == GestureType.HorizontalDrag)
                    {
                        _cardsDragging = true;
                        _cardsSlide = MathHelper.Clamp(_cardsSlide + g.Delta.X / (float)Math.Max(1, HX(400)), 0f, 1f);
                        continue;
                    }
                    if (g.GestureType == GestureType.DragComplete)
                    {
                        if (_cardsDragging)
                        {
                            _homeTab = _cardsSlide >= 0.5f ? 1 : 0;
                            _cardsDragging = false;
                        }
                        continue;
                    }
                }

                if (g.GestureType != GestureType.Tap) continue;
                var p = g.Position;

                if (_matchState == "intro")
                {
                    _matchState = "home";
                    _introTime = 0f;
                    return;
                }

                if (_matchState == "home")
                {
                    // Aba "cartas": painel lateral; toque na aba "batalha" volta.
                    if (GetNavCardsRect().Contains((int)p.X, (int)p.Y))
                    {
                        _homeTab = 1;
                        return;
                    }
                    if (GetNavBattleRect().Contains((int)p.X, (int)p.Y))
                    {
                        _homeTab = 0;
                        return;
                    }
                    if (_homeTab == 1)
                    {
                        for (int i = 0; i < _deck.Length; i++)
                        {
                            if (GetHomeCardRect(i).Contains((int)p.X, (int)p.Y))
                            {
                                _selectedDeckCard = _selectedDeckCard == i ? -1 : i;
                                return;
                            }
                        }
                        // Toque fora do painel (inclusive na arena) fecha o painel.
                        if (!GetCardsPanelRect().Contains((int)p.X, (int)p.Y)) _homeTab = 0;
                        return;
                    }

                    if (GetHomePlayRect().Contains((int)p.X, (int)p.Y))
                    {
                        ResetMatch();
                        return;
                    }
                    if (GetHomeDifficultyRect().Contains((int)p.X, (int)p.Y))
                    {
                        _difficulty = (_difficulty + 1) % 3;
                        ShowHomeToast("DIFICULDADE: " + GetDifficultyName());
                        return;
                    }
                    if (GetHomeExitRect().Contains((int)p.X, (int)p.Y))
                    {
                        Windows.UI.Xaml.Application.Current.Exit();
                        return;
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        if (!GetHomeChestRect(i).Contains((int)p.X, (int)p.Y)) continue;
                        if (IsChestReady(i))
                        {
                            _chestOpened[i] = true;
                            _chestProgress[i] = 0;
                            _trophies += 25;
                            _gems += 10;
                            int nextLevel = TowerBalance.BaseLevel + _trophies / 150;
                            if (nextLevel > _towerLevel) _towerLevel = nextLevel;
                            ShowHomeToast("BAU " + (i + 1) + " ABERTO +25 TROFEUS +10 GEMAS");
                            SaveProgress();
                        }
                        else ShowHomeToast(_chestOpened[i]
                            ? "BAU " + (i + 1) + " JA ABERTO"
                            : "BAU " + (i + 1) + " - " + _chestProgress[i] + "/3 VITORIAS");
                        return;
                    }
                    return;
                }

                if (_matchState == "win" || _matchState == "lose")
                {
                    if (GetEndRematchRect().Contains((int)p.X, (int)p.Y)) { ResetMatch(); return; }
                    if (GetEndMenuRect().Contains((int)p.X, (int)p.Y))
                    {
                        _matchState = "home";
                        _homeTab = 0;
                    }
                    return;
                }

                if (_matchState == "paused")
                {
                    if (GetPauseResumeRect().Contains((int)p.X, (int)p.Y)) { _matchState = "playing"; _status = "Retomado"; _statusTimer = 1f; }
                    else if (GetPauseRestartRect().Contains((int)p.X, (int)p.Y)) { ResetMatch(); }
                    return;
                }

                // pause button top-right HUD
                var pauseBtn = GetPauseButtonRect();
                if (pauseBtn.Contains((int)p.X, (int)p.Y))
                {
                    _matchState = "paused";
                    _selectedHand = -1;
                    return;
                }

                var cardRects = GetCardRects();
                for (int i = 0; i < 4; i++)
                {
                    if (cardRects[i].Contains((int)p.X, (int)p.Y))
                    {
                        _selectedHand = i;
                        _status = _hand[i].Name + " (" + _hand[i].Cost + " mana)";
                        _statusTimer = 2f;
                        return;
                    }
                }

                if (_selectedHand >= 0)
                {
                    var card = _hand[_selectedHand];
                    var pos = new Vector2(p.X, p.Y);
                    if (!_arenaRect.Contains((int)p.X, (int)p.Y)) return;
                    if (!IsValidDeploy(card, pos, false)) { _status = "Posicao invalida"; _statusTimer = 1.5f; return; }
                    if (_mana < card.Cost) { _status = "Mana insuficiente"; _statusTimer = 1.5f; return; }

                    PlayCard(card, pos, false);
                    _mana -= card.Cost;
                    DrawCard(_selectedHand);
                    _selectedHand = -1;
                }
            }
        }

        private Rectangle[] GetCardRects()
        {
            int cardH = Math.Max(104, GraphicsDevice.Viewport.Height / 9);
            int y = GraphicsDevice.Viewport.Height - cardH - 10;
            int gap = 8;
            int side = 10;
            int cardW = (GraphicsDevice.Viewport.Width - side * 2 - gap * 3) / 4;
            var rects = new Rectangle[4];
            for (int i = 0; i < 4; i++)
                rects[i] = new Rectangle(side + i * (cardW + gap), y, cardW, cardH);
            return rects;
        }

        // Retângulos centralizados: input e desenho usam exatamente os mesmos limites.
        private Rectangle GetPauseButtonRect()
        {
            return new Rectangle(GraphicsDevice.Viewport.Width - 42, 56, 32, 30);
        }

        private Rectangle GetPauseBoxRect()
        {
            return new Rectangle(GraphicsDevice.Viewport.Width / 2 - 150,
                                 GraphicsDevice.Viewport.Height / 2 - 95, 300, 190);
        }

        private Rectangle GetPauseResumeRect()
        {
            var box = GetPauseBoxRect();
            return new Rectangle(box.X + 40, box.Y + 60, 220, 38);
        }

        private Rectangle GetPauseRestartRect()
        {
            var box = GetPauseBoxRect();
            return new Rectangle(box.X + 40, box.Y + 108, 220, 38);
        }

        // Layout da home em coordenadas de projeto 480x800, escalado para a viewport.
        private int HX(float v) { return (int)(v * GraphicsDevice.Viewport.Width / 480f); }
        private int HY(float v) { return (int)(v * GraphicsDevice.Viewport.Height / 800f); }

        private Rectangle GetHomeTopBarRect()
        {
            return new Rectangle(HX(12), HY(8), HX(456), HY(78));
        }

        private Rectangle GetHomeBannerRect()
        {
            // Bandeira retangular na esquerda (X maior que Y).
            int w = HX(BannerW);
            int h = HY(BannerH);
            return new Rectangle(HX(14), HY(94), w, h);
        }

        private Rectangle GetHomeNickRect()
        {
            var b = GetHomeBannerRect();
            return new Rectangle(b.X, b.Bottom + HY(3), b.Width, HY(25));
        }

        private Rectangle GetHomeArenaRect()
        {
            // Arena independente do banner/nome: sempre no centro da tela.
            int size = HX(276);
            return new Rectangle(GraphicsDevice.Viewport.Width / 2 - size / 2,
                                 GraphicsDevice.Viewport.Height / 2 - size / 2, size, size);
        }

        private Rectangle GetHomeExitRect()
        {
            var a = GetHomeArenaRect();
            int bw = (a.Width - HX(16)) / 3;
            return new Rectangle(a.X, a.Bottom + HY(4), bw, HY(48));
        }

        private Rectangle GetHomePlayRect()
        {
            var a = GetHomeArenaRect();
            int bw = (a.Width - HX(16)) / 3;
            return new Rectangle(a.X + bw + HX(8), a.Bottom + HY(4), bw, HY(48));
        }

        private Rectangle GetHomeDifficultyRect()
        {
            var a = GetHomeArenaRect();
            int bw = (a.Width - HX(16)) / 3;
            return new Rectangle(a.X + (bw + HX(8)) * 2, a.Bottom + HY(4), bw, HY(48));
        }

        private Rectangle GetHomeChestRect(int index)
        {
            var buttons = GetHomePlayRect();
            int totalW = GraphicsDevice.Viewport.Width - HX(24);
            int gap = HX(10);
            int cw = (totalW - gap * 3) / 4;
            int x0 = (GraphicsDevice.Viewport.Width - totalW) / 2;
            return new Rectangle(x0 + index * (cw + gap), buttons.Bottom + HY(10), cw, HY(84));
        }

        private Rectangle GetNavRect()
        {
            return new Rectangle(HX(12), HY(730), HX(456), HY(62));
        }

        private Rectangle GetNavCardsRect()
        {
            var n = GetNavRect();
            return new Rectangle(n.X + 4, n.Y + 4, n.Width / 2 - 6, n.Height - 8);
        }

        private Rectangle GetNavBattleRect()
        {
            var n = GetNavRect();
            return new Rectangle(n.X + n.Width / 2 + 2, n.Y + 4, n.Width / 2 - 6, n.Height - 8);
        }

        private Rectangle GetCardsPanelRect()
        {
            var n = GetNavRect();
            int w = HX(400);
            int x = -(int)(w * (1f - _cardsSlide));
            return new Rectangle(x, HY(84), w, n.Y - HY(92));
        }

        private Rectangle GetHomeCardRect(int index)
        {
            var p = GetCardsPanelRect();
            int cw = (p.Width - HX(36)) / 2;
            int ch = HY(106);
            int col = index % 2;
            int row = index / 2;
            return new Rectangle(p.X + HX(12) + col * (cw + HX(12)),
                                 p.Y + HY(46) + row * (ch + HY(8)), cw, ch);
        }

        private string GetDifficultyName()
        {
            return _difficulty == 0 ? "NOVATO" : (_difficulty == 1 ? "VETERANO" : "MESTRE");
        }

        private Rectangle GetEndBoxRect()
        {
            return new Rectangle(GraphicsDevice.Viewport.Width / 2 - 180,
                                 GraphicsDevice.Viewport.Height / 2 - 100, 360, 200);
        }

        private Rectangle GetEndRematchRect()
        {
            var box = GetEndBoxRect();
            return new Rectangle(box.X + 30, box.Y + 144, 150, 36);
        }

        private Rectangle GetEndMenuRect()
        {
            var box = GetEndBoxRect();
            return new Rectangle(box.X + 190, box.Y + 144, 140, 36);
        }

        private void DrawCard(int handIndex)
        {
            _hand[handIndex] = _deck[_nextDraw % _deck.Length];
            _nextDraw = (_nextDraw + 1) % _deck.Length;
        }

        private bool IsValidDeploy(CardDef card, Vector2 pos, bool enemy)
        {
            if (!_arenaRect.Contains((int)pos.X, (int)pos.Y)) return false;
            if (card.Kind == CardKind.Spell) return true;

            int midY = _arenaRect.Center.Y + 20;
            bool onOwnSide = enemy ? pos.Y <= midY : pos.Y >= midY;
            if (onOwnSide) return true;

            // Pocket: começa na ponte e vai até pouco antes da torre lateral destruída.
            // Com as duas laterais caídas, vira um retângulo até perto da torre do rei.
            return GetDeployPockets(enemy).Any(p => p.Contains((int)pos.X, (int)pos.Y));
        }

        private void PlayCard(CardDef card, Vector2 pos, bool enemy)
        {
            pos.X = MathHelper.Clamp(pos.X, _arenaRect.Left + 15, _arenaRect.Right - 15);
            pos.Y = MathHelper.Clamp(pos.Y, _arenaRect.Top + 15, _arenaRect.Bottom - 15);
            if (card.Kind == CardKind.Spell)
            {
                CastSpell(card, pos, enemy);
                return;
            }
            float laneX = GetLaneX(pos.X);
            int count = Math.Max(1, card.SpawnCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPos = pos;
                if (count > 1)
                {
                    float ang = MathHelper.TwoPi * i / count;
                    float r = 14f + (i % 2) * 4f;
                    spawnPos += new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * r;
                    spawnPos.X = MathHelper.Clamp(spawnPos.X, _arenaRect.Left + 15, _arenaRect.Right - 15);
                    spawnPos.Y = MathHelper.Clamp(spawnPos.Y, _arenaRect.Top + 15, _arenaRect.Bottom - 15);
                }
                _units.Add(new Unit(card, spawnPos, enemy, laneX));
                AddFx(spawnPos, card.Color, card.Kind == CardKind.Building ? 18f : (count > 1 ? 10f : 14f), 0.22f, "spawn");
            }
        }

        private float GetLaneX(float x)
        {
            float left = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float right = _arenaRect.Left + _arenaRect.Width * 0.75f;
            return Math.Abs(x - left) <= Math.Abs(x - right) ? left : right;
        }

        private void CastSpell(CardDef card, Vector2 pos, bool enemy)
        {
            AddFx(pos, card.Color, card.AreaRadius, 0.35f, "hit");
            AddFx(pos, Color.White, card.AreaRadius * 0.5f, 0.2f, "spawn");
            foreach (var u in _units)
            {
                if (!u.Alive || u.IsEnemy == enemy) continue;
                if (Vector2.Distance(u.Pos, pos) <= card.AreaRadius + u.Radius) DamageUnit(u, card.Damage, card.Color);
            }
            foreach (var t in _towers)
            {
                if (!t.Alive || t.IsEnemy == enemy) continue;
                float tr = t.IsMain ? 34f : 28f;
                if (Vector2.Distance(t.Pos, pos) <= card.AreaRadius + tr)
                {
                    DamageTower(t, card.Damage, !enemy);
                }
            }
        }

        private void SpawnUnit(CardDef def, Vector2 pos, bool enemy)
        {
            PlayCard(def, pos, enemy);
        }

        private void UpdateEnemy(float dt)
        {
            _enemySpawnTimer -= dt;
            if (_enemySpawnTimer > 0) return;
            float interval = _difficulty == 0 ? 2.6f : (_difficulty == 2 ? 1.3f : 1.8f);
            _enemySpawnTimer = interval + (float)_rng.NextDouble() * interval;

            var affordable = _deck.Where(c => c.Cost <= _enemyMana).ToList();
            if (affordable.Count == 0) return;

            var card = ChooseEnemyCard(affordable);
            if (card == null) return;

            Vector2 pos = ChooseEnemyPos(card);
            if (!IsValidDeploy(card, pos, true)) return;

            _enemyMana -= card.Cost;
            SpawnUnit(card, pos, true);
        }

        private CardDef ChooseEnemyCard(List<CardDef> affordable)
        {
            var playerUnits = _units.Where(u => !u.IsEnemy && u.Alive).ToList();
            var enemyUnits = _units.Where(u => u.IsEnemy && u.Alive).ToList();

            // Spell: use on cluster of player units
            var spell = affordable.Where(c => c.Kind == CardKind.Spell).OrderByDescending(c => c.Cost).FirstOrDefault();
            if (spell != null)
            {
                Vector2 cluster = FindClusterCenter(playerUnits, spell.AreaRadius);
                if (cluster != Vector2.Zero && CountNear(playerUnits, cluster, spell.AreaRadius) >= 2)
                    return spell;
            }

            // Defense: player pushing close to enemy side
            var threats = playerUnits.Where(u => u.Pos.Y < _arenaRect.Center.Y + 40f).ToList();
            if (threats.Count > 0)
            {
                Vector2 threatCenter = AveragePos(threats);
                int nearCount = CountNear(playerUnits, threatCenter, 90f);
                bool hasColossusThreat = threats.Any(u => u.Def.Id == "pulso");

                var building = affordable.FirstOrDefault(c => c.Kind == CardKind.Building);
                if (building != null && (nearCount >= 1 || hasColossusThreat) && !enemyUnits.Any(u => u.IsBuilding && Vector2.Distance(u.Pos, threatCenter) < 140f))
                    return building;

                if (hasColossusThreat)
                {
                    var swarm = affordable.FirstOrDefault(c => c.Id == "enxame");
                    if (swarm != null) return swarm;
                }

                if (nearCount >= 2)
                {
                    var splashOrRanged = affordable.FirstOrDefault(c => c.Kind == CardKind.Ranged && c.Cost >= 2);
                    if (splashOrRanged != null) return splashOrRanged;
                }

                var counter = affordable
                    .Where(c => c.Kind == CardKind.Melee || c.Kind == CardKind.Ranged)
                    .OrderByDescending(c => c.Hp)
                    .FirstOrDefault();
                if (counter != null) return counter;
            }

            // Offensive: if we have mana advantage or few player units, push a lane
            if (_enemyMana >= 6f || playerUnits.Count <= 1)
            {
                var push = affordable
                    .Where(c => c.Kind == CardKind.Melee || c.Kind == CardKind.Ranged)
                    .OrderByDescending(c => c.Cost)
                    .FirstOrDefault();
                if (push != null) return push;
            }

            // Default: cheapest non-spell, or random affordable
            return affordable
                .OrderBy(c => c.Kind == CardKind.Spell ? 10 : 0)
                .ThenBy(c => c.Cost)
                .FirstOrDefault();
        }

        private Vector2 ChooseEnemyPos(CardDef card)
        {
            float leftX = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float rightX = _arenaRect.Left + _arenaRect.Width * 0.75f;
            var playerUnits = _units.Where(u => !u.IsEnemy && u.Alive).ToList();

            if (card.Kind == CardKind.Spell)
            {
                Vector2 cluster = FindClusterCenter(playerUnits, card.AreaRadius);
                if (cluster != Vector2.Zero) return cluster;
                var tower = _towers.Where(t => !t.IsEnemy && t.Alive).OrderBy(t => t.IsMain ? 1 : 0).FirstOrDefault();
                return tower != null ? tower.Pos : new Vector2(_arenaRect.Center.X, _arenaRect.Bottom - 120);
            }

            // Pick lane with most threat, or weak player tower
            float laneX = leftX;
            int leftThreat = playerUnits.Count(u => Math.Abs(u.Pos.X - leftX) < _arenaRect.Width * 0.3f);
            int rightThreat = playerUnits.Count(u => Math.Abs(u.Pos.X - rightX) < _arenaRect.Width * 0.3f);
            if (rightThreat > leftThreat) laneX = rightX;
            else if (rightThreat == leftThreat)
            {
                var pl = _towers.FirstOrDefault(t => !t.IsEnemy && !t.IsMain && t.Alive && Math.Abs(t.Pos.X - leftX) < 20f);
                var pr = _towers.FirstOrDefault(t => !t.IsEnemy && !t.IsMain && t.Alive && Math.Abs(t.Pos.X - rightX) < 20f);
                int lh = pl != null ? pl.Hp : 99999;
                int rh = pr != null ? pr.Hp : 99999;
                laneX = rh < lh ? rightX : leftX;
            }

            // Se alguma lateral do jogador caiu, às vezes ataca pelo bolso (da ponte até a torre).
            var pockets = GetDeployPockets(true);
            bool canPocket = pockets.Count > 0 && card.Kind != CardKind.Building && _enemyMana >= 5f;
            if (canPocket && _rng.NextDouble() < 0.45)
            {
                var p = pockets[_rng.Next(pockets.Count)];
                float px = p.X + p.Width * (0.2f + (float)_rng.NextDouble() * 0.6f);
                float py = p.Y + p.Height * (0.2f + (float)_rng.NextDouble() * 0.6f);
                return new Vector2(px, py);
            }

            float y;
            if (card.Kind == CardKind.Building)
            {
                y = _arenaRect.Center.Y - 70f - (float)_rng.NextDouble() * 40f;
            }
            else
            {
                var laneThreats = playerUnits.Where(u => Math.Abs(u.Pos.X - laneX) < _arenaRect.Width * 0.35f && u.Pos.Y < _arenaRect.Center.Y).ToList();
                if (laneThreats.Count > 0)
                {
                    float ty = laneThreats.Min(u => u.Pos.Y);
                    y = Math.Max(_arenaRect.Top + 30f, ty - 50f);
                }
                else
                {
                    y = _arenaRect.Top + 90f + (float)_rng.NextDouble() * (_arenaRect.Height * 0.18f);
                }
            }

            float x = laneX + ((float)_rng.NextDouble() - 0.5f) * 24f;
            return new Vector2(x, y);
        }

        private static Vector2 FindClusterCenter(List<Unit> units, float radius)
        {
            if (units.Count == 0) return Vector2.Zero;
            Vector2 best = Vector2.Zero;
            int bestCount = 1;
            for (int i = 0; i < units.Count; i++)
            {
                int count = 0;
                Vector2 sum = Vector2.Zero;
                for (int j = 0; j < units.Count; j++)
                {
                    if (Vector2.Distance(units[i].Pos, units[j].Pos) <= radius)
                    {
                        count++;
                        sum += units[j].Pos;
                    }
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    best = sum / count;
                }
            }
            return best;
        }

        private static int CountNear(List<Unit> units, Vector2 pos, float radius)
        {
            int c = 0;
            for (int i = 0; i < units.Count; i++)
                if (Vector2.Distance(units[i].Pos, pos) <= radius) c++;
            return c;
        }

        private static Vector2 AveragePos(List<Unit> units)
        {
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < units.Count; i++) sum += units[i].Pos;
            return sum / Math.Max(1, units.Count);
        }

        private void UpdateUnits(float dt)
        {
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                if (u.IsBuilding) { u.LifeLeft -= dt; }
                u.AttackTimer -= dt;
                var target = FindTarget(u);
                if (target.HasValue)
                {
                    u.AimPos = target.Value.pos;
                    u.HasAim = true;
                    u.AimIsStructure = target.Value.isTower ||
                                       (target.Value.unit != null && target.Value.unit.IsBuilding);
                }
                else
                {
                    u.HasAim = false;
                    u.AimPos = u.Pos;
                    u.AimIsStructure = false;
                }
                if (target == null) continue;

                var toTarget = target.Value.pos - u.Pos;
                if (toTarget.LengthSquared() > 1f) u.Facing = Vector2.Normalize(toTarget);

                float dist = Vector2.Distance(u.Pos, target.Value.pos);
                if (dist <= u.Def.Range + target.Value.radius)
                {
                    if (u.AttackTimer <= 0f)
                    {
                        u.AttackAnim = 0.14f;
                        if (u.Def.Kind == CardKind.Ranged)
                        {
                            SpawnProjectile(u, target.Value);
                        }
                        else
                        {
                            target.Value.applyDamage(u.Def.Damage);
                            AddFx(Vector2.Lerp(u.Pos, target.Value.pos, 0.6f), u.Def.Color, 8f, 0.16f, "hit");
                        }
                        u.AttackTimer = u.Def.AttackCooldown;
                    }
                }
                else if (!u.IsBuilding)
                {
                    Vector2 moveTarget = GetMoveTarget(u, target.Value);
                    var dir = moveTarget - u.Pos;
                    if (dir.LengthSquared() > 1f)
                    {
                        dir.Normalize();
                        u.Pos += dir * u.Def.Speed * dt;
                        u.WalkPhase += dt * (8f + u.Def.Speed * 0.08f);
                    }
                }
            }

            ResolveUnitSeparation();
            foreach (var u in _units) if (u.Alive && !u.IsBuilding) ClampToArena(u);
        }

        private void ResolveUnitSeparation()
        {
            // simple separation pass to avoid stacking
            for (int i = 0; i < _units.Count; i++)
            {
                var a = _units[i];
                if (!a.Alive || a.IsBuilding) continue;
                for (int j = i + 1; j < _units.Count; j++)
                {
                    var b = _units[j];
                    if (!b.Alive || b.IsBuilding) continue;

                    Vector2 delta = b.Pos - a.Pos;
                    float distSq = delta.LengthSquared();
                    float minDist = a.Radius + b.Radius + 2f;
                    if (distSq > minDist * minDist) continue;

                    float dist = (float)Math.Sqrt(distSq);
                    Vector2 push;
                    if (dist < 0.001f)
                    {
                        // deterministic-ish nudge based on index
                        float ang = (i * 0.7f + j * 1.3f);
                        push = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 0.5f;
                    }
                    else
                    {
                        push = (delta / dist) * ((minDist - dist) * 0.5f);
                    }

                    bool aMove = CanPush(a);
                    bool bMove = CanPush(b);
                    if (aMove && bMove)
                    {
                        a.Pos -= push;
                        b.Pos += push;
                    }
                    else if (aMove)
                    {
                        a.Pos -= push * 2f;
                    }
                    else if (bMove)
                    {
                        b.Pos += push * 2f;
                    }
                }
            }
        }

        private bool CanPush(Unit u)
        {
            // avoid pushing units out of valid river crossings too aggressively
            return !u.IsBuilding;
        }

        private void SpawnProjectile(Unit u, TargetInfo target)
        {
            Vector2 dir = target.pos - u.Pos;
            if (dir.LengthSquared() < 1f) dir = u.Facing;
            dir.Normalize();
            float speed = u.Def.AreaRadius > 0 ? 220f : 260f;
            var p = new Projectile
            {
                Pos = u.Pos + dir * (u.Radius + 4f),
                Vel = dir * speed,
                Damage = u.Def.Damage,
                ByPlayer = !u.IsEnemy,
                Color = u.Def.Color,
                Radius = u.Def.AreaRadius > 0 ? 5f : 4f,
                Life = 2.5f,
                UnitTarget = target.unit,
                TowerTarget = target.tower,
                AreaRadius = u.Def.AreaRadius
            };
            _projectiles.Add(p);
        }

        private void ApplyProjectileDamage(Projectile p, Vector2 hitPos)
        {
            if (p.AreaRadius > 0)
            {
                AddFx(hitPos, p.Color, p.AreaRadius, 0.22f, "hit");
                foreach (var u in _units)
                {
                    if (!u.Alive || u.IsEnemy == !p.ByPlayer) continue;
                    if (Vector2.Distance(u.Pos, hitPos) <= p.AreaRadius + u.Radius) DamageUnit(u, p.Damage, p.Color);
                }
                foreach (var t in _towers)
                {
                    if (!t.Alive || t.IsEnemy == !p.ByPlayer) continue;
                    float tr = t.IsMain ? 34f : 28f;
                    if (Vector2.Distance(t.Pos, hitPos) <= p.AreaRadius + tr) DamageTower(t, p.Damage, p.ByPlayer);
                }
            }
            else
            {
                // Alvo inimigo tem IsEnemy == ByPlayer: player ataca IsEnemy=true,
                // IA ataca IsEnemy=false. A comparação invertida aqui fazia os
                // projéteis (Vigia/Faisca/Torreta) nunca causarem dano.
                if (p.TowerTarget != null && p.TowerTarget.Alive && p.TowerTarget.IsEnemy == p.ByPlayer && Vector2.Distance(p.TowerTarget.Pos, hitPos) < 80f)
                {
                    DamageTower(p.TowerTarget, p.Damage, p.ByPlayer);
                }
                else if (p.UnitTarget != null && p.UnitTarget.Alive && p.UnitTarget.IsEnemy == p.ByPlayer && Vector2.Distance(p.UnitTarget.Pos, hitPos) < 80f)
                {
                    DamageUnit(p.UnitTarget, p.Damage, p.Color);
                }
                else
                {
                    // fallback: nearest opposing unit, or opposing tower at the hit point
                    bool applied = false;
                    foreach (var u in _units)
                    {
                        if (!u.Alive || u.IsEnemy == !p.ByPlayer) continue;
                        float rr = u.Radius + p.Radius + 2f;
                        if (Vector2.DistanceSquared(p.Pos, u.Pos) <= rr * rr)
                        { DamageUnit(u, p.Damage, p.Color); applied = true; break; }
                    }
                    if (!applied)
                    {
                        foreach (var t in _towers)
                        {
                            if (!t.Alive || t.IsEnemy == !p.ByPlayer) continue;
                            float tr = t.IsMain ? 34f : 28f;
                            float rr = tr + p.Radius + 2f;
                            if (Vector2.DistanceSquared(p.Pos, t.Pos) <= rr * rr)
                            { DamageTower(t, p.Damage, p.ByPlayer); break; }
                        }
                    }
                }
                AddFx(hitPos, p.Color, 8f, 0.14f, "hit");
            }
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (!p.Alive) { _projectiles.RemoveAt(i); continue; }
                p.Life -= dt;
                if (p.Life <= 0) { _projectiles.RemoveAt(i); continue; }

                // slight homing for better feel
                Vector2 targetPos = Vector2.Zero;
                bool hasTarget = false;
                if (p.UnitTarget != null && p.UnitTarget.Alive) { targetPos = p.UnitTarget.Pos; hasTarget = true; }
                else if (p.TowerTarget != null && p.TowerTarget.Alive) { targetPos = p.TowerTarget.Pos; hasTarget = true; }

                if (hasTarget)
                {
                    Vector2 desired = targetPos - p.Pos;
                    if (desired.LengthSquared() > 1f)
                    {
                        desired.Normalize();
                        float speed = p.Vel.Length();
                        var cur = Vector2.Normalize(p.Vel);
                        var blended = Vector2.Normalize(cur * 0.82f + desired * 0.18f);
                        p.Vel = blended * speed;
                    }
                }

                p.Pos += p.Vel * dt;

                bool hit = false;
                // hit unit
                foreach (var u in _units)
                {
                    if (!u.Alive) continue;
                    if (u.IsEnemy == !p.ByPlayer) continue;
                    if (Vector2.DistanceSquared(p.Pos, u.Pos) <= (u.Radius + p.Radius) * (u.Radius + p.Radius))
                    {
                        ApplyProjectileDamage(p, u.Pos);
                        hit = true;
                        break;
                    }
                }
                if (!hit)
                {
                    foreach (var t in _towers)
                    {
                        if (!t.Alive) continue;
                        if (t.IsEnemy == !p.ByPlayer) continue;
                        float tr = t.IsMain ? 34f : 28f;
                        if (Vector2.DistanceSquared(p.Pos, t.Pos) <= (tr + p.Radius) * (tr + p.Radius))
                        {
                            ApplyProjectileDamage(p, t.Pos);
                            hit = true;
                            break;
                        }
                    }
                }

                if (hit || !_arenaRect.Contains((int)p.Pos.X, (int)p.Pos.Y))
                    _projectiles.RemoveAt(i);
            }
        }

        private void ClampToArena(Unit u)
        {
            u.Pos.X = MathHelper.Clamp(u.Pos.X, _arenaRect.Left + 12, _arenaRect.Right - 12);
            u.Pos.Y = MathHelper.Clamp(u.Pos.Y, _arenaRect.Top + 12, _arenaRect.Bottom - 12);

            float bridgeY = _arenaRect.Center.Y;
            float riverTop = bridgeY - 14f;
            float riverBottom = bridgeY + 14f;
            bool inRiver = u.Pos.Y > riverTop && u.Pos.Y < riverBottom;
            if (!inRiver) return;

            float leftBridge = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float rightBridge = _arenaRect.Left + _arenaRect.Width * 0.75f;
            float bridgeHalf = 24f;
            bool onBridge = Math.Abs(u.Pos.X - leftBridge) <= bridgeHalf || Math.Abs(u.Pos.X - rightBridge) <= bridgeHalf;
            if (onBridge) return;

            // push out of river to nearest side
            if (u.IsEnemy)
                u.Pos.Y = riverBottom + 1f;
            else
                u.Pos.Y = riverTop - 1f;
        }

        private struct TargetInfo
        {
            public Vector2 pos;
            public float radius;
            public bool isTower;
            public bool isMain;
            public bool isEnemy;
            public Unit unit;
            public Tower tower;
            public Action<int> applyDamage;
        }

        private Tower GetSideTowerInLane(bool isEnemy, float laneX)
        {
            return _towers.FirstOrDefault(t => t.IsEnemy == isEnemy && !t.IsMain && Math.Abs(t.Pos.X - laneX) < 20f);
        }

        private TargetInfo? FindTarget(Unit u)
        {
            TargetInfo? best = null;
            float bestScore = float.MaxValue;
            bool buildingOnly = u.Def.Id == "pulso"; // Colosso targets only buildings/towers
            var laneTower = GetSideTowerInLane(!u.IsEnemy, u.LaneX);
            bool laneOpen = laneTower == null || !laneTower.Alive;

            if (!buildingOnly)
            {
                foreach (var other in _units)
                {
                    if (other.IsEnemy == u.IsEnemy || !other.Alive) continue;
                    float d = Vector2.Distance(u.Pos, other.Pos);
                    float score = d;
                    if (d < 140f) score -= 40f; // prefer nearby threats
                    if (score < bestScore)
                    {
                        bestScore = score;
                        var o = other;
                        best = new TargetInfo { pos = o.Pos, radius = o.Radius, isTower = false, isMain = false, isEnemy = o.IsEnemy, unit = o, tower = null, applyDamage = dmg => DamageUnit(o, dmg, u.Def.Color) };
                    }
                }

                // If a unit is already close to combat, keep it on unit target; otherwise route to towers.
                if (best.HasValue && bestScore < 120f) return best;
            }

            TargetInfo? bestStructure = null;
            float bestStructureScore = float.MaxValue;

            // enemy buildings (e.g. Torreta)
            foreach (var other in _units)
            {
                if (other.IsEnemy == u.IsEnemy || !other.Alive || !other.IsBuilding) continue;
                float d = Vector2.Distance(u.Pos, other.Pos);
                float laneDist = Math.Abs(other.Pos.X - u.LaneX);
                float score = d + laneDist * 0.2f - 30f;
                if (laneOpen && laneDist > 40f) score += 400f; // segue pelo lane aberto até o rei
                if (score < bestStructureScore)
                {
                    bestStructureScore = score;
                    var o = other;
                    bestStructure = new TargetInfo { pos = o.Pos, radius = o.Radius, isTower = false, isMain = false, isEnemy = o.IsEnemy, unit = o, tower = null, applyDamage = dmg => DamageUnit(o, dmg, u.Def.Color) };
                }
            }

            foreach (var t in _towers)
            {
                if (t.IsEnemy == u.IsEnemy || !t.Alive) continue;
                float laneDist = Math.Abs(t.Pos.X - u.LaneX);
                float d = Vector2.Distance(u.Pos, t.Pos);
                float score = d + laneDist * 0.35f;
                if (!t.IsMain)
                {
                    score -= 60f; // side towers first
                    if (laneOpen && laneDist > 40f) score += 400f; // não troca de lane depois de abrir o caminho
                }
                if (score < bestStructureScore)
                {
                    bestStructureScore = score;
                    var tw = t;
                    float tr = tw.IsMain ? 34f : 28f;
                    bestStructure = new TargetInfo { pos = tw.Pos, radius = tr, isTower = true, isMain = tw.IsMain, isEnemy = tw.IsEnemy, unit = null, tower = tw, applyDamage = dmg => { DamageTower(tw, dmg, !u.IsEnemy); } };
                }
            }

            if (buildingOnly) return bestStructure;
            return bestStructure ?? best;
        }

        private Vector2 GetMoveTarget(Unit u, TargetInfo target)
        {
            float bridgeY = _arenaRect.Center.Y;
            float riverTop = bridgeY - 14f;
            float riverBottom = bridgeY + 14f;
            bool onMySide = u.IsEnemy ? u.Pos.Y < riverTop : u.Pos.Y > riverBottom;
            bool onEnemySide = u.IsEnemy ? u.Pos.Y > riverBottom : u.Pos.Y < riverTop;
            bool targetOnOtherSide = u.IsEnemy ? target.pos.Y > riverBottom : target.pos.Y < riverTop;

            float left = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float right = _arenaRect.Left + _arenaRect.Width * 0.75f;
            float laneX = u.LaneX;

            // Pick bridge lane for crossing
            float crossLane = Math.Abs(u.Pos.X - left) <= Math.Abs(u.Pos.X - right) ? left : right;
            if (target.isTower && !target.isMain)
            {
                crossLane = laneX; // side tower: own lane
            }
            else if (target.isTower && target.isMain)
            {
                var leftTower = GetSideTowerInLane(target.isEnemy, left);
                var rightTower = GetSideTowerInLane(target.isEnemy, right);
                bool leftOpen = leftTower == null || !leftTower.Alive;
                bool rightOpen = rightTower == null || !rightTower.Alive;
                if (leftOpen && !rightOpen) crossLane = left;      // abre pelo lane da torre caída
                else if (rightOpen && !leftOpen) crossLane = right;
                else if (leftOpen && rightOpen) crossLane = Math.Abs(u.LaneX - left) <= Math.Abs(u.LaneX - right) ? left : right;
                else crossLane = Math.Abs(u.Pos.X - left) <= Math.Abs(u.Pos.X - right) ? left : right;
            }

            float exitY = u.IsEnemy ? riverBottom + 18f : riverTop - 18f;

            // Cross river when target is on other side
            if (onMySide && targetOnOtherSide)
            {
                if (Math.Abs(u.Pos.X - crossLane) > 12f)
                    return new Vector2(crossLane, u.Pos.Y);
                return new Vector2(crossLane, exitY);
            }

            // Currently in river: keep crossing to exit
            if (!onMySide && !onEnemySide)
            {
                // snap to nearest bridge while in river
                crossLane = Math.Abs(u.Pos.X - left) <= Math.Abs(u.Pos.X - right) ? left : right;
                return new Vector2(crossLane, exitY);
            }

            // Same side as target
            if (!target.isTower) return target.pos;

            if (!target.isMain)
            {
                // On enemy side: keep lane until close to side tower
                if (Math.Abs(target.pos.X - laneX) < 40f || Math.Abs(u.Pos.Y - target.pos.Y) > 70f)
                    return new Vector2(laneX, target.pos.Y);
                return target.pos;
            }

            // King tower: if on enemy side, route through a lane then approach center king
            if (onEnemySide)
            {
                float approachLane = crossLane;
                if (Math.Abs(u.Pos.Y - target.pos.Y) > 80f && Math.Abs(u.Pos.X - approachLane) > 18f)
                    return new Vector2(approachLane, u.Pos.Y);
                if (Math.Abs(u.Pos.Y - target.pos.Y) > 60f)
                    return new Vector2(approachLane, target.pos.Y);
                return target.pos;
            }

            return target.pos;
        }

        private void UpdateTowers(float dt)
        {
            foreach (var t in _towers)
            {
                if (!t.Alive) continue;
                t.CooldownLeft -= dt;
                if (!t.Awake) continue;
                if (t.CooldownLeft > 0) continue;
                Unit target = null;
                float best = float.MaxValue;
                foreach (var u in _units)
                {
                    if (u.IsEnemy == t.IsEnemy || !u.Alive) continue;
                    float d = Vector2.Distance(t.Pos, u.Pos);
                    if (d <= t.Range && d < best) { best = d; target = u; }
                }
                if (target != null)
                {
                    DamageUnit(target, t.Damage, new Color(255, 220, 120));
                    AddFx(Vector2.Lerp(t.Pos, target.Pos, 0.7f), new Color(255, 240, 160), 7f, 0.12f, "hit");
                    t.CooldownLeft = t.Cooldown;
                }
            }
        }

        private void DamageTower(Tower t, int dmg, bool byPlayer)
        {
            if (!t.Alive || dmg <= 0) return;
            bool wasAwake = t.Awake;
            t.Hp -= dmg;
            t.HitFlash = 0.16f;
            AddDamageNumber(t.Pos + new Vector2(0f, -12f), dmg,
                byPlayer ? new Color(150, 220, 255) : new Color(255, 150, 140));
            AddFx(t.Pos, Color.White, 12f, 0.14f, "hit");
            if (t.IsMain) t.Awake = true;

            if (!t.Alive)
            {
                AddBurst(t.Pos, t.IsEnemy ? new Color(255, 130, 110) : new Color(120, 190, 255), 12, 70f);
                _shake = Math.Max(_shake, t.IsMain ? 1f : 0.65f);
                if (t.IsMain) EndByKing(t.IsEnemy);
            }
            else if (t.IsMain && !wasAwake)
            {
                AddBurst(t.Pos, new Color(255, 225, 140), 10, 55f);
                _shake = Math.Max(_shake, 0.5f);
                _status = "REI ACORDOU!";
                _statusTimer = 2f;
            }

            if (!t.IsMain && !t.Alive)
            {
                AwardCrown(t.IsEnemy);
                // awaken king when a princess tower falls
                var king = _towers.FirstOrDefault(x => x.IsEnemy == t.IsEnemy && x.IsMain);
                if (king != null) king.Awake = true;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(HomeBackground);

            bool inMatch = _matchState == "playing" || _matchState == "paused" ||
                           _matchState == "win" || _matchState == "lose";

            if (inMatch)
            {
                _spriteBatch.Begin(transformMatrix: Matrix.CreateTranslation(new Vector3(_shakeOffset.X, _shakeOffset.Y, 0f)));
                DrawArena();
                DrawLaneHints();
                DrawDeployHint();
                DrawTowers();
                DrawUnits();
                DrawFx();
                _spriteBatch.End();
            }

            // UI in pixel-perfect nearest-neighbor (SE98 / retro look)
            _spriteBatch.Begin(samplerState: Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp);
            if (_matchState == "intro") DrawIntro();
            else if (_matchState == "home") DrawHome();
            else
            {
                DrawHud();
                DrawCards();
                DrawEndOverlay();
            }
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private static int ClampByte(int v)
        {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        private void BuildArenaTexture()
        {
            int w = Math.Max(1, _arenaRect.Width);
            int h = Math.Max(1, _arenaRect.Height);
            if (_arenaTex != null && _arenaTex.Width == w && _arenaTex.Height == h) return;
            if (_arenaTex != null) _arenaTex.Dispose();

            float leftLane = w * 0.25f;
            float rightLane = w * 0.75f;
            var data = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool light = ((x / 16) + (y / 16)) % 2 == 0;
                    int jitter = ((x * 37 + y * 17 + ((x * y) >> 3)) & 7) - 3;
                    int r = (light ? 46 : 41) + jitter / 2;
                    int g = (light ? 116 : 106) + jitter;
                    int b = (light ? 64 : 58) + jitter / 2;

                    float laneDist = Math.Min(Math.Abs(x - leftLane), Math.Abs(x - rightLane));
                    if (laneDist < 54f) { r += 4; g += 9; b += 3; }
                    if (x < 6 || y < 6 || x >= w - 6 || y >= h - 6) { r += 12; g += 10; b += 6; }

                    data[y * w + x] = new Color(ClampByte(r), ClampByte(g), ClampByte(b));
                }
            }

            var tex = new Texture2D(GraphicsDevice, w, h);
            tex.SetData(data);
            _arenaTex = tex;
        }

        private void DrawArena()
        {
            if (_arenaTex != null)
                _spriteBatch.Draw(_arenaTex, _arenaRect, Color.White);
            else
                DrawRect(_arenaRect, new Color(46, 112, 62));

            DrawRect(new Rectangle(_arenaRect.Left + 8, _arenaRect.Top + 8, _arenaRect.Width - 16, _arenaRect.Height - 16), new Color(76, 146, 88), 2);

            var river = new Rectangle(_arenaRect.Left, _arenaRect.Center.Y - 14, _arenaRect.Width, 28);
            DrawRect(river, new Color(52, 120, 190));
            DrawRect(new Rectangle(river.X, river.Y + 3, river.Width, 3), new Color(120, 190, 240, 110));
            DrawRect(new Rectangle(river.X, river.Bottom - 8, river.Width, 3), new Color(120, 190, 240, 80));

            // ondas animadas, baratas (uma linha a cada 24 px)
            int drift = (int)(_animTime * 14f) % 24;
            for (int x = river.X - 24 + drift; x < river.Right; x += 24)
            {
                int oy = (int)(Math.Sin(x * 0.05f + _animTime * 2f) * 2f);
                DrawRect(new Rectangle(x, river.Y + 9 + oy, 12, 2), new Color(210, 238, 255, 110));
                DrawRect(new Rectangle(x + 6, river.Y + 18 - oy, 10, 2), new Color(200, 232, 255, 70));
            }

            DrawBridge(_arenaRect.Left + _arenaRect.Width / 4);
            DrawBridge(_arenaRect.Left + 3 * _arenaRect.Width / 4);

            // center line hint
            DrawRect(new Rectangle(_arenaRect.Left, _arenaRect.Center.Y + 18, _arenaRect.Width, 2), new Color(255, 255, 255, 35));
        }

        private void DrawBridge(int centerX)
        {
            var r = new Rectangle(centerX - 24, _arenaRect.Center.Y - 20, 48, 40);
            DrawRect(r, new Color(132, 102, 74));
            DrawRect(r, new Color(88, 64, 44), 2);

            // tábuas
            for (int y = r.Y + 4; y < r.Bottom - 3; y += 6)
                DrawRect(new Rectangle(r.X + 3, y, r.Width - 6, 2), new Color(96, 70, 48, 170));

            // trilhos laterais
            DrawRect(new Rectangle(r.X + 2, r.Y + 3, 4, r.Height - 6), new Color(170, 138, 100));
            DrawRect(new Rectangle(r.Right - 6, r.Y + 3, 4, r.Height - 6), new Color(170, 138, 100));
            DrawRect(new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, 3), new Color(190, 158, 120));
            DrawRect(new Rectangle(r.X + 6, r.Bottom - 9, r.Width - 12, 3), new Color(190, 158, 120));
        }

        // Depois que uma lateral cai, mostra o caminho aberto até o rei daquele lado.
        private void DrawLaneHints()
        {
            if (_matchState == "intro" || _matchState == "home" || _matchState == "win" || _matchState == "lose") return;

            foreach (var t in _towers)
            {
                if (t.IsMain || t.Alive) continue;
                var king = _towers.FirstOrDefault(k => k.IsEnemy == t.IsEnemy && k.IsMain);
                if (king == null) continue;

                bool playerLane = t.IsEnemy; // lateral inimiga caiu => caminho do jogador
                float yStart = playerLane ? _arenaRect.Center.Y - 14f : _arenaRect.Center.Y + 14f;
                float yEnd = playerLane ? king.Pos.Y + 58f : king.Pos.Y - 58f;
                int y = (int)Math.Min(yStart, yEnd);
                int h = (int)Math.Max(8f, Math.Abs(yEnd - yStart));

                // Sem faixa retangular: só as setas mostram o caminho aberto.
                Color arrowTint = new Color(255, 230, 170);
                int dir = playerLane ? -1 : 1;
                int drift = (int)(_animTime * 34f) % 48;
                for (int off = 8 + drift; off < h - 8; off += 48)
                {
                    int ay = dir < 0 ? y + h - off : y + off;
                    int ax = (int)t.Pos.X;
                    int a = 95 + (int)(Math.Sin(_animTime * 3f + off) * 25f);
                    Color arrow = new Color(arrowTint.R, arrowTint.G, arrowTint.B, (byte)ClampByte(a));
                    int low = dir < 0 ? 3 : 0;
                    int high = dir < 0 ? 0 : 3;
                    DrawRect(new Rectangle(ax - 7, ay + low, 5, 2), arrow);
                    DrawRect(new Rectangle(ax - 2, ay + high, 5, 2), arrow);
                    DrawRect(new Rectangle(ax + 3, ay + low, 5, 2), arrow);
                }
            }
        }

        private void DrawDeployHint()
        {
            if (_selectedHand < 0 || _matchState != "playing") return;
            var card = _hand[_selectedHand];
            if (card.Kind == CardKind.Spell)
            {
                DrawRect(_arenaRect, SpawnAreaFill);
                DrawRect(_arenaRect, SpawnAreaBorder, 2);
                return;
            }
            int midY = _arenaRect.Center.Y + 20;
            var allowed = new Rectangle(_arenaRect.X, midY, _arenaRect.Width, _arenaRect.Bottom - midY);
            DrawRect(allowed, SpawnAreaFill);
            DrawRect(allowed, SpawnAreaBorder, 2);

            // pockets when enemy side towers are down - mesma cor fixa da área de spawn
            foreach (var pocket in GetDeployPockets(false))
            {
                DrawRect(pocket, SpawnAreaFill);
                DrawRect(pocket, SpawnAreaBorder, 2);
            }
        }

        private List<Rectangle> GetDeployPockets(bool enemyPlayer)
        {
            var pockets = new List<Rectangle>();
            bool targetIsEnemy = !enemyPlayer;
            var sideTowers = _towers.Where(t => t.IsEnemy == targetIsEnemy && !t.IsMain).ToList();
            if (sideTowers.Count == 0) return pockets;

            // Borda da ponte (rio) que separa os lados.
            float bridgeEdgeY = enemyPlayer ? _arenaRect.Center.Y + 14f : _arenaRect.Center.Y - 14f;

            // Com as duas torres laterais caídas: retângulo largo, da ponte até perto do rei.
            if (sideTowers.All(t => !t.Alive))
            {
                var king = _towers.FirstOrDefault(t => t.IsEnemy == targetIsEnemy && t.IsMain);
                if (king == null) return pockets;
                float kingEdgeY = enemyPlayer ? king.Pos.Y - 60f : king.Pos.Y + 60f;
                int y = (int)Math.Min(bridgeEdgeY, kingEdgeY);
                int h = (int)Math.Max(8f, Math.Abs(kingEdgeY - bridgeEdgeY));
                pockets.Add(new Rectangle(_arenaRect.X, y, _arenaRect.Width, h));
                return pockets;
            }

            // Uma lateral caída: área começa na ponte e para um pouco antes da torre, sem cobrir ela.
            float pocketHalfW = _arenaRect.Width * 0.22f;
            foreach (var tower in sideTowers.Where(t => !t.Alive))
            {
                float towerEdgeY = enemyPlayer ? tower.Pos.Y - 40f : tower.Pos.Y + 40f;
                int y = (int)Math.Min(bridgeEdgeY, towerEdgeY);
                int h = (int)Math.Max(8f, Math.Abs(towerEdgeY - bridgeEdgeY));
                int x = (int)Math.Max(_arenaRect.X, tower.Pos.X - pocketHalfW);
                int right = (int)Math.Min(_arenaRect.Right, tower.Pos.X + pocketHalfW);
                pockets.Add(new Rectangle(x, y, Math.Max(8, right - x), h));
            }
            return pockets;
        }

        private void DrawTowers()
        {
            foreach (var t in _towers)
            {
                if (!t.Alive) { DrawTowerRubble(t); continue; }
                Color team = t.IsEnemy ? new Color(208, 74, 74) : new Color(74, 146, 226);
                Color teamDark = t.IsEnemy ? new Color(120, 36, 36) : new Color(36, 78, 132);
                int size = t.IsMain ? 76 : 60;
                var body = new Rectangle((int)t.Pos.X - size / 2, (int)t.Pos.Y - size / 2, size, size);

                // plataforma de pedra sob a torre
                int padW = t.IsMain ? 92 : 74;
                var pad = new Rectangle((int)t.Pos.X - padW / 2, body.Bottom - 10, padW, 14);
                DrawRect(pad, new Color(30, 58, 36, 150));
                DrawRect(new Rectangle(pad.X + 5, pad.Y + 2, pad.Width - 10, 8), new Color(118, 122, 112, 200));
                DrawRect(new Rectangle(pad.X + 5, pad.Y + 2, pad.Width - 10, 2), new Color(170, 174, 164, 190));

                DrawRect(new Rectangle(body.X + 8, body.Bottom - 4, body.Width, 8), new Color(0, 0, 0, 45));

                if (_torretaTex != null)
                {
                    DrawTorretaSprite(body, team, t.HitFlash > 0);
                }
                else
                {
                    var baseR = new Rectangle(body.X - 6, body.Bottom - 10, body.Width + 12, 14);
                    DrawRect(baseR, new Color(88, 88, 96));
                    DrawRect(body, new Color(165, 170, 180));
                    DrawRect(body, teamDark, 2);
                    DrawRect(new Rectangle(body.X + 6, body.Y + 6, body.Width - 12, 12), team);
                    DrawRect(new Rectangle(body.Center.X - 5, body.Y - 10, 10, 14), teamDark);
                    if (t.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 120));
                }

                float pct = Math.Max(0, (float)t.Hp / t.MaxHp);
                var hbg = new Rectangle(body.X - 8, body.Y - 16, body.Width + 16, 7);
                var hfg = new Rectangle(hbg.X, hbg.Y, (int)(hbg.Width * pct), hbg.Height);
                DrawRect(hbg, new Color(20, 20, 20, 200));
                Color hpFill = team;
                if (pct <= 0.35f) hpFill = Color.Lerp(hpFill, new Color(255, 205, 90), 0.45f);
                DrawRect(hfg, hpFill);
                for (int i = 1; i < 4; i++)
                    DrawRect(new Rectangle(hbg.X + hbg.Width * i / 4, hbg.Y, 1, hbg.Height), new Color(0, 0, 0, 110));
                DrawRect(hbg, new Color(255, 255, 255, 70), 1);

                DrawPixelText("L" + t.Level, body.Center.X - 8, body.Bottom + 1, 2,
                              new Color(225, 232, 245, 190));
                if (t.IsMain) DrawKingIndicator(t, body);
            }
        }

        private void DrawTowerRubble(Tower t)
        {
            int size = t.IsMain ? 76 : 60;
            int cx = (int)t.Pos.X;
            int baseY = (int)t.Pos.Y + size / 2;
            Color stone = t.IsEnemy ? new Color(126, 78, 74) : new Color(76, 96, 122);
            Color dark = new Color(42, 46, 42, 200);

            DrawRect(new Rectangle(cx - size / 2 - 6, baseY - 6, size + 12, 10), new Color(24, 32, 24, 130));
            DrawRect(new Rectangle(cx - 24, baseY - 12, 18, 10), dark);
            DrawRect(new Rectangle(cx - 5, baseY - 19, 19, 14), stone);
            DrawRect(new Rectangle(cx + 14, baseY - 10, 13, 8), dark);
            DrawRect(new Rectangle(cx - 20, baseY - 15, 11, 3), new Color(210, 210, 200, 90));
            DrawRect(new Rectangle(cx - 3, baseY - 21, 13, 3), new Color(210, 210, 200, 70));
        }

        private void DrawKingIndicator(Tower t, Rectangle body)
        {
            int ix = body.Center.X;
            int iy = body.Y - 32;
            if (t.Awake)
            {
                int pulse = (int)(Math.Sin(_animTime * 5f) * 18f);
                Color glow = new Color(255, 220 - pulse, 110, 210);
                DrawRect(new Rectangle(ix - 10, iy + 5, 20, 5), glow);
                DrawRect(new Rectangle(ix - 7, iy, 4, 6), glow);
                DrawRect(new Rectangle(ix - 2, iy - 3, 4, 8), glow);
                DrawRect(new Rectangle(ix + 3, iy, 4, 6), glow);
            }
            else
            {
                DrawPixelText("Z", ix - 4, iy + 1, 2, new Color(215, 230, 250, 150));
                DrawPixelText("Z", ix + 6, iy - 5, 2, new Color(215, 230, 250, 85));
            }
        }

        private void DrawTorretaSprite(Rectangle dst, Color team, bool hitFlash)
        {
            if (_torretaTex == null) return;
            Color tint = Color.Lerp(Color.White, team, 0.32f);
            if (hitFlash) tint = Color.Lerp(tint, Color.White, 0.7f);
            _spriteBatch.Draw(_torretaTex, dst, tint);
        }

        private void DrawUnits()
        {
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                Color team = u.IsEnemy ? new Color(210, 70, 70) : new Color(80, 150, 235);
                Color dark = u.IsEnemy ? new Color(120, 30, 30) : new Color(35, 75, 130);

                if (u.IsBuilding)
                {
                    DrawBuildingUnit(u, team, dark);
                    continue;
                }

                bool drawn = u.Def.Id == "pulso" ? DrawColossoSprite(u, team) : DrawPunyUnit(u, team);
                if (!drawn)
                {
                    if (u.Def.Id == "pulso") DrawColossus(u, team, dark);
                    else if (u.Def.Id == "enxame") DrawSwarm(u, team, dark);
                    else if (u.Def.Id == "eco") DrawEco(u, team, dark);
                    else if (u.Def.Kind == CardKind.Ranged && u.Def.Id == "dardo") DrawArcher(u, team, dark);
                    else if (u.Def.Kind == CardKind.Ranged && u.Def.Id == "faisca") DrawPunyMage(u, team, dark);
                    else if (u.Def.Kind == CardKind.Ranged) DrawRanged(u, team, dark);
                    else DrawMelee(u, team, dark);
                }

                int hpW = u.Def.Id == "pulso" ? 64 : (u.Def.Id == "enxame" ? 28 : 44);
                int hpY = u.Def.Id == "pulso" ? 56 : (u.Def.Id == "enxame" ? 30 : 46);
                DrawHpBar((int)u.Pos.X - hpW / 2, (int)u.Pos.Y - hpY, hpW, 4, u.Hp, u.MaxHp);
                if (!u.IsEnemy) DrawAimHint(u);
            }
        }

        // Guia discreto do alvo atual das tropas do jogador.
        private void DrawAimHint(Unit u)
        {
            if (_matchState != "playing" || !u.HasAim) return;
            Vector2 to = u.AimPos - u.Pos;
            float len = to.Length();
            if (len < 26f) return;

            Vector2 dir = to / len;
            float maxLen = Math.Min(len - u.Radius, 84f);
            Color c = u.AimIsStructure ? new Color(255, 225, 150, 105) : new Color(215, 230, 255, 65);
            for (float d = u.Radius + 12f; d < maxLen; d += 13f)
            {
                int px = (int)(u.Pos.X + dir.X * d);
                int py = (int)(u.Pos.Y + dir.Y * d);
                DrawRect(new Rectangle(px - 1, py - 1, 2, 2), c);
            }

            if (u.AimIsStructure && len <= 150f)
            {
                int ax = (int)u.AimPos.X;
                int ay = (int)u.AimPos.Y;
                Color mark = new Color(255, 230, 150, 150);
                DrawRect(new Rectangle(ax - 5, ay, 4, 2), mark);
                DrawRect(new Rectangle(ax + 2, ay, 4, 2), mark);
                DrawRect(new Rectangle(ax, ay - 5, 2, 4), mark);
                DrawRect(new Rectangle(ax, ay + 2, 2, 4), mark);
            }
        }

        private void DrawSwarm(Unit u, Color team, Color dark)
        {
            float bob = (float)Math.Sin(u.WalkPhase * 3f) * 1f;
            int x = (int)u.Pos.X;
            int y = (int)(u.Pos.Y + bob);
            var body = new Rectangle(x - 5, y - 4, 10, 10);
            DrawRect(new Rectangle(body.X + 1, body.Bottom, body.Width - 1, 3), new Color(0, 0, 0, 45));
            DrawRect(body, u.Def.Color);
            DrawRect(body, dark, 1);
            DrawRect(new Rectangle(x - 3, y - 7, 6, 4), Color.Lerp(u.Def.Color, Color.White, 0.2f));
            DrawRect(new Rectangle(x - 4, y, 8, 2), team);
            int dir = u.Facing.X >= 0 ? 1 : -1;
            DrawRect(new Rectangle(x + dir * 4, y - 2, 2, 6), new Color(230, 230, 240));
            if (u.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 120));
        }

        private void DrawEco(Unit u, Color team, Color dark)
        {
            float bob = (float)Math.Sin(u.WalkPhase * 1.8f) * 1.5f;
            int x = (int)u.Pos.X;
            int y = (int)(u.Pos.Y + bob);
            var body = new Rectangle(x - 10, y - 10, 20, 22);
            DrawRect(new Rectangle(body.X + 2, body.Bottom, body.Width - 2, 4), new Color(0, 0, 0, 50));
            DrawRect(body, u.Def.Color);
            DrawRect(body, dark, 2);
            DrawRect(new Rectangle(x - 7, y - 15, 14, 8), Color.Lerp(u.Def.Color, Color.White, 0.18f));
            DrawRect(new Rectangle(x - 7, y - 15, 14, 8), dark, 1);
            // cannon core
            DrawRect(new Rectangle(x - 4, y - 2, 8, 8), new Color(60, 40, 60));
            DrawRect(new Rectangle(x - 2, y, 4, 4), team);
            // cannon barrel
            int dir = u.Facing.X >= 0 ? 1 : -1;
            DrawRect(new Rectangle(x + dir * 7, y - 1, 10, 5), new Color(70, 60, 80));
            if (u.AttackAnim > 0)
            {
                DrawRect(new Rectangle(x + dir * 16, y - 4, 8, 10), new Color(255, 200, 230, 180));
                DrawRect(new Rectangle(x - 14, y - 14, 28, 28), new Color(u.Def.Color, 40), 1);
            }
            if (u.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 110));
        }

        private void DrawBuildingUnit(Unit u, Color team, Color dark)
        {
            var b = new Rectangle((int)u.Pos.X - 24, (int)u.Pos.Y - 24, 48, 48);
            DrawRect(new Rectangle(b.X + 5, b.Bottom - 2, b.Width, 6), new Color(0, 0, 0, 50));

            if (u.Def.Id == "torreta" && _torretaTex != null)
            {
                DrawTorretaSprite(b, team, u.HitFlash > 0);
            }
            else
            {
                DrawRect(b, new Color(140, 145, 155));
                DrawRect(b, dark, 2);
                DrawRect(new Rectangle(b.X + 4, b.Y + 4, b.Width - 8, 10), Color.Lerp(u.Def.Color, Color.Black, 0.2f));
                DrawRect(new Rectangle(b.Center.X - 3, b.Y - 6, 6, 8), team);
                if (u.AttackAnim > 0) DrawRect(new Rectangle(b.Center.X - 2, b.Y - 10, 4, 4), Color.White);
                if (u.HitFlash > 0) DrawRect(b, new Color(255, 255, 255, 110));
            }

            DrawHpBar(b.X - 2, b.Y - 11, b.Width + 4, 4, u.Hp, u.MaxHp);
            float lt = u.Def.Lifetime > 0 ? MathHelper.Clamp(u.LifeLeft / u.Def.Lifetime, 0, 1) : 0;
            DrawRect(new Rectangle(b.X - 2, b.Bottom + 5, b.Width + 4, 3), new Color(20, 20, 20, 160));
            DrawRect(new Rectangle(b.X - 2, b.Bottom + 5, (int)((b.Width + 4) * lt), 3), new Color(200, 200, 255));
        }

        private void DrawMelee(Unit u, Color team, Color dark)
        {
            // Fallback quando o spritesheet Puny não carregar.
            float bob = (float)Math.Sin(u.WalkPhase * 2f) * 1.5f;
            int x = (int)u.Pos.X;
            int y = (int)(u.Pos.Y + bob);
            int s = 20;
            var body = new Rectangle(x - s / 2, y - s / 2, s, s);
            DrawRect(new Rectangle(body.X + 2, body.Bottom, body.Width - 2, 4), new Color(0, 0, 0, 55));
            DrawRect(body, u.Def.Color);
            DrawRect(body, dark, 2);
            DrawRect(new Rectangle(body.X + 3, body.Y + 3, body.Width - 6, 5), new Color(255, 255, 255, 45));
            DrawRect(new Rectangle(x - 5, body.Y - 6, 10, 7), Color.Lerp(u.Def.Color, Color.White, 0.15f));
            DrawRect(new Rectangle(x - 5, body.Y - 6, 10, 7), dark, 1);
            DrawRect(new Rectangle(x - 6, body.Y + 2, 12, 3), team);
            int dir = u.Facing.X >= 0 ? 1 : -1;
            int swing = u.AttackAnim > 0 ? 3 : 0;
            DrawRect(new Rectangle(x + dir * 8, y - 8 - swing, 3, 14), new Color(230, 230, 240));
            DrawRect(new Rectangle(x + dir * 7, y - 10 - swing, 5, 3), new Color(200, 200, 210));
            if (u.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 110));
        }

        private void DrawPunyMage(Unit u, Color team, Color dark)
        {
            // Fallback quando o spritesheet Puny não carregar.
            DrawRanged(u, team, dark);
        }

        private void DrawRanged(Unit u, Color team, Color dark)
        {
            float bob = (float)Math.Sin(u.WalkPhase * 2.4f) * 1.2f;
            int x = (int)u.Pos.X;
            int y = (int)(u.Pos.Y + bob);
            var body = new Rectangle(x - 8, y - 8, 16, 18);
            DrawRect(new Rectangle(body.X + 2, body.Bottom, body.Width - 2, 4), new Color(0, 0, 0, 50));
            DrawRect(body, u.Def.Color);
            DrawRect(body, dark, 2);
            // head/core
            DrawRect(new Rectangle(x - 5, y - 13, 10, 8), Color.Lerp(u.Def.Color, Color.White, 0.2f));
            DrawRect(new Rectangle(x - 2, y - 11, 4, 4), new Color(255, 255, 255, 180));
            // team marker
            DrawRect(new Rectangle(x - 5, y + 1, 10, 3), team);
            // barrel
            int dir = u.Facing.X >= 0 ? 1 : -1;
            DrawRect(new Rectangle(x + dir * 6, y - 2, 8, 4), new Color(60, 60, 70));
            if (u.AttackAnim > 0) DrawRect(new Rectangle(x + dir * 14, y - 3, 4, 6), new Color(255, 255, 200));
            if (u.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 110));
        }

        private bool DrawPunyUnit(Unit u, Color team)
        {
            Texture2D tex = GetUnitSpriteTexture(u.Def);
            if (tex == null) return false;

            int attackRow = 2; // espada
            if (u.Def.Id == "dardo" || u.Def.Id == "eco") attackRow = 3; // arco
            else if (u.Def.Id == "faisca") attackRow = 4; // cajado

            // Personagens 2x maiores que antes.
            int size = u.Def.Id == "enxame" ? 56 : 80;
            DrawPunySprite(u, tex, attackRow, team, size);
            return true;
        }

        private bool DrawColossoSprite(Unit u, Color team)
        {
            if (_colossoTex == null) return false;

            Vector2 f = u.Facing;
            if (f.LengthSquared() < 0.001f) f = new Vector2(0, 1);
            f.Normalize();

            // Colosso.png: 4 células de 32x32 -> [frente idle, frente ataque, costas idle, costas ataque].
            bool back = f.Y < -0.35f;
            int frame = u.AttackAnim > 0f ? 1 : 0;
            int col = (back ? 2 : 0) + frame;
            var src = new Rectangle(col * 32, 0, 32, 32);

            int size = 104;
            float bob = u.AttackAnim <= 0f && u.Def.Speed > 0f ? (float)Math.Sin(u.WalkPhase) * 1.6f : 0f;
            var dst = new Rectangle((int)(u.Pos.X - size / 2f), (int)(u.Pos.Y - size / 2f + bob - 4), size, size);
            DrawRect(new Rectangle(dst.X + 8, dst.Bottom - 8, dst.Width - 16, 6), new Color(0, 0, 0, 45));

            Color tint = Color.Lerp(Color.White, team, 0.10f);
            if (u.HitFlash > 0) tint = Color.Lerp(tint, Color.White, 0.75f);
            var fx = f.X < -0.35f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            _spriteBatch.Draw(_colossoTex, dst, src, tint, 0f, Vector2.Zero, fx, 0f);
            return true;
        }

        private Texture2D GetUnitSpriteTexture(CardDef def)
        {
            // Reaproveita o mesmo mapa das cartas para os personagens dentro da arena.
            switch (def.Id)
            {
                case "sentinela": return _punySoldierTex;
                case "dardo": return _punyArcherTex;
                case "faisca": return _punyMageTex;
                case "enxame": return _punyArcherPurpleTex;
                case "eco": return _punyMageRedTex;
                default: return null;
            }
        }

        private void DrawPunySprite(Unit u, Texture2D tex, int attackRow, Color team, int size)
        {
            if (tex == null) return;

            Vector2 f = u.Facing;
            if (f.LengthSquared() < 0.001f) f = u.IsEnemy ? new Vector2(0, 1) : new Vector2(0, -1);
            f.Normalize();
            int dirGroup = PunyDirGroup(f);

            int row;
            int frame;
            if (u.AttackAnim > 0f)
            {
                // AttackAnim conta de 0.14 -> 0; progresso 0 -> 1 nas 3 frames do ataque.
                float p = 1f - MathHelper.Clamp(u.AttackAnim / 0.14f, 0f, 1f);
                frame = Math.Min(2, (int)(p * 3f));
                row = attackRow;
            }
            else if (u.Def.Speed > 0.01f)
            {
                frame = Math.Abs((int)(u.WalkPhase / 1.5f)) % 3;
                row = 1; // walk
            }
            else
            {
                frame = 1;
                row = 0; // idle
            }

            // Mesmo grupo de direção para idle, walk e ataque -> nunca olha pra um lado e bate pro outro.
            var src = new Rectangle((dirGroup * 3 + frame) * PunyCell, row * PunyCell, PunyCell, PunyCell);
            float bob = row == 1 ? (float)Math.Sin(u.WalkPhase) * 1.2f : 0f;
            var dst = new Rectangle((int)(u.Pos.X - size / 2f), (int)(u.Pos.Y - size / 2f + bob - 2), size, size);
            DrawRect(new Rectangle(dst.X + 6, dst.Bottom - 5, dst.Width - 12, 4), new Color(0, 0, 0, 45));

            Color tint = Color.Lerp(Color.White, team, 0.08f);
            if (u.HitFlash > 0) tint = Color.Lerp(tint, Color.White, 0.75f);
            _spriteBatch.Draw(tex, dst, src, tint);
        }

        private static int PunyDirGroup(Vector2 f)
        {
            // Screen Y aponta para baixo: 0=+X (Leste), 90=+Y (Sul/front), 180=-X (Oeste), 270=-Y (Norte/back).
            float deg = MathHelper.ToDegrees((float)Math.Atan2(f.Y, f.X));
            if (deg < 0) deg += 360f;
            int idx = (int)Math.Floor((deg + 22.5f) / 45f) % 8; // 0=E,1=SE,2=S,3=SW,4=W,5=NW,6=N,7=NE
            // Planilha Puny: 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW
            return (idx + 2) % 8;
        }

        private void DrawArcher(Unit u, Color team, Color dark)
        {
            // Fallback quando o spritesheet Puny não carregar.
            if (_archerBackTex != null)
            {
                float bob = (float)Math.Sin(u.WalkPhase) * 1.2f;
                int size = 28;
                var dst = new Rectangle((int)(u.Pos.X - size / 2f), (int)(u.Pos.Y - size / 2f + bob), size, size);
                DrawRect(new Rectangle(dst.X + 4, dst.Bottom - 3, dst.Width - 8, 3), new Color(0, 0, 0, 45));
                Color tint = Color.Lerp(Color.White, team, 0.15f);
                if (u.HitFlash > 0) tint = Color.Lerp(tint, Color.White, 0.7f);
                _spriteBatch.Draw(_archerBackTex, dst, tint);
                return;
            }

            DrawRanged(u, team, dark);
        }

        private void DrawColossus(Unit u, Color team, Color dark)
        {
            float bob = (float)Math.Sin(u.WalkPhase * 1.4f) * 2.0f;
            int x = (int)u.Pos.X;
            int y = (int)(u.Pos.Y + bob);
            // shadow
            DrawRect(new Rectangle(x - 14, y + 18, 28, 6), new Color(0, 0, 0, 60));
            // legs
            int step = (int)(Math.Sin(u.WalkPhase * 1.4f) * 2f);
            DrawRect(new Rectangle(x - 10, y + 8 + step, 7, 12), Color.Lerp(u.Def.Color, Color.Black, 0.35f));
            DrawRect(new Rectangle(x + 3, y + 8 - step, 7, 12), Color.Lerp(u.Def.Color, Color.Black, 0.35f));
            // body
            var body = new Rectangle(x - 16, y - 10, 32, 22);
            DrawRect(body, u.Def.Color);
            DrawRect(body, dark, 2);
            DrawRect(new Rectangle(body.X + 3, body.Y + 3, body.Width - 6, 6), new Color(255, 255, 255, 35));
            // shoulders
            DrawRect(new Rectangle(x - 20, y - 12, 8, 10), Color.Lerp(u.Def.Color, Color.Black, 0.15f));
            DrawRect(new Rectangle(x + 12, y - 12, 8, 10), Color.Lerp(u.Def.Color, Color.Black, 0.15f));
            DrawRect(new Rectangle(x - 20, y - 12, 8, 10), dark, 1);
            DrawRect(new Rectangle(x + 12, y - 12, 8, 10), dark, 1);
            // head
            DrawRect(new Rectangle(x - 7, y - 20, 14, 10), Color.Lerp(u.Def.Color, Color.White, 0.12f));
            DrawRect(new Rectangle(x - 7, y - 20, 14, 10), dark, 1);
            DrawRect(new Rectangle(x - 4, y - 17, 3, 3), team);
            DrawRect(new Rectangle(x + 1, y - 17, 3, 3), team);
            // chest core
            DrawRect(new Rectangle(x - 4, y - 2, 8, 6), team);
            if (u.AttackAnim > 0) DrawRect(new Rectangle(x - 18, y - 6, 36, 20), new Color(255, 255, 255, 40));
            if (u.HitFlash > 0) DrawRect(new Rectangle(x - 20, y - 20, 40, 44), new Color(255, 255, 255, 90));
        }

        private void DrawHpBar(int x, int y, int w, int h, int hp, int maxHp)
        {
            float pct = Math.Max(0, (float)hp / maxHp);
            DrawRect(new Rectangle(x, y, w, h), new Color(15, 15, 15, 190));
            DrawRect(new Rectangle(x, y, (int)(w * pct), h), pct > 0.35f ? new Color(120, 230, 110) : new Color(235, 90, 80));
        }

        private void DrawFx()
        {
            foreach (var f in _fx)
            {
                float a = MathHelper.Clamp(f.Life / f.MaxLife, 0f, 1f);
                if (a <= 0f) continue;
                int cx = (int)f.Pos.X;
                int cy = (int)f.Pos.Y;

                if (f.Kind == "damage")
                {
                    if (string.IsNullOrEmpty(f.Text)) continue;
                    int tx = cx - f.Text.Length * 4;
                    DrawPixelText(f.Text, tx + 1, cy + 1, 2, Color.Black * (a * 0.75f));
                    DrawPixelText(f.Text, tx, cy, 2, f.Color * a);
                    continue;
                }

                if (f.Kind == "spark" || f.Kind == "death")
                {
                    int s = Math.Max(2, (int)f.Size);
                    DrawRect(new Rectangle(cx - s / 2, cy - s / 2, s, s), f.Color * (a * 0.9f));
                    continue;
                }

                int size = Math.Max(4, (int)f.Size);
                var r = new Rectangle(cx - size / 2, cy - size / 2, size, size);

                if (f.Kind == "hit")
                {
                    int arm = size / 2;
                    int t = Math.Max(2, arm / 3);
                    DrawRect(new Rectangle(cx - arm, cy - t / 2, arm, t), f.Color * a);
                    DrawRect(new Rectangle(cx + t / 2, cy - t / 2, arm, t), f.Color * a);
                    DrawRect(new Rectangle(cx - t / 2, cy - arm, t, arm), f.Color * a);
                    DrawRect(new Rectangle(cx - t / 2, cy + t / 2, t, arm), f.Color * a);
                    DrawRect(new Rectangle(cx - 2, cy - 2, 4, 4), Color.White * a);
                    continue;
                }

                // spawn / death ring
                int d = size / 2;
                int k = Math.Max(3, size / 4);
                DrawRect(new Rectangle(cx - d, cy - d, k, k), f.Color * (a * 0.75f));
                DrawRect(new Rectangle(cx + d - k, cy - d, k, k), f.Color * (a * 0.75f));
                DrawRect(new Rectangle(cx - d, cy + d - k, k, k), f.Color * (a * 0.75f));
                DrawRect(new Rectangle(cx + d - k, cy + d - k, k, k), f.Color * (a * 0.75f));
            }
        }

        private void DrawHud()
        {
            int w = GraphicsDevice.Viewport.Width;
            int hudH = 94;

            // Window face + sunken/sunlit bevel
            DrawRect(new Rectangle(0, 0, w, hudH), Se98Face);
            DrawRect(new Rectangle(0, hudH - 2, w, 2), Se98Shadow);
            DrawRect(new Rectangle(0, 0, w, 1), Se98Light);
            DrawRect(new Rectangle(0, hudH - 3, w, 1), Se98Light);

            // Gradient title bar strip
            int titleH = 18;
            for (int x = 0; x < w; x += 2)
            {
                float t = x / (float)Math.Max(1, w - 1);
                DrawRect(new Rectangle(x, 0, 2, titleH), Color.Lerp(Se98TitleA, Se98TitleB, t));
            }
            DrawPixelText("LUMIAROYALE - ARENA", 8, 6, 2, Color.White);

            int sec = Math.Max(0, (int)Math.Ceiling(_timeLeft));
            string timeStr = (sec / 60) + ":" + (sec % 60).ToString("00");
            DrawBeveledPanel(new Rectangle(w / 2 - 52, 24, 104, 26), false);
            DrawPixelText(timeStr, w / 2 - 24, 30, 3, sec <= 30 ? new Color(200, 30, 30) : Se98Text);

            DrawBeveledPanel(new Rectangle(w - 96, 24, 88, 26), false);
            DrawPixelText(_playerCrowns + "-" + _enemyCrowns, w - 70, 30, 3, Se98Text);

            if (_matchState == "playing")
            {
                var pause = GetPauseButtonRect();
                DrawBeveledPanel(pause, true);
                DrawRect(new Rectangle(pause.X + 9, pause.Y + 8, 4, 14), Se98Text);
                DrawRect(new Rectangle(pause.X + 19, pause.Y + 8, 4, 14), Se98Text);
            }

            // Elixir / mana bar as a classic progress bar
            int barX = 10;
            int barW = w - 64;
            int barY = 58;
            int barH = 26;
            var bg = new Rectangle(barX, barY, barW, barH);
            DrawBeveledPanel(bg, true);
            var inner = new Rectangle(bg.X + 3, bg.Y + 3, bg.Width - 6, bg.Height - 6);
            DrawRect(inner, new Color(255, 255, 255));
            float pct = _mana / ManaMax;
            var fg = new Rectangle(inner.X, inner.Y, (int)(inner.Width * pct), inner.Height);
            for (int i = 0; i < fg.Width; i += 4)
            {
                Color c = new Color(150 + (i % 8) * 6, 70 + (i % 8) * 4, 220 - (i % 8) * 6);
                DrawRect(new Rectangle(inner.X + i, inner.Y, Math.Min(4, fg.Width - i), fg.Height), i < fg.Width ? c : Color.White);
            }
            for (int i = 1; i < 10; i++)
            {
                int x = bg.X + (int)(barW * (i / 10f));
                DrawRect(new Rectangle(x, bg.Y + 3, 1, barH - 6), Se98Shadow);
            }
            DrawPixelText(((int)_mana) + "/10", barX + 8, barY + 6, 3, Se98Text);

            string msg = _statusTimer > 0 ? _status : (_selectedHand >= 0 ? "TOQUE NA ARENA" : "ESCOLHA UMA CARTA");
            DrawPixelText(msg.ToUpperInvariant(), 10, 84, 2, Se98Text);
        }

        private void DrawCards()
        {
            var rects = GetCardRects();
            for (int i = 0; i < 4; i++)
            {
                var r = rects[i];
                var def = _hand[i];
                bool sel = i == _selectedHand;
                bool canAfford = def != null && _mana >= def.Cost;

                Color bgTop = sel ? new Color(70, 78, 95) : new Color(44, 50, 62);
                Color bgBot = sel ? new Color(45, 52, 66) : new Color(28, 32, 42);
                if (!canAfford) { bgTop = new Color(36, 38, 44); bgBot = new Color(24, 26, 30); }
                DrawRect(r, bgBot);
                DrawRect(new Rectangle(r.X, r.Y, r.Width, r.Height / 2), bgTop);
                DrawRect(r, sel ? new Color(255, 220, 90) : new Color(95, 105, 120), sel ? 3 : 2);
                if (sel) DrawRect(new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6), new Color(255, 220, 90, 35), 1);

                if (def == null) continue;

                var art = new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, r.Height - 42);
                DrawRect(art, Color.Lerp(def.Color, Color.Black, 0.35f));
                DrawRect(new Rectangle(art.X, art.Y, art.Width, 10), new Color(255, 255, 255, 22));
                DrawBeveledPanel(art, true);

                // 4 px de margem nas bordas da área da foto; tamanho limitado pela altura (Y).
                int margin = 4;
                int portraitSize = Math.Max(8, Math.Min(art.Height - margin * 2, art.Width - margin * 2));
                var prev = new Rectangle(art.Center.X - portraitSize / 2, art.Center.Y - portraitSize / 2, portraitSize, portraitSize);
                Texture2D portraitTex = GetCardPortraitTexture(def);
                if (portraitTex != null)
                {
                    // Frame idle de frente: direção S (grupo 4), frame do meio.
                    // Colosso e Torreta têm sprites próprios (uma única pose de frente).
                    var src = (def.Id == "pulso" || def.Id == "torreta")
                        ? new Rectangle(0, 0, PunyCell, PunyCell)
                        : new Rectangle((PunyFrontDir * 3 + 1) * PunyCell, 0, PunyCell, PunyCell);
                    _spriteBatch.Draw(portraitTex, prev, src, canAfford ? Color.White : new Color(150, 150, 150));
                }
                else if (def.Kind == CardKind.Spell)
                {
                    DrawRect(new Rectangle(prev.X - 2, prev.Y - 2, prev.Width + 4, prev.Height + 4), new Color(255, 255, 255, 20), 1);
                    DrawRect(new Rectangle(prev.Center.X - 6, prev.Center.Y - 6, 12, 12), def.Color);
                    DrawRect(new Rectangle(prev.Center.X - 2, prev.Center.Y - 10, 4, 20), Color.White * 0.3f);
                    DrawRect(new Rectangle(prev.Center.X - 10, prev.Center.Y - 2, 20, 4), Color.White * 0.3f);
                }
                else if (def.Kind == CardKind.Building)
                {
                    DrawRect(new Rectangle(prev.X, prev.Bottom - 4, prev.Width, 4), new Color(80, 80, 90));
                    DrawRect(prev, new Color(150, 155, 165));
                    DrawRect(new Rectangle(prev.X + 4, prev.Y + 4, prev.Width - 8, 6), def.Color);
                    DrawRect(prev, Color.White * 0.5f, 1);
                }
                else if (def.SpawnCount > 1)
                {
                    for (int k = 0; k < Math.Min(4, def.SpawnCount); k++)
                    {
                        int px = prev.X + 1 + (k % 2) * 11;
                        int py = prev.Y + 1 + (k / 2) * 11;
                        DrawRect(new Rectangle(px, py, 8, 8), def.Color);
                        DrawRect(new Rectangle(px, py, 8, 8), Color.White * 0.45f, 1);
                    }
                }
                else
                {
                    DrawRect(new Rectangle(prev.X + 2, prev.Bottom, prev.Width - 4, 3), new Color(0, 0, 0, 50));
                    DrawRect(prev, def.Color);
                    DrawRect(new Rectangle(prev.X + 3, prev.Y + 3, prev.Width - 6, 5), Color.White * 0.2f);
                    DrawRect(prev, Color.White * 0.5f, 1);
                    if (def.Kind == CardKind.Ranged)
                        DrawRect(new Rectangle(prev.Center.X - 2, prev.Center.Y - 2, 4, 4), new Color(255, 255, 255, 220));
                }

                var costR = new Rectangle(r.X + 6, r.Bottom - 30, 30, 22);
                DrawBeveledPanel(costR, false);
                DrawPixelText(def.Cost.ToString(), costR.X + 9, costR.Y + 5, 2, Se98Text);

                var nameR = new Rectangle(r.X + 40, r.Bottom - 30, r.Width - 46, 22);
                DrawBeveledPanel(nameR, true);
                DrawRect(new Rectangle(nameR.X + 2, nameR.Y + 2, nameR.Width - 4, nameR.Height - 4), canAfford ? Se98Selected : new Color(60, 64, 74));
                DrawPixelText(def.Name.ToUpperInvariant(), nameR.X + 4, nameR.Y + 6, 2, canAfford ? Color.White : new Color(200, 200, 200));
                if (!canAfford) DrawRect(r, new Color(0, 0, 0, 25));
            }
        }

        private Texture2D GetCardPortraitTexture(CardDef def)
        {
            switch (def.Id)
            {
                case "sentinela": return _punySoldierTex;
                case "dardo": return _punyArcherTex;
                case "pulso": return _colossoTex;
                case "torreta": return _torretaTex;
                case "faisca": return _punyMageTex;
                case "enxame": return _punyArcherPurpleTex;
                case "eco": return _punyMageRedTex;
                default: return null;
            }
        }

        private void DrawCostPips(Rectangle costR, int cost)
        {
            DrawPixelText(cost.ToString(), costR.X + 8, costR.Y + 4, 2, Color.White);
        }

        private void DrawMiniLabel(string text, int x, int y, int pixel, Color c)
        {
            if (text == null) return;
            if (text.Length > 8) text = text.Substring(0, 8);
            DrawPixelText(text, x, y, 1, c);
        }

        private void DrawStatusText(string text, int x, int y, int pixel, Color c)
        {
            DrawPixelText(text, x, y, 2, c);
        }

        private void DrawPixelText(string text, int x, int y, int scale, Color c)
        {
            if (string.IsNullOrEmpty(text)) return;
            scale = Math.Max(1, scale);
            int cx = x;
            foreach (char raw in text.ToUpperInvariant())
            {
                if (raw == '\n') { cx = x; y += (5 * scale) + (2 * scale); continue; }
                if (!TryGetGlyph(raw, out var rows)) { cx += 4 * scale; continue; }
                for (int ry = 0; ry < 5; ry++)
                {
                    int row = rows[ry];
                    for (int rx = 0; rx < 3; rx++)
                    {
                        if (((row >> (2 - rx)) & 1) == 1)
                            DrawRect(new Rectangle(cx + rx * scale, y + ry * scale, scale, scale), c);
                    }
                }
                cx += 4 * scale;
            }
        }

        private static bool TryGetGlyph(char ch, out int[] rows)
        {
            // 3x5 glyphs, each row 3 bits (bit2 left)
            switch (ch)
            {
                case '0': rows = new[] { 7, 5, 5, 5, 7 }; return true;
                case '1': rows = new[] { 2, 6, 2, 2, 7 }; return true;
                case '2': rows = new[] { 7, 1, 7, 4, 7 }; return true;
                case '3': rows = new[] { 7, 1, 7, 1, 7 }; return true;
                case '4': rows = new[] { 5, 5, 7, 1, 1 }; return true;
                case '5': rows = new[] { 7, 4, 7, 1, 7 }; return true;
                case '6': rows = new[] { 7, 4, 7, 5, 7 }; return true;
                case '7': rows = new[] { 7, 1, 1, 1, 1 }; return true;
                case '8': rows = new[] { 7, 5, 7, 5, 7 }; return true;
                case '9': rows = new[] { 7, 5, 7, 1, 7 }; return true;
                case 'A': rows = new[] { 2, 5, 7, 5, 5 }; return true;
                case 'B': rows = new[] { 6, 5, 6, 5, 6 }; return true;
                case 'C': rows = new[] { 3, 4, 4, 4, 3 }; return true;
                case 'D': rows = new[] { 6, 5, 5, 5, 6 }; return true;
                case 'E': rows = new[] { 7, 4, 6, 4, 7 }; return true;
                case 'F': rows = new[] { 7, 4, 6, 4, 4 }; return true;
                case 'G': rows = new[] { 3, 4, 5, 5, 3 }; return true;
                case 'H': rows = new[] { 5, 5, 7, 5, 5 }; return true;
                case 'I': rows = new[] { 7, 2, 2, 2, 7 }; return true;
                case 'J': rows = new[] { 1, 1, 1, 5, 2 }; return true;
                case 'K': rows = new[] { 5, 5, 6, 5, 5 }; return true;
                case 'L': rows = new[] { 4, 4, 4, 4, 7 }; return true;
                case 'M': rows = new[] { 5, 7, 7, 5, 5 }; return true;
                case 'N': rows = new[] { 5, 7, 7, 7, 5 }; return true;
                case 'O': rows = new[] { 2, 5, 5, 5, 2 }; return true;
                case 'P': rows = new[] { 6, 5, 6, 4, 4 }; return true;
                case 'Q': rows = new[] { 2, 5, 5, 7, 3 }; return true;
                case 'R': rows = new[] { 6, 5, 6, 5, 5 }; return true;
                case 'S': rows = new[] { 3, 4, 2, 1, 6 }; return true;
                case 'T': rows = new[] { 7, 2, 2, 2, 2 }; return true;
                case 'U': rows = new[] { 5, 5, 5, 5, 7 }; return true;
                case 'V': rows = new[] { 5, 5, 5, 5, 2 }; return true;
                case 'W': rows = new[] { 5, 5, 7, 7, 5 }; return true;
                case 'X': rows = new[] { 5, 5, 2, 5, 5 }; return true;
                case 'Y': rows = new[] { 5, 5, 2, 2, 2 }; return true;
                case 'Z': rows = new[] { 7, 1, 2, 4, 7 }; return true;
                case ' ': rows = new[] { 0, 0, 0, 0, 0 }; return true;
                case '-': rows = new[] { 0, 0, 7, 0, 0 }; return true;
                case ':': rows = new[] { 0, 2, 0, 2, 0 }; return true;
                case '!': rows = new[] { 2, 2, 2, 0, 2 }; return true;
                case '/': rows = new[] { 1, 1, 2, 4, 4 }; return true;
                case '.': rows = new[] { 0, 0, 0, 0, 2 }; return true;
                case '(': rows = new[] { 1, 2, 2, 2, 1 }; return true;
                case ')': rows = new[] { 4, 2, 2, 2, 4 }; return true;
                default: rows = new[] { 7, 5, 5, 5, 7 }; return true;
            }
        }

        // Entrada: fundo Void, logotipo SE98 deslizando e barra carregando até a home.
        private void DrawIntro()
        {
            int vw = GraphicsDevice.Viewport.Width;
            int vh = GraphicsDevice.Viewport.Height;
            float t = Math.Max(0f, _introTime);

            if (_homeBgTex != null)
                _spriteBatch.Draw(_homeBgTex, new Rectangle(0, 0, vw, vh), new Color(255, 255, 255, 170));
            else DrawVoidGrid(vw, vh);
            DrawRect(new Rectangle(0, 0, vw, vh), new Color(8, 12, 26, 110));

            int barW = Math.Min(vw - 60, 400);
            int barX = vw / 2 - barW / 2;
            int barY = vh / 2 + 46;

            float fade = Clamp01(t / 0.45f);
            float titleIn = Clamp01((t - 0.25f) / 0.75f);
            float subIn = Clamp01((t - 0.9f) / 0.6f);

            for (int x = barX; x < barX + barW; x += 2)
            {
                float g = (x - barX) / (float)barW;
                DrawRect(new Rectangle(x, barY - 44, 2, 30), Color.Lerp(Se98TitleA, Se98TitleB, g) * fade);
            }

            int slide = (int)((1f - titleIn) * 48f);
            DrawPixelText("LUMIAROYALE", vw / 2 - 11 * 2 * 4 + slide, barY - 36, 4, Color.White * fade);
            DrawPixelText("ARENA DE CARTAS", vw / 2 - 15 * 2 * 3, barY - 6, 3, new Color(161, 196, 233) * subIn);
            DrawPixelText("VOID / SE98", vw / 2 - 11 * 2 * 2, barY + 14, 2, new Color(120, 150, 210) * subIn);

            DrawBeveledPanel(new Rectangle(barX, barY + 34, barW, 14), true);
            int fill = (int)(barW * Clamp01(t / 2.6f));
            for (int x = barX + 2; x < barX + 2 + fill && x < barX + barW - 2; x += 2)
                DrawRect(new Rectangle(x, barY + 36, 2, 10), Se98Selected);

            if (t > 1.4f)
                DrawPixelText("TOQUE PARA PULAR", vw / 2 - 16 * 2 * 2, barY + 58, 2, Se98Face * 0.7f);
        }

        private void DrawVoidGrid(int w, int h)
        {
            for (int x = 0; x < w; x += 24)
                DrawRect(new Rectangle(x, 0, 1, h), HomeGrid);
            for (int y = 0; y < h; y += 24)
                DrawRect(new Rectangle(0, y, w, 1), HomeGrid);
        }

        // Home: aba Batalha com arena quadrada e aba Cartas com painel lateral.
        private void DrawHome()
        {
            int vw = GraphicsDevice.Viewport.Width;
            int vh = GraphicsDevice.Viewport.Height;

            if (_homeBgTex != null)
                _spriteBatch.Draw(_homeBgTex, new Rectangle(0, 0, vw, vh), Color.White);
            else DrawVoidGrid(vw, vh);
            DrawRect(new Rectangle(0, 0, vw, vh), new Color(8, 12, 26, 70));

            DrawHomeTopBar();
            DrawHomeBanner();
            DrawHomeArena();
            DrawHomeControls();
            DrawHomeChests();
            if (_cardsSlide > 0.01f)
                DrawRect(new Rectangle(0, 0, vw, vh), new Color(0, 0, 0, (int)(120 * _cardsSlide)));
            DrawHomeNav();

            if (_cardsSlide > 0.01f) DrawHomeCardsPanel();

            if (_homeToastTimer > 0f && !string.IsNullOrEmpty(_homeToast))
            {
                var tr = new Rectangle(vw / 2 - 220, GetNavRect().Y - 40, 440, 30);
                DrawRect(tr, new Color(8, 12, 26, 235));
                DrawBeveledPanel(tr, true);
                DrawPixelText(_homeToast, tr.Center.X - _homeToast.Length * 6, tr.Center.Y - 7, 3,
                              new Color(255, 230, 150));
            }
        }

        private void DrawHomeBanner()
        {
            var b = GetHomeBannerRect();
            if (_homeBannerTex != null)
            {
                // Sombra acompanha o corte da bandeira.
                _spriteBatch.Draw(_homeBannerTex, new Rectangle(b.X + 5, b.Y + 5, b.Width, b.Height),
                                  new Color(0, 0, 0, 150));
                _spriteBatch.Draw(_homeBannerTex, b, Color.White);
            }
            else
            {
                DrawRect(b, new Color(20, 34, 70));
                DrawRect(b, new Color(120, 160, 220), 2);
            }

            var nick = GetHomeNickRect();
            DrawRect(new Rectangle(nick.X, nick.Y, nick.Width, nick.Height), new Color(8, 12, 26, 200));
            DrawBeveledPanel(nick, true);
            DrawPixelText("UAITI", nick.X + 16, nick.Y + 10, 3, Color.White);
            DrawPixelText("COROAS " + _crowns, nick.Right - 16 - 11 * 6, nick.Y + 10, 3,
                          new Color(255, 220, 130));
        }

        private void DrawHomeControls()
        {
            DrawBeveledButton(GetHomeExitRect(), "SAIR");
            DrawBeveledButton(GetHomePlayRect(), "JOGAR");
            DrawBeveledButton(GetHomeDifficultyRect(), "DIF " + GetDifficultyName(), true, 2);
        }

        private void DrawHomeNav()
        {
            var n = GetNavRect();
            DrawBeveledPanel(n, false);
            DrawRect(new Rectangle(n.X + 2, n.Y + 2, n.Width - 4, n.Height - 4), new Color(8, 12, 26));
            DrawBeveledButton(GetNavCardsRect(), "CARTAS", _homeTab == 1);
            DrawBeveledButton(GetNavBattleRect(), "BATALHA", _homeTab == 0);
        }

        private void DrawHomeArena()
        {
            var r = GetHomeArenaRect();
            DrawRect(r, new Color(6, 18, 36));
            if (_arenaTex != null)
                _spriteBatch.Draw(_arenaTex, r, Color.White * 0.5f);
            DrawRect(r, new Color(8, 12, 26, 100), 3);

            int midY = r.Center.Y;
            var river = new Rectangle(r.X, midY - 13, r.Width, 26);
            DrawRect(river, new Color(52, 120, 190, 205));
            int drift = (int)(_animTime * 16f) % 24;
            for (int x = r.X - 24 + drift; x < r.Right; x += 24)
            {
                int oy = (int)(Math.Sin(x * 0.05f + _animTime * 2f) * 2f);
                DrawRect(new Rectangle(x, river.Y + 7 + oy, 12, 2), new Color(210, 238, 255, 120));
            }

            int lx = r.X + r.Width / 4;
            int rx = r.X + 3 * r.Width / 4;
            DrawHomeBridge(lx, midY);
            DrawHomeBridge(rx, midY);

            DrawHomeTower(new Vector2(lx, r.Y + 66), true, false);
            DrawHomeTower(new Vector2(rx, r.Y + 66), true, false);
            DrawHomeTower(new Vector2(r.Center.X, r.Y + 30), true, true);
            DrawHomeTower(new Vector2(lx, r.Bottom - 66), false, false);
            DrawHomeTower(new Vector2(rx, r.Bottom - 66), false, false);
            DrawHomeTower(new Vector2(r.Center.X, r.Bottom - 30), false, true);

            // Confronto rápido azul x vermelho, em loop de 2 segundos.
            float t = (_animTime * 0.5f) % 1f;
            float approach = MathHelper.Clamp(t / 0.45f, 0f, 1f);
            bool clash = t >= 0.42f && t < 0.78f;
            float blueX = MathHelper.Lerp(r.X + 34f, r.Center.X - 16f, approach);
            float redX = MathHelper.Lerp(r.Right - 34f, r.Center.X + 16f, approach);
            if (t >= 0.78f)
            {
                float back = (t - 0.78f) / 0.22f;
                redX = MathHelper.Lerp(r.Center.X + 16f, r.Right - 34f, back);
                blueX = MathHelper.Lerp(r.Center.X - 16f, r.Center.X - 36f, back);
            }

            int blueDir = PunyDirGroup(new Vector2(1f, 0f));
            int redDir = PunyDirGroup(new Vector2(-1f, 0f));
            int walkFrame = (int)(_animTime * 9f) % 3;
            int atkFrame = Math.Min(2, (int)((t - 0.42f) / 0.06f) % 3);
            int row = clash ? 2 : 1;
            int frame = clash ? atkFrame : walkFrame;

            for (int i = 0; i < 2; i++)
            {
                int y = midY - 30 - i * 16;
                DrawHomeDemoUnit(_punySoldierTex, (int)blueX, y, blueDir, row, frame, 30, new Color(120, 180, 255));
                DrawHomeDemoUnit(_punyWarriorTex, (int)redX, y, redDir, row, frame, 30, new Color(255, 130, 120));
            }

            if (clash)
            {
                int cx = (int)((blueX + redX) / 2f);
                int cy = midY - 38;
                int j = (int)(Math.Sin(_animTime * 22f) * 3f);
                for (int k = 0; k < 4; k++)
                {
                    Color sc = k % 2 == 0 ? new Color(255, 225, 120) : new Color(255, 255, 255);
                    DrawRect(new Rectangle(cx - 8 + k * 5 - j, cy - 8 + (k % 2) * 12, 4, 4), sc * 0.9f);
                }
            }

            var plaque = new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, HY(24));
            DrawRect(plaque, new Color(6, 10, 22, 225));
            DrawRect(plaque, new Color(120, 160, 220), 1);
            DrawPixelText("ARENA 1", r.Center.X - 28, r.Y + HY(10), 2, Color.White);
        }

        private void DrawHomeDemoUnit(Texture2D tex, int x, int y, int dir, int row, int frame, int size, Color team)
        {
            var dst = new Rectangle(x - size / 2, y - size / 2, size, size);
            DrawRect(new Rectangle(dst.X + 4, dst.Bottom - 3, dst.Width - 8, 3), new Color(0, 0, 0, 55));
            if (tex != null)
            {
                var src = new Rectangle((dir * 3 + frame) * PunyCell, row * PunyCell, PunyCell, PunyCell);
                _spriteBatch.Draw(tex, dst, src, Color.Lerp(Color.White, team, 0.12f));
            }
            else DrawRect(dst, team);
        }

        private void DrawHomeBridge(int cx, int cy)
        {
            var b = new Rectangle(cx - 22, cy - 18, 44, 36);
            DrawRect(b, new Color(132, 102, 74));
            DrawRect(b, new Color(88, 64, 44), 2);
            for (int y = b.Y + 4; y < b.Bottom - 3; y += 6)
                DrawRect(new Rectangle(b.X + 3, y, b.Width - 6, 2), new Color(96, 70, 48, 170));
        }

        private void DrawHomeTower(Vector2 pos, bool enemy, bool main)
        {
            int w = main ? 30 : 22;
            int h = main ? 42 : 32;
            var b = new Rectangle((int)pos.X - w / 2, (int)pos.Y - h / 2, w, h);
            Color face = enemy ? new Color(208, 74, 74) : new Color(74, 146, 226);
            Color dark = enemy ? new Color(120, 36, 36) : new Color(36, 78, 132);

            DrawRect(new Rectangle(b.X + 6, b.Bottom - 6, w - 12, 7), new Color(24, 32, 30, 120));
            DrawRect(b, dark);
            DrawRect(b, face, 2);
            DrawRect(new Rectangle(b.X + 3, b.Y + 3, w - 6, 4), new Color(255, 255, 255, 70));

            if (main)
            {
                int pulse = (int)(Math.Sin(_animTime * 5f) * 18f);
                Color crown = new Color(255, ClampByte(220 - pulse), 110, 230);
                DrawRect(new Rectangle(b.X + w / 2 - 9, b.Y - 6, 18, 4), crown);
                for (int i = -1; i <= 1; i++)
                    DrawRect(new Rectangle(b.X + w / 2 + i * 5 - 2, b.Y - 10, 4, 5), crown);
            }
        }

        private void DrawHomeTopBar()
        {
            var s = GetHomeTopBarRect();
            DrawBeveledPanel(s, false);
            DrawRect(new Rectangle(s.X + 2, s.Y + 2, s.Width - 4, s.Height - 4), new Color(8, 12, 26));

            DrawPixelText("NIV " + _towerLevel, s.X + HX(6), s.Y + HY(4), 2, Color.White);

            const int perLevel = 150;
            var bar = new Rectangle(s.X + HX(6), s.Y + HY(30), HX(250), HY(16));
            DrawBeveledPanel(bar, true);
            int fill = (int)((bar.Width - 4) * (_trophies % perLevel) / (float)perLevel);
            for (int x = bar.X + 2; x < bar.X + 2 + fill; x += 2)
                DrawRect(new Rectangle(x, bar.Y + 2, 2, bar.Height - 4), new Color(255, 200, 90));
            string xp = (_trophies % perLevel) + "/" + perLevel;
            DrawPixelText("XP " + xp, bar.X + 2, bar.Y + HY(20), 2, Se98Face);

            int ix = s.X + HX(272);
            DrawRect(new Rectangle(ix, s.Y + HY(10), HX(20), HX(20)), new Color(240, 200, 80));
            DrawRect(new Rectangle(ix, s.Y + HY(10), HX(20), HX(20)), new Color(120, 80, 20), 1);
            DrawPixelText(_coins.ToString(), ix + HX(26), s.Y + HY(12), 2, new Color(255, 230, 150));

            ix = s.X + HX(356);
            DrawRect(new Rectangle(ix, s.Y + HY(10), HX(18), HX(22)), new Color(120, 210, 250));
            DrawRect(new Rectangle(ix, s.Y + HY(10), HX(18), HX(22)), new Color(40, 90, 150), 1);
            DrawPixelText(_gems.ToString(), ix + HX(24), s.Y + HY(12), 2, new Color(190, 235, 255));

            DrawPixelText("TROFEUS " + _trophies + "  V" + _wins + " D" + _losses,
                          s.X + HX(6), s.Y + HY(58), 2, new Color(170, 195, 230));
        }

        private void DrawHomeChests()
        {
            for (int i = 0; i < 4; i++)
            {
                var c = GetHomeChestRect(i);
                int prog = _chestProgress[i];
                bool ready = IsChestReady(i);
                bool opened = _chestOpened[i];

                DrawBeveledPanel(c, false);
                DrawRect(new Rectangle(c.X + 2, c.Y + 2, c.Width - 4, c.Height - 4), new Color(10, 16, 32));
                DrawRect(new Rectangle(c.X + 2, c.Y + 2, c.Width - 4, HY(18)), new Color(8, 12, 26));
                DrawPixelText("BAU " + (i + 1), c.X + HX(8), c.Y + HY(5), 2, Se98Face);

                int bx = c.X + HX(12);
                int bw = c.Width - HX(24);
                var body = new Rectangle(bx, c.Y + HY(38), bw, HY(32));
                var lid = new Rectangle(bx, c.Y + HY(24), bw, HY(16));

                Color wood = opened ? new Color(70, 72, 78)
                           : ready ? new Color(196, 146, 74) : new Color(118, 88, 58);
                Color dark = opened ? new Color(40, 42, 48) : new Color(78, 54, 34);
                if (opened) lid.Y -= HY(8);

                if (opened)
                    DrawRect(new Rectangle(body.X, c.Y + HY(38), bw, HY(10)), new Color(20, 22, 28));

                DrawRect(body, dark);
                DrawRect(body, wood, 2);
                DrawRect(lid, dark);
                DrawRect(lid, wood, 2);
                DrawRect(new Rectangle(lid.X + 2, lid.Y + 2, lid.Width - 4, HY(3)),
                         new Color(255, 220, 150, 90));

                Color metal = opened ? new Color(96, 98, 104) : new Color(150, 156, 164);
                DrawRect(new Rectangle(bx + HX(14), body.Y, HX(8), body.Height), metal);
                DrawRect(new Rectangle(bx + bw - HX(22), body.Y, HX(8), body.Height), metal);
                DrawRect(new Rectangle(bx + HX(14), lid.Y, HX(8), lid.Height), metal);
                DrawRect(new Rectangle(bx + bw - HX(22), lid.Y, HX(8), lid.Height), metal);

                if (!opened)
                {
                    var lock = new Rectangle(c.Center.X - HX(8), c.Y + HY(42), HX(16), HX(16));
                    DrawRect(lock, new Color(240, 208, 112));
                    DrawRect(lock, new Color(90, 62, 24), 1);
                    DrawRect(new Rectangle(c.Center.X - HX(2), c.Y + HY(46), HX(4), HX(6)), new Color(70, 46, 20));
                }
                if (ready)
                {
                    int pulse = ClampByte((int)(Math.Sin(_animTime * 4f + i) * 20f));
                    DrawRect(new Rectangle(c.X + 4, c.Bottom - HY(3), c.Width - 8, HY(3)),
                             new Color(255, 230 - pulse, 120));
                }

                string label = opened ? "ABERTO" : ready ? "ABRIR" : prog + "/3 VITORIAS";
                int scale = label.Length > 8 ? 1 : 2;
                DrawPixelText(label, c.Center.X - label.Length * 2 * scale, c.Y + HY(72), scale,
                              ready ? new Color(255, 230, 150) : Se98Face);
            }
        }

        private void DrawHomeCardsPanel()
        {
            var p = GetCardsPanelRect();
            DrawBeveledPanel(p, false);
            DrawRect(new Rectangle(p.X + 2, p.Y + 2, p.Width - 4, p.Height - 4), new Color(8, 12, 26));
            for (int x = p.X + 2; x < p.Right - 2; x += 2)
                DrawRect(new Rectangle(x, p.Y + 2, 2, HY(30)),
                         Color.Lerp(Se98TitleA, Se98TitleB, (x - p.X) / (float)p.Width));
            DrawPixelText("CARTAS", p.X + HX(12), p.Y + HY(6), 3, Color.White);
            DrawPixelText("ARRASTE PARA FECHAR", p.Right - HX(92), p.Y + HY(12), 1,
                          new Color(150, 170, 205));

            for (int i = 0; i < _deck.Length; i++)
            {
                var c = GetHomeCardRect(i);
                var def = _deck[i];
                bool sel = _selectedDeckCard == i;
                DrawBeveledPanel(c, sel);
                DrawRect(new Rectangle(c.X + 2, c.Y + 2, c.Width - 4, c.Height - 4),
                         sel ? new Color(16, 32, 80) : new Color(18, 24, 42));
                DrawRect(new Rectangle(c.X + 2, c.Y + 2, c.Width - 4, HY(4)), def.Color);

                var tex = GetCardPortraitTexture(def);
                var portrait = new Rectangle(c.X + HX(8), c.Y + HY(12), HX(62), HY(62));
                if (tex != null)
                {
                    var src = new Rectangle((PunyFrontDir * 3 + 1) * PunyCell, 0, PunyCell, PunyCell);
                    _spriteBatch.Draw(tex, portrait, src, Color.White);
                }
                else
                {
                    DrawRect(portrait, def.Color);
                    DrawRect(portrait, Color.White * 0.4f, 2);
                }
                DrawRect(portrait, def.Color, 2);

                var cost = new Rectangle(c.Right - HX(32), c.Y + HY(6), HX(26), HX(26));
                DrawBeveledPanel(cost, false);
                DrawPixelText(def.Cost.ToString(), cost.Center.X - 4, cost.Center.Y - 7, 3, Se98Text);

                string name = def.Name.ToUpperInvariant();
                if (name.Length > 12) name = name.Substring(0, 12);
                DrawPixelText(name, c.X + HX(76), c.Y + HY(14), 2, Color.White);
                DrawPixelText(def.Kind.ToString().ToUpperInvariant(), c.X + HX(76), c.Y + HY(36), 2,
                              new Color(180, 200, 230));
                DrawPixelText("DMG " + def.Damage + "  VIDA " + def.Hp, c.X + HX(8), c.Y + HY(84), 2,
                              Se98Face);
            }

            var d = new Rectangle(p.X + HX(12), p.Y + HY(506), p.Width - HX(24), HY(118));
            DrawBeveledPanel(d, true);
            DrawRect(new Rectangle(d.X + 2, d.Y + 2, d.Width - 4, d.Height - 4), new Color(12, 18, 36));
            if (_selectedDeckCard >= 0 && _selectedDeckCard < _deck.Length)
            {
                var def = _deck[_selectedDeckCard];
                DrawPixelText(def.Name.ToUpperInvariant(), d.X + HX(10), d.Y + HY(8), 3, Color.White);
                DrawPixelText("CUSTO " + def.Cost + "  DMG " + def.Damage + "  VIDA " + def.Hp,
                              d.X + HX(10), d.Y + HY(38), 2, new Color(255, 220, 130));
                DrawPixelText("ALCANCE " + (int)def.Range + "  VEL " + (int)def.Speed,
                              d.X + HX(10), d.Y + HY(62), 2, Se98Face);
                DrawPixelText(def.Kind.ToString().ToUpperInvariant(), d.X + HX(10), d.Y + HY(86), 2,
                              new Color(180, 200, 230));
            }
            else
            {
                DrawPixelText("TOQUE EM UMA CARTA", d.X + HX(10), d.Y + HY(12), 2, Se98Face);
                DrawPixelText("VEJA CUSTO, DANO E VIDA", d.X + HX(10), d.Y + HY(40), 2,
                              new Color(150, 170, 205));
            }
        }

        private void DrawEndOverlay()
        {
            if (_matchState == "playing") return;
            DrawRect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 150));

            if (_matchState == "paused")
            {
                var box = GetPauseBoxRect();
                DrawBeveledPanel(box, false);
                for (int x = box.X + 2; x < box.Right - 2; x += 2)
                    DrawRect(new Rectangle(x, box.Y + 2, 2, 26), Color.Lerp(Se98TitleA, Se98TitleB, (x - box.X) / (float)box.Width));
                DrawPixelText("PAUSADO", box.X + 96, box.Y + 8, 4, Color.White);

                DrawBeveledButton(GetPauseResumeRect(), "RETOMAR");
                DrawBeveledButton(GetPauseRestartRect(), "REINICIAR");
                return;
            }

            bool win = _matchState == "win";
            var ebox = GetEndBoxRect();
            DrawBeveledPanel(ebox, false);
            for (int x = ebox.X + 2; x < ebox.Right - 2; x += 2)
                DrawRect(new Rectangle(x, ebox.Y + 2, 2, 32), Color.Lerp(Se98TitleA, Se98TitleB, (x - ebox.X) / (float)ebox.Width));

            string title = win ? "VITORIA!" : "DERROTA";
            DrawPixelText(title, ebox.X + (win ? 122 : 132), ebox.Y + 10, 4, Color.White);
            if (!string.IsNullOrEmpty(_status))
                DrawPixelText(_status, ebox.X + (360 - _status.Length * 8) / 2, ebox.Y + 36, 2, Se98Shadow);
            DrawPixelText("COROAS " + _playerCrowns + " X " + _enemyCrowns, ebox.X + 100, ebox.Y + 58, 3, Se98Text);
            DrawPixelText("TORRES " + _enemyTowersLost + " X " + _playerTowersLost, ebox.X + 100, ebox.Y + 80, 3, Se98Text);
            int sec = Math.Max(0, (int)Math.Ceiling(_timeLeft));
            DrawPixelText("TEMPO " + (sec / 60) + ":" + (sec % 60).ToString("00"), ebox.X + 112, ebox.Y + 102, 3, Se98Text);

            DrawBeveledButton(GetEndRematchRect(), "REVANCHE");
            DrawBeveledButton(GetEndMenuRect(), "MENU");
        }

        private void DrawBeveledPanel(Rectangle r, bool sunken)
        {
            if (sunken)
            {
                DrawRect(new Rectangle(r.X, r.Y, r.Width, 2), Se98Shadow);
                DrawRect(new Rectangle(r.X, r.Y, 2, r.Height), Se98Shadow);
                DrawRect(new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Se98Light);
                DrawRect(new Rectangle(r.Right - 2, r.Y, 2, r.Height), Se98Light);
            }
            else
            {
                DrawRect(new Rectangle(r.X, r.Y, r.Width, 2), Se98Light);
                DrawRect(new Rectangle(r.X, r.Y, 2, r.Height), Se98Light);
                DrawRect(new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Se98Shadow);
                DrawRect(new Rectangle(r.Right - 2, r.Y, 2, r.Height), Se98Shadow);
                DrawRect(new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), Se98Face);
            }
        }

        private void DrawBeveledButton(Rectangle r, string label, bool active = false, int scale = 3)
        {
            DrawBeveledPanel(r, false);
            int tx = r.Center.X - label.Length * 2 * scale;
            int ty = r.Center.Y - (5 * scale) / 2;
            if (active)
            {
                DrawRect(new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), Se98Selected);
                DrawPixelText(label, tx, ty, scale, Color.White);
            }
            else
            {
                DrawPixelText(label, tx, ty, scale, Se98Text);
            }
        }

        private void DrawRect(Rectangle r, Color c)
        {
            _spriteBatch.Draw(_pixel, r, c);
        }

        private void DrawRect(Rectangle r, Color c, int thickness)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, thickness), c);
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), c);
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, thickness, r.Height), c);
            _spriteBatch.Draw(_pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), c);
        }
    }
}
