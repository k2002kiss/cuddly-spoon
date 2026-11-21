using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using PVZHE.ModEditor.ModSystem;

namespace CactusTune
{
    public sealed class Entry : IXWModRuntimeEntry
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly HashSet<ulong> _tuned = new HashSet<ulong>();
        private readonly Dictionary<ulong, double> _next = new Dictionary<ulong, double>();
        private SceneTree _tree;
        private double _scanAt;

        public void Initialize(XWModRuntimeContext context)
        {
            _tree = Engine.GetMainLoop() as SceneTree;
            if (_tree != null) _tree.ProcessFrame += OnFrame;
            Console.WriteLine("CACTUS_TUNE_INIT api=" + context.ApiVersion);
        }

        public void OnAllModsLoaded()
        {
            Console.WriteLine("CACTUS_TUNE_LOADED");
        }

        public void Shutdown()
        {
            if (_tree != null) _tree.ProcessFrame -= OnFrame;
        }

        private void OnFrame()
        {
            double now = Time.GetTicksMsec() / 1000.0;
            if (now < _scanAt) return;
            _scanAt = now + 0.25;
            if (_tree?.Root == null) return;
            Walk(_tree.Root, now);
        }

        private void Walk(Node node, double now)
        {
            if (node.GetType().Name == "TowerDefensePlantCactusCannon") Tune(node, now);
            foreach (Node child in node.GetChildren()) Walk(child, now);
        }

        private void Tune(Node plant, double now)
        {
            ulong id = plant.GetInstanceId();
            Type type = plant.GetType();

            string name = ReadName(ReadConfig(type, plant));
            if (name != "CactusCannonX") return;

            if (_tuned.Add(id))
            {
                object shell = type.GetField("_shell", Flags)?.GetValue(plant);
                if (shell != null)
                {
                    FieldInfo dmg = shell.GetType().GetField("baseDamage", Flags);
                    if (dmg != null) dmg.SetValue(shell, 10000.0);
                }
                Console.WriteLine("CACTUS_TUNE_APPLY " + id);
            }

            if (!_next.TryGetValue(id, out double next) || now >= next)
            {
                type.GetField("_mainReady", Flags)?.SetValue(plant, true);
                _next[id] = now + 0.5;
            }
        }

        private static object ReadConfig(Type type, object plant)
        {
            object cfg = type.GetField("_config", Flags)?.GetValue(plant);
            if (cfg == null)
            {
                PropertyInfo p = type.GetProperty("config", Flags);
                if (p != null) cfg = p.GetValue(plant);
            }
            return cfg;
        }

        private static string ReadName(object cfg)
        {
            if (cfg == null) return null;
            Type t = cfg.GetType();
            object v = t.GetField("name", Flags)?.GetValue(cfg);
            if (v == null)
            {
                PropertyInfo p = t.GetProperty("name", Flags);
                if (p != null) v = p.GetValue(cfg);
            }
            return v as string;
        }
    }
}
