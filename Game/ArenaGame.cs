using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using Windows.Graphics.Display;
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
        private string _matchState = ""; // "menu", "playing", "win", "lose"

        private Rectangle _arenaRect;
        private float _statusTimer;
        private float _timeLeft = 180f;
        private int _playerCrowns;
        private int _enemyCrowns;
        private int _playerTowersLost;
        private int _enemyTowersLost;

        private struct Fx
        {
            public Vector2 Pos;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
            public string Kind; // "hit", "death", "spawn"
        }

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
            TouchPanel.EnabledGestures = GestureType.Tap;
            base.Initialize();
            ResetMatch();
            _matchState = "menu";
            _status = "Toque para jogar";
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

            float leftLaneX = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float rightLaneX = _arenaRect.Left + _arenaRect.Width * 0.75f;
            float centerX = _arenaRect.Left + _arenaRect.Width * 0.5f;

            // Enemy towers (top): side towers forward (closer to river), king receded (closer to top)
            _towers.Add(new Tower(new Vector2(leftLaneX, arenaTop + 165), 900, false, true));
            _towers.Add(new Tower(new Vector2(rightLaneX, arenaTop + 165), 900, false, true));
            _towers.Add(new Tower(new Vector2(centerX, arenaTop + 80), 1400, true, true));
            // Player towers (bottom): side towers forward (closer to river), king receded (closer to bottom)
            _towers.Add(new Tower(new Vector2(leftLaneX, arenaBottom - 165), 900, false, false));
            _towers.Add(new Tower(new Vector2(rightLaneX, arenaBottom - 165), 900, false, false));
            _towers.Add(new Tower(new Vector2(centerX, arenaBottom - 80), 1400, true, false));
        }

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0.1f) dt = 0.1f;

            HandleInput();
            UpdateFx(dt);
            if (_statusTimer > 0) _statusTimer -= dt;

            if (_matchState != "playing") { base.Update(gameTime); return; }

            _timeLeft -= dt;
            if (_timeLeft <= 0f) { _timeLeft = 0f; EndByCrowns(); base.Update(gameTime); return; }

            float manaRate = _timeLeft <= 60f ? 0.85f : 0.55f;
            _mana = Math.Min(ManaMax, _mana + dt * manaRate);
            _enemyMana = Math.Min(ManaMax, _enemyMana + dt * manaRate);

            UpdateEnemy(dt);
            UpdateUnits(dt);
            UpdateTowers(dt);
            UpdateProjectiles(dt);

            for (int i = _units.Count - 1; i >= 0; i--)
            {
                if (_units[i].Alive) continue;
                var u = _units[i];
                AddFx(u.Pos, u.IsEnemy ? new Color(255, 120, 120) : new Color(120, 190, 255), u.IsBuilding ? 24f : 18f, 0.35f, "death");
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
        }

        private void UpdateFx(float dt)
        {
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                f.Life -= dt;
                f.Size += dt * (f.Kind == "hit" ? 18f : 34f);
                _fx[i] = f;
                if (f.Life <= 0) _fx.RemoveAt(i);
            }
            foreach (var u in _units)
            {
                if (u.HitFlash > 0) u.HitFlash -= dt;
                if (u.AttackAnim > 0) u.AttackAnim -= dt;
            }
            foreach (var t in _towers) if (t.HitFlash > 0) t.HitFlash -= dt;
        }

        private void AddFx(Vector2 pos, Color c, float size, float life, string kind)
        {
            _fx.Add(new Fx { Pos = pos, Color = c, Size = size, Life = life, MaxLife = life, Kind = kind });
        }

        private void CheckEnd()
        {
            bool playerMain = _towers.Any(t => !t.IsEnemy && t.IsMain && t.Alive);
            bool enemyMain = _towers.Any(t => t.IsEnemy && t.IsMain && t.Alive);
            if (!playerMain) { _matchState = "lose"; _status = "Derrota - toque para reiniciar"; }
            else if (!enemyMain) { _matchState = "win"; _status = "Vitoria! Toque para reiniciar"; }
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
                if (g.GestureType != GestureType.Tap) continue;
                var p = g.Position;

                if (_matchState == "menu" || _matchState == "win" || _matchState == "lose")
                {
                    ResetMatch();
                    return;
                }

                if (_matchState == "paused")
                {
                    int w = GraphicsDevice.Viewport.Width;
                    int h = GraphicsDevice.Viewport.Height;
                    var resume = new Rectangle(w / 2 - 110, h / 2 - 17, 220, 38);
                    var restart = new Rectangle(w / 2 - 110, h / 2 + 31, 220, 38);
                    if (resume.Contains((int)p.X, (int)p.Y)) { _matchState = "playing"; _status = "Retomado"; _statusTimer = 1f; }
                    else if (restart.Contains((int)p.X, (int)p.Y)) { ResetMatch(); }
                    return;
                }

                // pause button top-right HUD
                var pauseBtn = new Rectangle(GraphicsDevice.Viewport.Width - 42, 44, 32, 32);
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

            // After destroying an enemy side tower, allow deploying in that lane's pocket on opponent side
            float leftX = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float rightX = _arenaRect.Left + _arenaRect.Width * 0.75f;
            float laneX = Math.Abs(pos.X - leftX) <= Math.Abs(pos.X - rightX) ? leftX : rightX;
            var sideTower = _towers.FirstOrDefault(t => t.IsEnemy != enemy && !t.IsMain && Math.Abs(t.Pos.X - laneX) < 20f);
            if (sideTower != null && sideTower.Alive) return false;

            float pocketHalfW = _arenaRect.Width * 0.22f;
            if (Math.Abs(pos.X - laneX) > pocketHalfW) return false;

            if (enemy)
            {
                // enemy deploying into player's pocket (player side tower destroyed)
                float minY = _arenaRect.Bottom - _arenaRect.Height * 0.35f;
                return pos.Y >= minY;
            }
            else
            {
                // player deploying into enemy pocket
                float maxY = _arenaRect.Top + _arenaRect.Height * 0.35f;
                return pos.Y <= maxY;
            }
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
                if (Vector2.Distance(u.Pos, pos) <= card.AreaRadius + u.Radius) { u.Hp -= card.Damage; u.HitFlash = 0.15f; }
            }
            foreach (var t in _towers)
            {
                if (!t.Alive || t.IsEnemy == enemy) continue;
                float tr = t.IsMain ? 34f : 28f;
                if (Vector2.Distance(t.Pos, pos) <= card.AreaRadius + tr)
                {
                    t.Hp -= card.Damage;
                    t.HitFlash = 0.15f;
                    if (t.IsMain) t.Awake = true;
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
            _enemySpawnTimer = 1.8f + (float)_rng.NextDouble() * 2.0f;

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

            // If player side tower in chosen lane is down, sometimes deploy in pocket to pressure
            var oppSide = _towers.FirstOrDefault(t => !t.IsEnemy && !t.IsMain && Math.Abs(t.Pos.X - laneX) < 20f);
            bool pocketAvailable = oppSide != null && !oppSide.Alive;
            bool canPocket = pocketAvailable && card.Kind != CardKind.Building && _enemyMana >= 5f;
            if (canPocket && _rng.NextDouble() < 0.45)
            {
                float px = laneX + ((float)_rng.NextDouble() - 0.5f) * (_arenaRect.Width * 0.2f);
                float py = _arenaRect.Bottom - _arenaRect.Height * 0.28f + (float)_rng.NextDouble() * (_arenaRect.Height * 0.12f);
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
                    if (Vector2.Distance(u.Pos, hitPos) <= p.AreaRadius + u.Radius) { u.Hp -= p.Damage; u.HitFlash = 0.12f; }
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
                if (p.UnitTarget != null && p.UnitTarget.Alive && p.UnitTarget.IsEnemy == !p.ByPlayer && Vector2.Distance(p.UnitTarget.Pos, hitPos) < 80f)
                {
                    p.UnitTarget.Hp -= p.Damage; p.UnitTarget.HitFlash = 0.12f;
                }
                else if (p.TowerTarget != null && p.TowerTarget.Alive && p.TowerTarget.IsEnemy == !p.ByPlayer && Vector2.Distance(p.TowerTarget.Pos, hitPos) < 80f)
                {
                    DamageTower(p.TowerTarget, p.Damage, p.ByPlayer);
                }
                else
                {
                    // fallback: damage nearest enemy at hit
                    foreach (var u in _units)
                    {
                        if (!u.Alive || u.IsEnemy == !p.ByPlayer) continue;
                        if (Vector2.DistanceSquared(p.Pos, u.Pos) <= (u.Radius + p.Radius + 2f) * (u.Radius + p.Radius + 2f))
                        { u.Hp -= p.Damage; u.HitFlash = 0.12f; break; }
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

        private TargetInfo? FindTarget(Unit u)
        {
            TargetInfo? best = null;
            float bestScore = float.MaxValue;
            bool buildingOnly = u.Def.Id == "pulso"; // Colosso targets only buildings/towers

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
                        best = new TargetInfo { pos = o.Pos, radius = o.Radius, isTower = false, isMain = false, isEnemy = o.IsEnemy, unit = o, tower = null, applyDamage = dmg => { o.Hp -= dmg; o.HitFlash = 0.12f; } };
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
                if (score < bestStructureScore)
                {
                    bestStructureScore = score;
                    var o = other;
                    bestStructure = new TargetInfo { pos = o.Pos, radius = o.Radius, isTower = false, isMain = false, isEnemy = o.IsEnemy, unit = o, tower = null, applyDamage = dmg => { o.Hp -= dmg; o.HitFlash = 0.12f; } };
                }
            }

            foreach (var t in _towers)
            {
                if (t.IsEnemy == u.IsEnemy || !t.Alive) continue;
                float laneDist = Math.Abs(t.Pos.X - u.LaneX);
                float d = Vector2.Distance(u.Pos, t.Pos);
                float score = d + laneDist * 0.35f;
                if (!t.IsMain) score -= 60f; // side towers first
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
                bool leftAlive = _towers.Any(t => t.Alive && t.IsEnemy == target.isEnemy && !t.IsMain && Math.Abs(t.Pos.X - left) < 10f);
                bool rightAlive = _towers.Any(t => t.Alive && t.IsEnemy == target.isEnemy && !t.IsMain && Math.Abs(t.Pos.X - right) < 10f);
                if (leftAlive && !rightAlive) crossLane = left;
                else if (rightAlive && !leftAlive) crossLane = right;
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
                    target.Hp -= t.Damage;
                    target.HitFlash = 0.12f;
                    AddFx(Vector2.Lerp(t.Pos, target.Pos, 0.7f), new Color(255, 240, 160), 7f, 0.12f, "hit");
                    t.CooldownLeft = t.Cooldown;
                }
            }
        }

        private void DamageTower(Tower t, int dmg, bool byPlayer)
        {
            if (!t.Alive) return;
            bool wasAlive = t.Alive;
            t.Hp -= dmg;
            t.HitFlash = 0.12f;
            if (t.IsMain) t.Awake = true;
            if (wasAlive && !t.Alive && !t.IsMain)
            {
                AwardCrown(t.IsEnemy);
                // awaken king when a princess tower falls
                var king = _towers.FirstOrDefault(x => x.IsEnemy == t.IsEnemy && x.IsMain);
                if (king != null) king.Awake = true;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(26, 72, 44));

            _spriteBatch.Begin();
            DrawArena();
            DrawDeployHint();
            DrawTowers();
            DrawUnits();
            DrawFx();
            DrawHud();
            DrawCards();
            DrawEndOverlay();
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawArena()
        {
            DrawRect(_arenaRect, new Color(46, 112, 62));
            // subtle lane shading
            DrawRect(new Rectangle(_arenaRect.Left, _arenaRect.Top, _arenaRect.Width, _arenaRect.Height / 2), new Color(0, 0, 0, 18));
            DrawRect(new Rectangle(_arenaRect.Left + 8, _arenaRect.Top + 8, _arenaRect.Width - 16, _arenaRect.Height - 16), new Color(76, 146, 88), 2);

            var river = new Rectangle(_arenaRect.Left, _arenaRect.Center.Y - 14, _arenaRect.Width, 28);
            DrawRect(river, new Color(58, 132, 206));
            DrawRect(new Rectangle(river.X, river.Y + 4, river.Width, 3), new Color(120, 190, 240, 120));
            DrawRect(new Rectangle(river.X, river.Bottom - 8, river.Width, 3), new Color(120, 190, 240, 90));

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
            DrawRect(new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, 4), new Color(170, 138, 100));
            DrawRect(new Rectangle(r.X + 6, r.Bottom - 10, r.Width - 12, 4), new Color(170, 138, 100));
        }

        private void DrawDeployHint()
        {
            if (_selectedHand < 0 || _matchState != "playing") return;
            var card = _hand[_selectedHand];
            if (card.Kind == CardKind.Spell)
            {
                DrawRect(_arenaRect, new Color(255, 160, 80, 20));
                DrawRect(_arenaRect, new Color(255, 180, 120, 90), 2);
                return;
            }
            int midY = _arenaRect.Center.Y + 20;
            var allowed = new Rectangle(_arenaRect.X, midY, _arenaRect.Width, _arenaRect.Bottom - midY);
            DrawRect(allowed, new Color(120, 255, 160, 30));
            DrawRect(allowed, new Color(150, 255, 180, 120), 2);

            // pockets when enemy side towers are down
            foreach (var pocket in GetDeployPockets(false))
            {
                DrawRect(pocket, new Color(120, 255, 160, 35));
                DrawRect(pocket, new Color(180, 255, 200, 140), 2);
            }
        }

        private List<Rectangle> GetDeployPockets(bool enemyPlayer)
        {
            var pockets = new List<Rectangle>();
            float leftX = _arenaRect.Left + _arenaRect.Width * 0.25f;
            float rightX = _arenaRect.Left + _arenaRect.Width * 0.75f;
            float pocketHalfW = _arenaRect.Width * 0.22f;
            int pocketH = (int)(_arenaRect.Height * 0.35f);

            foreach (float laneX in new[] { leftX, rightX })
            {
                var sideTower = _towers.FirstOrDefault(t => t.IsEnemy != enemyPlayer && !t.IsMain && Math.Abs(t.Pos.X - laneX) < 20f);
                if (sideTower == null || sideTower.Alive) continue;
                int x = (int)(laneX - pocketHalfW);
                int w = (int)(pocketHalfW * 2);
                if (enemyPlayer)
                {
                    int y = _arenaRect.Bottom - pocketH;
                    pockets.Add(new Rectangle(x, y, w, pocketH));
                }
                else
                {
                    int y = _arenaRect.Top;
                    pockets.Add(new Rectangle(x, y, w, pocketH));
                }
            }
            return pockets;
        }

        private void DrawTowers()
        {
            foreach (var t in _towers)
            {
                if (!t.Alive) continue;
                Color team = t.IsEnemy ? new Color(208, 74, 74) : new Color(74, 146, 226);
                Color teamDark = t.IsEnemy ? new Color(120, 36, 36) : new Color(36, 78, 132);
                int size = t.IsMain ? 64 : 50;
                var body = new Rectangle((int)t.Pos.X - size / 2, (int)t.Pos.Y - size / 2, size, size);
                var baseR = new Rectangle(body.X - 6, body.Bottom - 10, body.Width + 12, 14);
                DrawRect(new Rectangle(baseR.X + 4, baseR.Y + 6, baseR.Width, baseR.Height), new Color(0, 0, 0, 45));
                DrawRect(baseR, new Color(88, 88, 96));
                DrawRect(body, new Color(165, 170, 180));
                DrawRect(body, teamDark, 2);
                var top = new Rectangle(body.X + 6, body.Y + 6, body.Width - 12, 12);
                DrawRect(top, team);
                DrawRect(new Rectangle(body.Center.X - 5, body.Y - 10, 10, 14), teamDark);
                if (t.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 120));

                float pct = Math.Max(0, (float)t.Hp / t.MaxHp);
                var hbg = new Rectangle(body.X - 8, body.Y - 16, body.Width + 16, 7);
                var hfg = new Rectangle(hbg.X, hbg.Y, (int)(hbg.Width * pct), hbg.Height);
                DrawRect(hbg, new Color(20, 20, 20, 200));
                DrawRect(hfg, pct > 0.35f ? new Color(110, 220, 90) : new Color(230, 90, 70));
                DrawRect(hbg, new Color(255, 255, 255, 70), 1);
            }
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

                if (u.Def.Id == "pulso") DrawColossus(u, team, dark);
                else if (u.Def.Id == "enxame") DrawSwarm(u, team, dark);
                else if (u.Def.Id == "eco") DrawEco(u, team, dark);
                else if (u.Def.Kind == CardKind.Ranged && u.Def.Id == "dardo") DrawArcher(u, team, dark);
                else if (u.Def.Kind == CardKind.Ranged && u.Def.Id == "faisca") DrawPunyMage(u, team, dark);
                else if (u.Def.Kind == CardKind.Ranged) DrawRanged(u, team, dark);
                else DrawMelee(u, team, dark);

                int hpW = u.Def.Id == "pulso" ? 40 : (u.Def.Id == "enxame" ? 18 : 28);
                int hpY = u.Def.Id == "pulso" ? 34 : (u.Def.Id == "enxame" ? 14 : 22);
                DrawHpBar((int)u.Pos.X - hpW / 2, (int)u.Pos.Y - hpY, hpW, 4, u.Hp, u.MaxHp);
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
            var b = new Rectangle((int)u.Pos.X - 14, (int)u.Pos.Y - 14, 28, 28);
            DrawRect(new Rectangle(b.X + 3, b.Bottom - 1, b.Width, 4), new Color(0, 0, 0, 50));
            DrawRect(b, new Color(140, 145, 155));
            DrawRect(b, dark, 2);
            DrawRect(new Rectangle(b.X + 4, b.Y + 4, b.Width - 8, 10), Color.Lerp(u.Def.Color, Color.Black, 0.2f));
            DrawRect(new Rectangle(b.Center.X - 3, b.Y - 6, 6, 8), team);
            if (u.AttackAnim > 0) DrawRect(new Rectangle(b.Center.X - 2, b.Y - 10, 4, 4), Color.White);
            if (u.HitFlash > 0) DrawRect(b, new Color(255, 255, 255, 110));
            DrawHpBar(b.X - 2, b.Y - 11, b.Width + 4, 4, u.Hp, u.MaxHp);
            float lt = u.Def.Lifetime > 0 ? MathHelper.Clamp(u.LifeLeft / u.Def.Lifetime, 0, 1) : 0;
            DrawRect(new Rectangle(b.X - 2, b.Bottom + 5, b.Width + 4, 3), new Color(20, 20, 20, 160));
            DrawRect(new Rectangle(b.X - 2, b.Bottom + 5, (int)((b.Width + 4) * lt), 3), new Color(200, 200, 255));
        }

        private void DrawMelee(Unit u, Color team, Color dark)
        {
            Texture2D tex = u.Def.Id == "sentinela" ? _punySoldierTex : (_punyWarriorTex ?? _punySoldierTex);
            if (tex != null && u.Def.Id != "pulso")
            {
                int anim = u.AttackAnim > 0 ? 2 : 1;
                if (u.Def.Speed <= 0.01f) anim = u.AttackAnim > 0 ? 2 : 0;
                DrawPunySprite(u, tex, anim, 3, 6f, team);
                return;
            }

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
            if (_punyMageTex != null)
            {
                int anim = u.AttackAnim > 0 ? 2 : 1;
                if (u.Def.Speed <= 0.01f) anim = u.AttackAnim > 0 ? 2 : 0;
                DrawPunySprite(u, _punyMageTex, anim, 3, 6f, team);
                return;
            }
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

        private void DrawPunySprite(Unit u, Texture2D tex, int animGroup, int frames, float fps, Color team)
        {
            // Puny Characters: 768x256, 32x32 cells. 24 cols = 8 groups * 3 frames, 8 rows = directions.
            // Row mapping assumed: 0=S,1=SW,2=W,3=NW,4=N,5=NE,6=E,7=SE (common Puny layout).
            if (tex == null) { DrawRanged(u, team, team); return; }

            Vector2 f = u.Facing;
            if (f.LengthSquared() < 0.001f) f = u.IsEnemy ? new Vector2(0, 1) : new Vector2(0, -1);
            f.Normalize();
            int dirRow = PunyDirRow(f);
            float t = animGroup == 1 ? u.WalkPhase * 0.9f : (animGroup == 2 ? (1f - u.AttackAnim * 3f) : 0f);
            int frame = Math.Abs((int)Math.Floor(t * fps)) % Math.Max(1, frames);
            int col = animGroup * 3 + frame;
            var src = new Rectangle(col * 32, dirRow * 32, 32, 32);

            float bob = animGroup == 1 ? (float)Math.Sin(u.WalkPhase) * 1.2f : 0f;
            int size = u.Def.Id == "pulso" ? 52 : 40;
            var dst = new Rectangle((int)(u.Pos.X - size / 2f), (int)(u.Pos.Y - size / 2f + bob - 2), size, size);
            DrawRect(new Rectangle(dst.X + 6, dst.Bottom - 5, dst.Width - 12, 4), new Color(0, 0, 0, 45));

            Color tint = Color.Lerp(Color.White, team, 0.08f);
            if (u.HitFlash > 0) tint = Color.Lerp(tint, Color.White, 0.75f);
            _spriteBatch.Draw(tex, dst, src, tint);
        }

        private static int PunyDirRow(Vector2 f)
        {
            // 8 directions: angle 0 = +X (east), CCW positive in math, but screen Y down.
            float ang = (float)Math.Atan2(f.Y, f.X); // -PI..PI
            float deg = MathHelper.ToDegrees(ang);
            if (deg < 0) deg += 360f;
            // sectors centered at E(0), SE(45), S(90), SW(135), W(180), NW(225), N(270), NE(315)
            int idx = (int)Math.Floor((deg + 22.5f) / 45f) % 8;
            // idx: 0=E,1=SE,2=S,3=SW,4=W,5=NW,6=N,7=NE
            switch (idx)
            {
                case 2: return 0; // S
                case 3: return 1; // SW
                case 4: return 2; // W
                case 5: return 3; // NW
                case 6: return 4; // N
                case 7: return 5; // NE
                case 0: return 6; // E
                case 1: return 7; // SE
                default: return 0;
            }
        }

        private void DrawArcher(Unit u, Color team, Color dark)
        {
            // Use Puny Archer if available; anim groups: 0 idle,1 walk,2 bow attack,3 bow? varies - use 1 for walk, 2 for attack, 0 idle
            if (_punyArcherTex != null)
            {
                int anim = u.AttackAnim > 0 ? 2 : (u.Facing.LengthSquared() > 0.001f && Math.Abs((float)Math.Sin(u.WalkPhase)) > 0.01f ? 1 : 0);
                // When moving (speed>0) use walk, when attacking use attack, else idle
                anim = u.AttackAnim > 0 ? 2 : 1; // most units are moving or attacking; idle rare
                if (u.Def.Speed <= 0.01f) anim = u.AttackAnim > 0 ? 2 : 0;
                DrawPunySprite(u, _punyArcherTex, anim, 3, 6f, team);
                return;
            }

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
                int size = (int)f.Size;
                var r = new Rectangle((int)f.Pos.X - size / 2, (int)f.Pos.Y - size / 2, size, size);
                DrawRect(r, f.Color * (a * 0.55f));
                DrawRect(r, Color.White * (a * 0.35f), 1);
            }
        }

        private void DrawHud()
        {
            int w = GraphicsDevice.Viewport.Width;
            int hudH = 92;
            DrawRect(new Rectangle(0, 0, w, hudH), new Color(12, 20, 30, 210));
            DrawRect(new Rectangle(0, hudH - 2, w, 2), new Color(255, 255, 255, 35));

            // Timer + crowns panel
            int sec = Math.Max(0, (int)Math.Ceiling(_timeLeft));
            var timeColor = sec <= 30 ? new Color(255, 110, 110) : new Color(245, 245, 245);
            string timeStr = (sec / 60) + ":" + (sec % 60).ToString("00");
            DrawRect(new Rectangle(w / 2 - 54, 8, 108, 28), new Color(25, 35, 50, 180));
            DrawRect(new Rectangle(w / 2 - 54, 8, 108, 28), new Color(255, 255, 255, 45), 1);
            DrawPixelText(timeStr, w / 2 - 28, 14, 2, timeColor);

            DrawRect(new Rectangle(w - 98, 8, 84, 28), new Color(40, 34, 20, 180));
            DrawRect(new Rectangle(w - 98, 8, 84, 28), new Color(255, 220, 120, 80), 1);
            DrawPixelText(_playerCrowns + "-" + _enemyCrowns, w - 74, 14, 2, new Color(255, 230, 140));

            if (_matchState == "playing")
            {
                var pause = new Rectangle(w - 42, 44, 32, 32);
                DrawRect(pause, new Color(45, 55, 70, 200));
                DrawRect(pause, new Color(200, 210, 220, 120), 1);
                DrawRect(new Rectangle(pause.X + 9, pause.Y + 8, 4, 16), Color.White);
                DrawRect(new Rectangle(pause.X + 19, pause.Y + 8, 4, 16), Color.White);
            }

            // Mana bar
            int barX = 14;
            int barW = w - 64;
            int barY = 58;
            int barH = 22;
            var bg = new Rectangle(barX, barY, barW, barH);
            float pct = _mana / ManaMax;
            var fg = new Rectangle(barX, barY, (int)(barW * pct), barH);
            DrawRect(bg, new Color(30, 28, 40));
            DrawRect(fg, new Color(150, 85, 235));
            DrawRect(new Rectangle(bg.X, bg.Y, fg.Width, 7), new Color(210, 170, 255, 80));
            DrawRect(bg, new Color(210, 210, 225, 110), 1);
            for (int i = 1; i < 10; i++)
            {
                int x = bg.X + (int)(barW * (i / 10f));
                DrawRect(new Rectangle(x, bg.Y, 1, bg.Height), new Color(255, 255, 255, 30));
            }
            DrawPixelText(((int)_mana) + "/10", barX + 6, barY + 5, 2, new Color(240, 230, 255));

            // Status
            string msg = _statusTimer > 0 ? _status : (_selectedHand >= 0 ? "Toque na arena para posicionar" : "Escolha uma carta");
            Color msgBg = _statusTimer > 0 ? new Color(20, 30, 45, 180) : new Color(0, 0, 0, 0);
            int msgW = Math.Min(w - 20, msg.Length * 8 + 16);
            if (_statusTimer > 0)
            {
                DrawRect(new Rectangle(10, 40, msgW, 16), msgBg);
            }
            DrawPixelText(msg.ToUpperInvariant(), 14, 42, 1, _statusTimer > 0 ? new Color(255, 245, 180) : new Color(190, 205, 220));
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
                DrawRect(art, Color.Lerp(def.Color, Color.Black, 0.25f));
                DrawRect(new Rectangle(art.X, art.Y, art.Width, 8), new Color(255, 255, 255, 30));
                DrawRect(art, new Color(255, 255, 255, 25), 1);

                var prev = new Rectangle(art.Center.X - 12, art.Center.Y - 10, 24, 24);
                if (def.Kind == CardKind.Spell)
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
                DrawRect(costR, canAfford ? new Color(140, 70, 200) : new Color(90, 60, 110));
                DrawRect(costR, new Color(230, 210, 255, 160), 1);
                DrawPixelText(def.Cost.ToString(), costR.X + 9, costR.Y + 5, 2, Color.White);

                DrawMiniLabel(def.Name, r.X + 40, r.Bottom - 23, 4, canAfford ? new Color(235, 240, 245) : new Color(140, 145, 150));
                if (!canAfford) DrawRect(r, new Color(0, 0, 0, 55));
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

        private void DrawEndOverlay()
        {
            if (_matchState == "playing") return;
            DrawRect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 150));

            int vw = GraphicsDevice.Viewport.Width;
            int vh = GraphicsDevice.Viewport.Height;

            if (_matchState == "menu")
            {
                var box = new Rectangle(vw / 2 - 170, vh / 2 - 105, 340, 210);
                DrawRect(box, new Color(24, 50, 38, 245));
                DrawRect(box, new Color(120, 200, 140, 180), 2);
                DrawRect(new Rectangle(box.X + 2, box.Y + 2, box.Width - 4, 44), new Color(255, 255, 255, 18));
                DrawPixelText("LUMIAROYALE", box.X + 68, box.Y + 22, 4, Color.White);
                DrawPixelText("ARENA DE CARTAS", box.X + 88, box.Y + 66, 2, new Color(190, 230, 200));

                var play = new Rectangle(box.X + 60, box.Y + 100, 220, 40);
                DrawRect(play, new Color(80, 160, 90));
                DrawRect(play, new Color(220, 255, 225, 180), 2);
                DrawPixelText("TOQUE PARA JOGAR", play.X + 30, play.Y + 14, 2, Color.White);
                DrawPixelText("DESTRUA A TORRE PRINCIPAL", box.X + 44, box.Y + 160, 2, new Color(170, 200, 175));
                return;
            }

            if (_matchState == "paused")
            {
                var box = new Rectangle(vw / 2 - 150, vh / 2 - 95, 300, 190);
                DrawRect(box, new Color(30, 40, 52, 245));
                DrawRect(box, new Color(180, 200, 220, 160), 2);
                DrawPixelText("PAUSADO", box.X + 90, box.Y + 26, 4, Color.White);

                var resume = new Rectangle(box.X + 40, box.Y + 78, 220, 38);
                var restart = new Rectangle(box.X + 40, box.Y + 126, 220, 38);
                DrawRect(resume, new Color(65, 130, 80)); DrawRect(resume, new Color(220, 255, 225, 160), 1);
                DrawRect(restart, new Color(140, 70, 70)); DrawRect(restart, new Color(255, 220, 220, 140), 1);
                DrawPixelText("RETOMAR", resume.X + 76, resume.Y + 13, 2, Color.White);
                DrawPixelText("REINICIAR", restart.X + 68, restart.Y + 13, 2, Color.White);
                return;
            }

            bool win = _matchState == "win";
            var ebox = new Rectangle(vw / 2 - 180, vh / 2 - 100, 360, 200);
            Color c = win ? new Color(40, 110, 65, 245) : new Color(120, 45, 45, 245);
            Color accent = win ? new Color(160, 255, 170) : new Color(255, 170, 170);
            DrawRect(ebox, c);
            DrawRect(ebox, accent * 0.7f, 2);
            DrawRect(new Rectangle(ebox.X + 2, ebox.Y + 2, ebox.Width - 4, 48), new Color(255, 255, 255, 18));

            string title = win ? "VITORIA!" : "DERROTA";
            DrawPixelText(title, ebox.X + (win ? 110 : 120), ebox.Y + 24, 4, Color.White);
            DrawPixelText("COROAS " + _playerCrowns + " X " + _enemyCrowns, ebox.X + 100, ebox.Y + 74, 2, Color.White);
            DrawPixelText("TORRES " + _enemyTowersLost + " X " + _playerTowersLost, ebox.X + 100, ebox.Y + 96, 2, new Color(235, 235, 235));
            int sec = Math.Max(0, (int)Math.Ceiling(_timeLeft));
            DrawPixelText("TEMPO " + (sec / 60) + ":" + (sec % 60).ToString("00"), ebox.X + 124, ebox.Y + 118, 2, new Color(235, 235, 235));

            var again = new Rectangle(ebox.X + 70, ebox.Y + 148, 220, 34);
            DrawRect(again, Color.White * 0.12f);
            DrawRect(again, Color.White * 0.5f, 1);
            DrawPixelText("TOQUE PARA REINICIAR", again.X + 20, again.Y + 11, 2, Color.White);
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
