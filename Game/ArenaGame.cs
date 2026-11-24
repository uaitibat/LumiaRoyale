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

        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<Tower> _towers = new List<Tower>();
        private readonly List<Fx> _fx = new List<Fx>();
        private readonly Random _rng = new Random();

        private CardDef[] _deck;
        private CardDef[] _hand;
        private int _nextDraw;
        private float _mana;
        private const float ManaMax = 10f;
        private int _selectedHand = -1;

        private float _enemyMana;
        private float _enemySpawnTimer;
        private string _status = "Toque numa carta e depois na sua metade da arena";
        private string _matchState = ""; // "", "win", "lose"

        private Rectangle _arenaRect;
        private float _statusTimer;

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
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        private void ResetMatch()
        {
            _units.Clear();
            _towers.Clear();
            _fx.Clear();
            _deck = CardDef.StarterDeck().OrderBy(_ => _rng.Next()).ToArray();
            _hand = new CardDef[4];
            for (int i = 0; i < 4; i++) _hand[i] = _deck[i % _deck.Length];
            _nextDraw = 4 % _deck.Length;
            _mana = 5f;
            _enemyMana = 5f;
            _enemySpawnTimer = 2f;
            _selectedHand = -1;
            _status = "Toque numa carta e depois na sua metade";
            _matchState = "";
            _statusTimer = 3f;

            int w = GraphicsDevice.Viewport.Width;
            int h = GraphicsDevice.Viewport.Height;
            int margin = Math.Max(14, w / 36);
            int arenaTop = Math.Max(88, h / 12);
            int arenaBottom = h - Math.Max(150, h / 7);
            _arenaRect = new Rectangle(margin, arenaTop, w - margin * 2, arenaBottom - arenaTop);

            // Enemy towers (top)
            _towers.Add(new Tower(new Vector2(w * 0.25f, arenaTop + 70), 900, false, true));
            _towers.Add(new Tower(new Vector2(w * 0.75f, arenaTop + 70), 900, false, true));
            _towers.Add(new Tower(new Vector2(w * 0.5f, arenaTop + 150), 1400, true, true));
            // Player towers (bottom)
            _towers.Add(new Tower(new Vector2(w * 0.25f, arenaBottom - 70), 900, false, false));
            _towers.Add(new Tower(new Vector2(w * 0.75f, arenaBottom - 70), 900, false, false));
            _towers.Add(new Tower(new Vector2(w * 0.5f, arenaBottom - 150), 1400, true, false));
        }

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0.1f) dt = 0.1f;

            HandleInput();
            UpdateFx(dt);
            if (_statusTimer > 0) _statusTimer -= dt;

            if (_matchState != "") { base.Update(gameTime); return; }

            _mana = Math.Min(ManaMax, _mana + dt * 0.55f);
            _enemyMana = Math.Min(ManaMax, _enemyMana + dt * 0.55f);

            UpdateEnemy(dt);
            UpdateUnits(dt);
            UpdateTowers(dt);

            for (int i = _units.Count - 1; i >= 0; i--)
            {
                if (_units[i].Alive) continue;
                var u = _units[i];
                AddFx(u.Pos, u.IsEnemy ? new Color(255, 120, 120) : new Color(120, 190, 255), 18f, 0.35f, "death");
                _units.RemoveAt(i);
            }
            CheckEnd();
            base.Update(gameTime);
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
            foreach (var u in _units) if (u.HitFlash > 0) u.HitFlash -= dt;
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

        private void HandleInput()
        {
            while (TouchPanel.IsGestureAvailable)
            {
                var g = TouchPanel.ReadGesture();
                if (g.GestureType != GestureType.Tap) continue;
                var p = g.Position;

                if (_matchState != "") { ResetMatch(); return; }

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
                    var midY = _arenaRect.Center.Y + 20;
                    if (p.Y < midY) { _status = "Jogue na sua metade"; _statusTimer = 1.5f; return; }
                    if (!_arenaRect.Contains((int)p.X, (int)p.Y)) return;
                    if (_mana < card.Cost) { _status = "Mana insuficiente"; _statusTimer = 1.5f; return; }

                    SpawnUnit(card, new Vector2(p.X, p.Y), false);
                    AddFx(new Vector2(p.X, p.Y), card.Color, 14f, 0.28f, "spawn");
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

        private void SpawnUnit(CardDef def, Vector2 pos, bool enemy)
        {
            pos.X = MathHelper.Clamp(pos.X, _arenaRect.Left + 15, _arenaRect.Right - 15);
            pos.Y = MathHelper.Clamp(pos.Y, _arenaRect.Top + 15, _arenaRect.Bottom - 15);
            _units.Add(new Unit(def, pos, enemy));
        }

        private void UpdateEnemy(float dt)
        {
            _enemySpawnTimer -= dt;
            if (_enemySpawnTimer > 0) return;
            _enemySpawnTimer = 2.5f + (float)_rng.NextDouble() * 2.0f;

            var affordable = _deck.Where(c => c.Cost <= _enemyMana).ToList();
            if (affordable.Count == 0) return;
            var card = affordable[_rng.Next(affordable.Count)];
            _enemyMana -= card.Cost;
            float x = _arenaRect.Left + 40 + (float)_rng.NextDouble() * (_arenaRect.Width - 80);
            float y = _arenaRect.Top + 80 + (float)_rng.NextDouble() * (_arenaRect.Height * 0.25f);
            SpawnUnit(card, new Vector2(x, y), true);
        }

        private void UpdateUnits(float dt)
        {
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                u.AttackTimer -= dt;
                var target = FindTarget(u);
                if (target == null) continue;

                float dist = Vector2.Distance(u.Pos, target.Value.pos);
                if (dist <= u.Def.Range + target.Value.radius)
                {
                    if (u.AttackTimer <= 0f)
                    {
                        target.Value.applyDamage(u.Def.Damage);
                        AddFx(Vector2.Lerp(u.Pos, target.Value.pos, 0.6f), u.Def.Color, 8f, 0.16f, "hit");
                        u.AttackTimer = u.Def.AttackCooldown;
                    }
                }
                else
                {
                    var dir = target.Value.pos - u.Pos;
                    if (dir.LengthSquared() > 1f)
                    {
                        dir.Normalize();
                        u.Pos += dir * u.Def.Speed * dt;
                    }
                }
            }
        }

        private struct TargetInfo
        {
            public Vector2 pos;
            public float radius;
            public Action<int> applyDamage;
        }

        private TargetInfo? FindTarget(Unit u)
        {
            TargetInfo? best = null;
            float bestDist = float.MaxValue;

            foreach (var other in _units)
            {
                if (other.IsEnemy == u.IsEnemy || !other.Alive) continue;
                float d = Vector2.Distance(u.Pos, other.Pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    var o = other;
                    best = new TargetInfo { pos = o.Pos, radius = o.Radius, applyDamage = dmg => { o.Hp -= dmg; o.HitFlash = 0.12f; } };
                }
            }

            foreach (var t in _towers)
            {
                if (t.IsEnemy == u.IsEnemy || !t.Alive) continue;
                float d = Vector2.Distance(u.Pos, t.Pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    var tw = t;
                    best = new TargetInfo { pos = tw.Pos, radius = 24f, applyDamage = dmg => { tw.Hp -= dmg; tw.HitFlash = 0.12f; } };
                }
            }
            return best;
        }

        private void UpdateTowers(float dt)
        {
            foreach (var t in _towers)
            {
                if (!t.Alive) continue;
                t.CooldownLeft -= dt;
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
            if (_selectedHand < 0 || _matchState != "") return;
            int midY = _arenaRect.Center.Y + 20;
            var allowed = new Rectangle(_arenaRect.X, midY, _arenaRect.Width, _arenaRect.Bottom - midY);
            DrawRect(allowed, new Color(120, 255, 160, 30));
            DrawRect(allowed, new Color(150, 255, 180, 120), 2);
        }

        private void DrawTowers()
        {
            foreach (var t in _towers)
            {
                if (!t.Alive) continue;
                Color team = t.IsEnemy ? new Color(208, 74, 74) : new Color(74, 146, 226);
                Color teamDark = t.IsEnemy ? new Color(120, 36, 36) : new Color(36, 78, 132);
                int size = t.IsMain ? 46 : 36;
                var body = new Rectangle((int)t.Pos.X - size / 2, (int)t.Pos.Y - size / 2, size, size);
                var baseR = new Rectangle(body.X - 4, body.Bottom - 8, body.Width + 8, 10);
                DrawRect(new Rectangle(baseR.X + 3, baseR.Y + 5, baseR.Width, baseR.Height), new Color(0, 0, 0, 45));
                DrawRect(baseR, new Color(88, 88, 96));
                DrawRect(body, new Color(165, 170, 180));
                DrawRect(body, teamDark, 2);
                var top = new Rectangle(body.X + 5, body.Y + 5, body.Width - 10, 10);
                DrawRect(top, team);
                DrawRect(new Rectangle(body.Center.X - 4, body.Y - 8, 8, 12), teamDark);
                if (t.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 120));

                float pct = Math.Max(0, (float)t.Hp / t.MaxHp);
                var hbg = new Rectangle(body.X - 6, body.Y - 14, body.Width + 12, 6);
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
                int s = u.Def.Kind == UnitKind.Melee ? 22 : 18;
                var body = new Rectangle((int)u.Pos.X - s / 2, (int)u.Pos.Y - s / 2, s, s);
                // shadow
                DrawRect(new Rectangle(body.X + 2, body.Bottom - 2, body.Width - 2, 4), new Color(0, 0, 0, 55));
                DrawRect(body, u.Def.Color);
                DrawRect(body, dark, 2);
                DrawRect(new Rectangle(body.X + 3, body.Y + 3, body.Width - 6, 5), new Color(255, 255, 255, 55));
                // team marker
                DrawRect(new Rectangle(body.Center.X - 4, body.Y - 5, 8, 4), team);
                if (u.Def.Kind == UnitKind.Ranged)
                {
                    DrawRect(new Rectangle(body.Center.X - 2, body.Center.Y - 2, 4, 4), Color.White);
                }
                if (u.HitFlash > 0) DrawRect(body, new Color(255, 255, 255, 110));

                float pct = Math.Max(0, (float)u.Hp / u.MaxHp);
                var hbg = new Rectangle(body.X - 2, body.Y - 9, body.Width + 4, 4);
                var hfg = new Rectangle(hbg.X, hbg.Y, (int)(hbg.Width * pct), hbg.Height);
                DrawRect(hbg, new Color(15, 15, 15, 190));
                DrawRect(hfg, pct > 0.35f ? new Color(120, 230, 110) : new Color(235, 90, 80));
            }
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
            int hudH = 76;
            DrawRect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, hudH), new Color(10, 18, 14, 185));
            DrawRect(new Rectangle(0, hudH - 2, GraphicsDevice.Viewport.Width, 2), new Color(255, 255, 255, 30));

            int barW = GraphicsDevice.Viewport.Width - 28;
            var bg = new Rectangle(14, 44, barW, 18);
            float pct = _mana / ManaMax;
            var fg = new Rectangle(14, 44, (int)(barW * pct), 18);
            DrawRect(bg, new Color(35, 35, 45));
            DrawRect(fg, new Color(165, 90, 240));
            DrawRect(new Rectangle(bg.X, bg.Y, fg.Width, 6), new Color(210, 170, 255, 90));
            DrawRect(bg, new Color(220, 220, 230, 120), 1);

            // mana pips
            for (int i = 1; i < 10; i++)
            {
                int x = bg.X + (int)(barW * (i / 10f));
                DrawRect(new Rectangle(x, bg.Y, 1, bg.Height), new Color(255, 255, 255, 35));
            }

            string msg = _statusTimer > 0 ? _status : (_selectedHand >= 0 ? "Escolha onde posicionar" : "Selecione uma carta");
            DrawStatusText(msg, 14, 14, 7, Color.White);
            DrawStatusText(((int)_mana).ToString() + "/10", bg.Right - 46, 16, 6, new Color(230, 210, 255));
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
                Color bg = sel ? new Color(60, 60, 68) : new Color(34, 34, 40);
                if (!canAfford) bg = new Color(28, 28, 32);
                DrawRect(r, bg);
                DrawRect(r, sel ? new Color(255, 220, 90) : new Color(120, 120, 130), sel ? 3 : 1);

                if (def == null) continue;
                var art = new Rectangle(r.X + 7, r.Y + 7, r.Width - 14, r.Height - 38);
                DrawRect(art, Color.Lerp(def.Color, Color.Black, 0.15f));
                DrawRect(new Rectangle(art.X, art.Y, art.Width, 10), new Color(255, 255, 255, 35));
                // tiny unit preview
                var prev = new Rectangle(art.Center.X - 10, art.Center.Y - 8, 20, 20);
                DrawRect(prev, def.Color);
                DrawRect(prev, Color.White * 0.7f, 1);
                if (def.Kind == UnitKind.Ranged) DrawRect(new Rectangle(prev.Center.X - 2, prev.Center.Y - 2, 4, 4), Color.White);

                var costR = new Rectangle(r.X + 6, r.Bottom - 26, 28, 20);
                DrawRect(costR, new Color(126, 66, 180));
                DrawRect(costR, new Color(230, 210, 255), 1);
                DrawCostPips(costR, def.Cost);

                DrawMiniLabel(def.Name, r.X + 38, r.Bottom - 22, 4, canAfford ? Color.White : new Color(170, 170, 170));
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
            if (_matchState == "") return;
            DrawRect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 130));
            var box = new Rectangle(GraphicsDevice.Viewport.Width / 2 - 150, GraphicsDevice.Viewport.Height / 2 - 56, 300, 112);
            Color c = _matchState == "win" ? new Color(60, 140, 80) : new Color(150, 60, 60);
            DrawRect(box, c);
            DrawRect(box, Color.White, 2);
            string title = _matchState == "win" ? "VITORIA!" : "DERROTA";
            DrawPixelText(title, box.X + 70, box.Y + 24, 4, Color.White);
            DrawPixelText("TOQUE PARA REINICIAR", box.X + 34, box.Y + 72, 2, Color.White * 0.9f);
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
