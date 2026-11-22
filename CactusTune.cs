using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using PVZHE.ModEditor.ModSystem;

namespace WmcqArt
{
    public sealed class Entry : IXWModRuntimeEntry
    {
        private object _inner;
        private Type _innerType;
        private SceneTree _tree;
        private Texture2D _tex;
        private readonly HashSet<ulong> _done = new HashSet<ulong>();

        public void Initialize(XWModRuntimeContext context)
        {
            try
            {
                byte[] bytes = FileAccess.GetFileAsBytes("res://Runtime/WMCQImpl.dll");
                Assembly asm = Assembly.Load(bytes);
                _innerType = asm.GetType("WmcqGoldCardPoolRuntimeEntryXXXXXXXX");
                if (_innerType != null)
                {
                    _inner = Activator.CreateInstance(_innerType);
                    _innerType.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(_inner, new object[] { context });
                }
                Console.WriteLine("WMCQ_ART_INIT inner=" + (_inner != null));
            }
            catch (Exception e) { Console.WriteLine("WMCQ_ART_INIT_ERR " + e); }
        }

        public void OnAllModsLoaded()
        {
            try { _innerType?.GetMethod("OnAllModsLoaded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(_inner, null); }
            catch (Exception e) { Console.WriteLine("WMCQ_ART_ALL_ERR " + e); }
            _tree = Engine.GetMainLoop() as SceneTree;
            if (_tree != null) _tree.ProcessFrame += OnFrame;
            LoadTexture();
            Console.WriteLine("WMCQ_ART_LOADED");
        }

        public void Shutdown()
        {
            if (_tree != null) _tree.ProcessFrame -= OnFrame;
            try { _innerType?.GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(_inner, null); } catch { }
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
                if (!_done.Contains(id) && sp.Texture == null)
                {
                    sp.Texture = _tex;
                    _done.Add(id);
                }
            }
            foreach (Node child in node.GetChildren()) Walk(child);
        }
    }
}
