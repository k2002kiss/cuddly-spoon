using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using PVZHE.ModEditor.ModSystem;

namespace WmcqArt
{
    public sealed class Entry : IXWModRuntimeEntry
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private SceneTree _tree;
        private Texture2D _tex;
        private readonly HashSet<ulong> _done = new HashSet<ulong>();

        public void Initialize(XWModRuntimeContext context) { Console.WriteLine("WMCQ_ART_INIT"); }

        public void OnAllModsLoaded()
        {
            _tree = Engine.GetMainLoop() as SceneTree;
            if (_tree != null) _tree.ProcessFrame += OnFrame;
            LoadTexture();
            InjectGold();
            Console.WriteLine("WMCQ_ART_LOADED");
        }

        public void Shutdown()
        {
            if (_tree != null) _tree.ProcessFrame -= OnFrame;
        }

        private void LoadTexture()
        {
            try
            {
                byte[] png = FileAccess.GetFileAsBytes("res://Resources/Characters/Plants/WMCQ/Sprite/WMCQ_body.png");
                var img = new Image();
                img.LoadPngFromBuffer(png);
                _tex = ImageTexture.CreateFromImage(img);
                Console.WriteLine("WMCQ_ART_TEX " + img.GetWidth() + "x" + img.GetHeight());
            }
            catch (Exception e) { Console.WriteLine("WMCQ_ART_TEX_ERR " + e.Message); }
        }

        private void InjectGold()
        {
            try
            {
                Type rm = typeof(XWModRuntimeContext).Assembly.GetType("ResourceManager");
                object inst = rm?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                object banksObj = rm?.GetProperty("TOWERDEFENSE_PACKETBANKS", Flags)?.GetValue(inst);
                if (!(banksObj is Godot.Collections.Dictionary banks)) return;
                Godot.Collections.Dictionary targetCat = null;
                Godot.Collections.Array targetArr = null;
                foreach (var k in banks.Keys)
                {
                    object bank = banks[k];
                    object catObj = bank == null ? null : FindField(bank.GetType(), "category")?.GetValue(bank);
                    if (!(catObj is Godot.Collections.Dictionary cat)) continue;
                    if (!cat.ContainsKey("Gold")) continue;
                    var arr = cat["Gold"].AsGodotArray();
                    if (targetArr == null) { targetCat = cat; targetArr = arr; }
                    foreach (var v in arr) if (v.AsString() == "PlantWinterMelonCat") { targetCat = cat; targetArr = arr; break; }
                }
                if (targetArr == null) return;
                for (int i = targetArr.Count - 1; i >= 0; i--) if (targetArr[i].AsString() == "WMCQ") targetArr.RemoveAt(i);
                bool has = false;
                foreach (var v in targetArr) if (v.AsString() == "WMCQ") { has = true; break; }
                if (!has) targetArr.Insert(0, "WMCQ");
                targetCat["Gold"] = targetArr;
                Console.WriteLine("WMCQ_ART_GOLD " + targetArr.Count);
            }
            catch (Exception e) { Console.WriteLine("WMCQ_ART_GOLD_ERR " + e.Message); }
        }

        private void OnFrame()
        {
            if (_tree?.CurrentScene == null || _tex == null) return;
            Walk(_tree.CurrentScene);
        }

        private void Walk(Node node)
        {
            if (node is Sprite2D sp && sp.Name == "Body")
            {
                ulong id = sp.GetInstanceId();
                if (!_done.Contains(id) && sp.Texture == null) { sp.Texture = _tex; _done.Add(id); }
            }
            foreach (Node child in node.GetChildren()) Walk(child);
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
