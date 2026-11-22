using System;
using System.Collections.Generic;
using Godot;
using PVZHE.ModEditor.ModSystem;

namespace WmcqArt
{
    public sealed class Entry : IXWModRuntimeEntry
    {
        private SceneTree _tree;
        private Texture2D _tex;
        private readonly HashSet<ulong> _done = new HashSet<ulong>();

        public void Initialize(XWModRuntimeContext context)
        {
            Console.WriteLine("WMCQ_ART_INIT");
        }

        public void OnAllModsLoaded()
        {
            _tree = Engine.GetMainLoop() as SceneTree;
            if (_tree != null) _tree.ProcessFrame += OnFrame;
            LoadTexture();
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
