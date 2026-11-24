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

        private Rectangle _arenaRect;

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
            _deck = CardDef.StarterDeck().OrderBy(_ => _rng.Next()).ToArray();
            _hand = new CardDef[4];
            for (int i = 0; i < 4; i++) _hand[i] = _deck[i % _deck.Length];
            _nextDraw = 4 % _deck.Length;
            _mana = 5f;
            _enemyMana = 5f;
            _enemySpawnTimer = 2f;
            _selectedHand = -1;
            _status = "Toque numa carta e depois na sua metade da arena";

            int w = GraphicsDevice.Viewport.Width;
            int h = GraphicsDevice.Viewport.Height;
            int margin = 18;
            int arenaTop = 80;
            int arenaBottom = h - 150;
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

            if (IsMatchOver()) { base.Update(gameTime); return; }

            _mana = Math.Min(ManaMax, _mana + dt * 0.55f);
            _enemyMana = Math.Min(ManaMax, _enemyMana + dt * 0.55f);

            UpdateEnemy(dt);
            UpdateUnits(dt);
            UpdateTowers(dt);

            _units.RemoveAll(u => !u.Alive);
            CheckEnd();
            base.Update(gameTime);
        }

        private bool IsMatchOver()
        {
            bool playerMain = _towers.Any(t => !t.IsEnemy && t.IsMain && t.Alive);
            bool enemyMain = _towers.Any(t => t.IsEnemy && t.IsMain && t.Alive);
            return !playerMain || !enemyMain;
        }

        private void CheckEnd()
        {
            bool playerMain = _towers.Any(t => !t.IsEnemy && t.IsMain && t.Alive);
            bool enemyMain = _towers.Any(t => t.IsEnemy && t.IsMain && t.Alive);
            if (!playerMain) _status = "Derrota - toque para reiniciar";
            else if (!enemyMain) _status = "Vitória! Toque para reiniciar";
        }

        private void HandleInput()
        {
            while (TouchPanel.IsGestureAvailable)
            {
                var g = TouchPanel.ReadGesture();
                if (g.GestureType != GestureType.Tap) continue;
                var p = g.Position;

                if (IsMatchOver()) { ResetMatch(); return; }

                int cardH = 110;
                int cardY = GraphicsDevice.Viewport.Height - cardH - 10;
                int cardW = (GraphicsDevice.Viewport.Width - 40) / 4;
                for (int i = 0; i < 4; i++)
                {
                    var r = new Rectangle(10 + i * (cardW + 6), cardY, cardW, cardH);
                    if (r.Contains((int)p.X, (int)p.Y))
                    {
                        _selectedHand = i;
                        _status = "Carta: " + _hand[i].Name + " (" + _hand[i].Cost + ")";
                        return;
                    }
                }

                if (_selectedHand >= 0)
                {
                    var card = _hand[_selectedHand];
                    var midY = _arenaRect.Center.Y + 20;
                    if (p.Y < midY) { _status = "Jogue na sua metade"; return; }
                    if (!_arenaRect.Contains((int)p.X, (int)p.Y)) return;
                    if (_mana < card.Cost) { _status = "Mana insuficiente"; return; }

                    SpawnUnit(card, new Vector2(p.X, p.Y), false);
                    _mana -= card.Cost;
                    DrawCard(_selectedHand);
                    _selectedHand = -1;
                }
            }
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
                if (d < bestDist) { bestDist = d; var o = other; best = new TargetInfo { pos = o.Pos, radius = o.Radius, applyDamage = dmg => o.Hp -= dmg }; }
            }

            foreach (var t in _towers)
            {
                if (t.IsEnemy == u.IsEnemy || !t.Alive) continue;
                float d = Vector2.Distance(u.Pos, t.Pos);
                if (d < bestDist) { bestDist = d; var tw = t; best = new TargetInfo { pos = tw.Pos, radius = 24f, applyDamage = dmg => tw.Hp -= dmg }; }
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
                    t.CooldownLeft = t.Cooldown;
                }
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(30, 90, 50));

            _spriteBatch.Begin();
            DrawArena();
            DrawTowers();
            DrawUnits();
            DrawHud();
            DrawCards();
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawArena()
        {
            DrawRect(_arenaRect, new Color(45, 120, 65));
            var river = new Rectangle(_arenaRect.Left, _arenaRect.Center.Y - 12, _arenaRect.Width, 24);
            DrawRect(river, new Color(60, 130, 200));
            DrawRect(new Rectangle(_arenaRect.Left + _arenaRect.Width / 4 - 20, river.Y - 4, 40, 32), new Color(140, 110, 80));
            DrawRect(new Rectangle(_arenaRect.Left + 3 * _arenaRect.Width / 4 - 20, river.Y - 4, 40, 32), new Color(140, 110, 80));
        }

        private void DrawTowers()
        {
            foreach (var t in _towers)
            {
                if (!t.Alive) continue;
                var c = t.IsEnemy ? new Color(200, 70, 70) : new Color(70, 140, 220);
                int size = t.IsMain ? 44 : 34;
                var r = new Rectangle((int)t.Pos.X - size / 2, (int)t.Pos.Y - size / 2, size, size);
                DrawRect(r, c);
                DrawRect(r, Color.White, 2);
                float pct = Math.Max(0, (float)t.Hp / t.MaxHp);
                var bg = new Rectangle(r.X - 4, r.Y - 10, r.Width + 8, 5);
                var fg = new Rectangle(bg.X, bg.Y, (int)(bg.Width * pct), bg.Height);
                DrawRect(bg, Color.Black);
                DrawRect(fg, Color.LimeGreen);
            }
        }

        private void DrawUnits()
        {
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                int s = 18;
                var r = new Rectangle((int)u.Pos.X - s / 2, (int)u.Pos.Y - s / 2, s, s);
                DrawRect(r, u.Def.Color);
                DrawRect(r, u.IsEnemy ? Color.DarkRed : Color.White, 1);
            }
        }

        private void DrawHud()
        {
            DrawRect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, 70), new Color(0, 0, 0, 160));
            int barW = GraphicsDevice.Viewport.Width - 40;
            var bg = new Rectangle(20, 40, barW, 16);
            var fg = new Rectangle(20, 40, (int)(barW * (_mana / ManaMax)), 16);
            DrawRect(bg, Color.DarkSlateGray);
            DrawRect(fg, new Color(170, 90, 255));
            DrawRect(bg, Color.White, 1);
        }

        private void DrawCards()
        {
            int cardH = 110;
            int y = GraphicsDevice.Viewport.Height - cardH - 10;
            int cardW = (GraphicsDevice.Viewport.Width - 40) / 4;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rectangle(10 + i * (cardW + 6), y, cardW, cardH);
                bool sel = i == _selectedHand;
                DrawRect(r, sel ? new Color(70, 70, 70) : new Color(35, 35, 35));
                DrawRect(r, sel ? Color.Yellow : Color.Gray, sel ? 3 : 1);
                var def = _hand[i];
                if (def == null) continue;
                DrawRect(new Rectangle(r.X + 8, r.Y + 8, r.Width - 16, 50), def.Color);
                DrawRect(new Rectangle(r.X + 6, r.Bottom - 28, 26, 20), new Color(120, 60, 180));
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
