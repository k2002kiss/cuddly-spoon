using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using PVZHE.ModEditor.ModSystem;

namespace MagicPillar
{
    public sealed class Entry : IXWModRuntimeEntry
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private SceneTree _tree;
        private double _scanAt;
        private readonly Dictionary<int, Vector2> _seen = new Dictionary<int, Vector2>();
        private readonly List<(Sprite2D glow, Sprite2D core, double end)> _pillars = new List<(Sprite2D, Sprite2D, double)>();

        public void Initialize(XWModRuntimeContext context)
        {
            _tree = Engine.GetMainLoop() as SceneTree;
            if (_tree != null) _tree.ProcessFrame += OnFrame;
            Console.WriteLine("MAGIC_PILLAR_INIT");
        }

        public void OnAllModsLoaded()
        {
            if (_tree == null)
            {
                _tree = Engine.GetMainLoop() as SceneTree;
                if (_tree != null) _tree.ProcessFrame += OnFrame;
            }
            Console.WriteLine("MAGIC_PILLAR_LOADED");
            SpawnPillar(new Vector2(640, 360));
        }

        public void Shutdown()
        {
            if (_tree != null) _tree.ProcessFrame -= OnFrame;
        }

        private void OnFrame()
        {
            double now = Time.GetTicksMsec() / 1000.0;
            if (now < _scanAt) return;
            _scanAt = now + 0.05;
            if (_tree?.Root == null) return;

            ScanBullets();
            CleanPillars(now);
        }

        private void ScanBullets()
        {
            try
            {
                Type bfType = typeof(XWModRuntimeContext).Assembly.GetType("BulletField");
                if (bfType == null) return;
                object inst = bfType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                if (inst == null) return;
                FieldInfo dataField = FindField(bfType, "_data");
                FieldInfo idxField = FindField(bfType, "_activeIndices");
                FieldInfo cntField = FindField(bfType, "_activeCount");
                if (dataField == null || idxField == null || cntField == null) return;
                if (!(dataField.GetValue(inst) is Array data)) return;
                object idxObj = idxField.GetValue(inst);
                int count = Convert.ToInt32(cntField.GetValue(inst));
                var active = new HashSet<int>();

                for (int i = 0; i < count; i++)
                {
                    int idx;
                    if (idxObj is Array ia) idx = Convert.ToInt32(ia.GetValue(i));
                    else if (idxObj is System.Collections.IList il) idx = Convert.ToInt32(il[i]);
                    else break;
                    if (idx < 0 || idx >= data.Length) continue;
                    object boxed = data.GetValue(idx);
                    if (boxed == null) continue;
                    Type bt = boxed.GetType();
                    object activeObj = FindField(bt, "active")?.GetValue(boxed);
                    if (activeObj is bool act && !act) continue;
                    object overObj = FindField(bt, "over")?.GetValue(boxed);
                    if (overObj is bool ov && ov) continue;

                    object cfg = FindField(bt, "config")?.GetValue(boxed);
                    string name = cfg != null ? FindField(cfg.GetType(), "name")?.GetValue(cfg) as string : null;
                    if (name != "MagicLaser") continue;

                    object posObj = FindField(bt, "pos")?.GetValue(boxed);
                    if (posObj is Vector2 pos)
                    {
                        _seen[idx] = pos;
                        active.Add(idx);
                    }
                }

                var gone = new List<int>();
                foreach (var kv in _seen)
                {
                    if (!active.Contains(kv.Key)) gone.Add(kv.Key);
                }
                foreach (int idx in gone)
                {
                    Vector2 pos = _seen[idx];
                    _seen.Remove(idx);
                    SpawnPillar(pos);
                }
            }
            catch (Exception e) { Console.WriteLine("MAGIC_PILLAR_ERR " + e.Message); }
        }

        private void SpawnPillar(Vector2 pos)
        {
            if (_tree?.Root == null) return;
            var material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            var glow = MakeSprite(material, new Color(1f, 0.35f, 0.8f, 0.35f), new Vector2(1.6f, 2.6f));
            var core = MakeSprite(material, new Color(1f, 0.85f, 0.95f, 0.95f), new Vector2(0.9f, 2.2f));
            glow.GlobalPosition = pos;
            core.GlobalPosition = pos;
            _tree.Root.AddChild(glow);
            _tree.Root.AddChild(core);
            _pillars.Add((glow, core, Time.GetTicksMsec() / 1000.0 + 2.0));
            Console.WriteLine("MAGIC_PILLAR_SPAWN");
        }

        private static Sprite2D MakeSprite(CanvasItemMaterial material, Color color, Vector2 scale)
        {
            var gradient = new Gradient();
            gradient.SetOffsets(new[] { 0f, 0.25f, 0.55f, 0.8f, 1f });
            gradient.SetColors(new[]
            {
                new Color(1f, 0.2f, 0.6f, 0f),
                new Color(1f, 0.35f, 0.75f, 1f),
                new Color(1f, 0.75f, 0.9f, 1f),
                new Color(1f, 0.55f, 0.85f, 1f),
                new Color(1f, 0.2f, 0.6f, 0f),
            });
            var tex = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 32,
                Height = 128,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
            };
            return new Sprite2D
            {
                Material = material,
                Modulate = color,
                Texture = tex,
                Scale = scale,
                ZIndex = 20,
            };
        }

        private void CleanPillars(double now)
        {
            for (int i = _pillars.Count - 1; i >= 0; i--)
            {
                var p = _pillars[i];
                if (now < p.end) continue;
                if (IsInstanceValid(p.glow)) p.glow.QueueFree();
                if (IsInstanceValid(p.core)) p.core.QueueFree();
                _pillars.RemoveAt(i);
            }
        }

        private static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo f = type.GetField(name, Flags);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }
    }
}
