using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using PVZHE.ModEditor.ModSystem;

namespace DamageNumbers
{
    public sealed class Entry : IXWModRuntimeEntry
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private SceneTree _tree;
        private CanvasLayer _layer;
        private double _scanAt;
        private readonly Dictionary<ulong, double> _hp = new Dictionary<ulong, double>();
        private readonly List<(Label label, double start, Vector2 pos)> _labels = new List<(Label, double, Vector2)>();

        public void Initialize(XWModRuntimeContext context)
        {
            _tree = Engine.GetMainLoop() as SceneTree;
            if (_tree != null) _tree.ProcessFrame += OnFrame;
            Console.WriteLine("DAMAGE_NUM_INIT");
        }

        public void OnAllModsLoaded()
        {
            if (_tree == null)
            {
                _tree = Engine.GetMainLoop() as SceneTree;
                if (_tree != null) _tree.ProcessFrame += OnFrame;
            }
            Console.WriteLine("DAMAGE_NUM_LOADED");
        }

        public void Shutdown()
        {
            if (_tree != null) _tree.ProcessFrame -= OnFrame;
        }

        private void OnFrame()
        {
            double now = Time.GetTicksMsec() / 1000.0;
            if (now < _scanAt) return;
            _scanAt = now + 0.1;
            if (_tree == null) return;
            Node scene = _tree.CurrentScene;
            if (scene == null) return;
            EnsureUi(scene);
            Scan(scene);
            TickLabels(now);
        }

        private void EnsureUi(Node scene)
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer) && _layer.IsInsideTree()) return;
            _layer = new CanvasLayer { Layer = 100 };
            scene.AddChild(_layer);
        }

        private void Scan(Node node)
        {
            if (node is Node2D n2)
            {
                object inst = FindField(node.GetType(), "instance")?.GetValue(node);
                if (inst != null)
                {
                    object hpObj = FindField(inst.GetType(), "hitpoints")?.GetValue(inst);
                    if (hpObj != null)
                    {
                        double hp = Convert.ToDouble(hpObj);
                                    ulong id = node.GetInstanceId();
                        if (_hp.TryGetValue(id, out double old))
                        {
                            double diff = old - hp;
                            if (diff > 0.5) Show(n2, "-" + Math.Round(diff), new Color(1, 1, 1));
                            else if (diff < -0.5) Show(n2, "+" + Math.Round(-diff), new Color(0.4f, 1, 0.4f));
                        }
                        _hp[id] = hp;
                    }
                }
            }
            foreach (Node child in node.GetChildren()) Scan(child);
        }

        private void Show(Node2D target, string text, Color color)
        {
            if (_layer == null) return;
            var label = new Label { Text = text, Modulate = color, ZIndex = 300 };
            label.AddThemeFontSizeOverride("font_size", 22);
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
            label.AddThemeConstantOverride("outline_size", 4);
            Vector2 pos = target.GetGlobalTransformWithCanvas().Origin + new Vector2(-10, -70);
            label.Position = pos;
            _layer.AddChild(label);
            _labels.Add((label, Time.GetTicksMsec() / 1000.0, pos));
        }

        private void TickLabels(double now)
        {
            for (int i = _labels.Count - 1; i >= 0; i--)
            {
                var item = _labels[i];
                double t = now - item.start;
                if (t >= 0.9 || !GodotObject.IsInstanceValid(item.label))
                {
                    if (GodotObject.IsInstanceValid(item.label)) item.label.QueueFree();
                    _labels.RemoveAt(i);
                    continue;
                }
                item.label.Position = item.pos + new Vector2(0, (float)(-40 * t));
                item.label.Modulate = new Color(item.label.Modulate.R, item.label.Modulate.G, item.label.Modulate.B, (float)(1 - t / 0.9));
            }
        }

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
