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
        private readonly HashSet<ulong> _done = new HashSet<ulong>();
        private readonly Dictionary<ulong, double> _next = new Dictionary<ulong, double>();
        private SceneTree _tree;
        private double _scanAt;
        private bool _scaled;

        public void Initialize(XWModRuntimeContext context)
        {
            _tree = Engine.GetMainLoop() as SceneTree;
            if (_tree != null) _tree.ProcessFrame += OnFrame;
            Console.WriteLine("CACTUS_TUNE_INIT api=" + context.ApiVersion);
        }

        public void OnAllModsLoaded()
        {
            if (_tree == null)
            {
                _tree = Engine.GetMainLoop() as SceneTree;
                if (_tree != null) _tree.ProcessFrame += OnFrame;
            }
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

            if (!_scaled)
            {
                _scaled = true;
                if (plant is Node2D n2) n2.Scale = new Vector2(2.5f, 2.5f);
                Console.WriteLine("CACTUS_TUNE_FOUND " + id);
            }

            if (_done.Add(id))
            {
                object shell = FindField(type, "_shell")?.GetValue(plant);
                if (shell != null)
                {
                    FieldInfo dmg = FindField(shell.GetType(), "baseDamage");
                    if (dmg != null) dmg.SetValue(shell, 10000.0);
                }
            }

            if (!_next.TryGetValue(id, out double next) || now >= next)
            {
                FindField(type, "_mainReady")?.SetValue(plant, true);
                _next[id] = now + 0.5;
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
