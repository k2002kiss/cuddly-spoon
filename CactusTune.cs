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
        private readonly HashSet<ulong> _plants = new HashSet<ulong>();
        private readonly HashSet<ulong> _shots = new HashSet<ulong>();
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
            if (_tree == null)
            {
                _tree = Engine.GetMainLoop() as SceneTree;
                if (_tree != null) _tree.ProcessFrame += OnFrame;
            }
            Console.WriteLine("CACTUS_TUNE_LOADED");
            TryInjectGold();
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
            Walk(_tree.Root, now);
            TrySlowBullets();
        }

        private void Walk(Node node, double now)
        {
            string typeName = node.GetType().Name;
            if (typeName == "TowerDefensePlantCactusCannon") TunePlant(node, now);
            else if (typeName == "TowerDefenseProjectile") TuneShot(node);
            foreach (Node child in node.GetChildren()) Walk(child, now);
        }


        private static void TryInjectGold()
        {
            try
            {
                Type asm = typeof(XWModRuntimeContext).Assembly.GetType("ResourceManager");
                if (asm == null) { Console.WriteLine("CACTUS_GOLD no ResourceManager"); return; }
                object inst = asm.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                if (inst == null) { Console.WriteLine("CACTUS_GOLD no instance"); return; }
                object banksObj = asm.GetProperty("TOWERDEFENSE_PACKETBANKS", Flags)?.GetValue(inst);
                if (!(banksObj is Godot.Collections.Dictionary banks)) { Console.WriteLine("CACTUS_GOLD no banks"); return; }
                int added = 0;
                foreach (var k in banks.Keys)
                {
                    object bank = banks[k];
                    if (bank == null) continue;
                    object catObj = FindField(bank.GetType(), "category")?.GetValue(bank);
                    if (!(catObj is Godot.Collections.Dictionary cat)) continue;
                    Godot.Collections.Array arr;
                    if (cat.ContainsKey("Gold")) arr = cat["Gold"].AsGodotArray();
                    else { arr = new Godot.Collections.Array(); cat["Gold"] = arr; }
                    bool has = false;
                    foreach (var v in arr) if (v.AsString() == "CactusCannonX") { has = true; break; }
                    if (!has) { arr.Add("CactusCannonX"); added++; }
                }
                Console.WriteLine("CACTUS_GOLD added=" + added);
            }
            catch (Exception e) { Console.WriteLine("CACTUS_GOLD error " + e.Message); }
        }


        private static void ScaleField(Type type, object boxed, string name, float factor)
        {
            FieldInfo f = FindField(type, name);
            if (f == null) return;
            object v = f.GetValue(boxed);
            if (v is float fl) f.SetValue(boxed, fl * factor);
            else if (v is double db) f.SetValue(boxed, db * factor);
        }

        private static void TrySlowBullets()
        {
            try
            {
                Type asm = typeof(XWModRuntimeContext).Assembly.GetType("BulletField");
                if (asm == null) return;
                object inst = asm.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                if (inst == null) return;
                FieldInfo dataField = FindField(asm, "_data");
                FieldInfo idxField = FindField(asm, "_activeIndices");
                FieldInfo cntField = FindField(asm, "_activeCount");
                if (dataField == null || idxField == null || cntField == null) return;
                if (!(dataField.GetValue(inst) is Array data)) return;
                object idxObj = idxField.GetValue(inst);
                int count = Convert.ToInt32(cntField.GetValue(inst));
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

                    object ownerObj = FindField(bt, "fireCharacter")?.GetValue(boxed);
                    if (ownerObj == null) continue;
                    string ownerName = ReadName(ReadConfig(ownerObj.GetType(), ownerObj));
                    if (ownerName != "CactusCannonX") continue;

                    object catOpenObj = FindField(bt, "catapultOpen")?.GetValue(boxed);
                    if (catOpenObj is bool catOpen && catOpen)
                    {
                        ScaleField(bt, boxed, "catapultTime", 5f);
                        ScaleField(bt, boxed, "catapultTimer", 5f);
                        data.SetValue(boxed, idx);
                        Console.WriteLine("CACTUS_TUNE_CATAPULT idx=" + idx);
                        continue;
                    }

                    FieldInfo velField = FindField(bt, "vel");
                    FieldInfo speedField = FindField(bt, "speed");
                    if (velField == null) continue;
                    Vector2 vel = (Vector2)velField.GetValue(boxed);
                    float len = vel.Length();
                    float sp = speedField != null ? Convert.ToSingle(speedField.GetValue(boxed)) : 0f;
                    if (len < 40f && sp < 40f) continue;

                    const float factor = 0.05f;
                    if (len > 1f) velField.SetValue(boxed, vel * factor);
                    if (speedField != null && sp > 1f) speedField.SetValue(boxed, sp * factor);
                    data.SetValue(boxed, idx);
                    Console.WriteLine("CACTUS_TUNE_BULLET idx=" + idx + " len=" + len + " sp=" + sp);
                }
            }
            catch (Exception e) { Console.WriteLine("CACTUS_TUNE_BULLET_ERR " + e.Message); }
        }

        private void TunePlant(Node plant, double now)
        {
            ulong id = plant.GetInstanceId();
            Type type = plant.GetType();

            if (_plants.Add(id))
            {
                SetDamage(FindField(type, "_shell")?.GetValue(plant), 200000.0);
                SetDamage(FindField(type, "_spike")?.GetValue(plant), 10000.0);
                SetDamage(FindField(type, "_droneSpike")?.GetValue(plant), 10000.0);
                Console.WriteLine("CACTUS_TUNE_APPLY " + id);
            }

            if (!_next.TryGetValue(id, out double next) || now >= next)
            {
                FindField(type, "_mainReady")?.SetValue(plant, true);
                FindField(type, "_firing")?.SetValue(plant, false);
                _next[id] = now + 1.27;
            }
        }

        private void TuneShot(Node shot)
        {
            ulong id = shot.GetInstanceId();
            if (_shots.Contains(id)) return;

            Type type = shot.GetType();
            FieldInfo velField = FindField(type, "velocity");
            if (velField == null) return;
            Vector2 vel = (Vector2)velField.GetValue(shot);
            float len = vel.Length();
            if (len < 1f) return;

            const float factor = 0.02f;
            vel *= factor;
            velField.SetValue(shot, vel);

            FieldInfo speedField = FindField(type, "speed");
            if (speedField != null)
            {
                object sp = speedField.GetValue(shot);
                if (sp is float sf) speedField.SetValue(shot, sf * factor);
                else if (sp is double sd) speedField.SetValue(shot, sd * factor);
            }
            _shots.Add(id);
            Console.WriteLine("CACTUS_TUNE_SHOT " + id + " speed=" + len);
        }

        private static void SetDamage(object projectile, double value)
        {
            if (projectile == null) return;
            FindField(projectile.GetType(), "baseDamage")?.SetValue(projectile, value);
        }

        private static object ReadConfig(Type type, object host)
        {
            object cfg = FindField(type, "_config")?.GetValue(host);
            if (cfg == null)
            {
                PropertyInfo p = FindProperty(type, "config");
                if (p != null) cfg = p.GetValue(host);
            }
            return cfg;
        }

        private static string ReadName(object cfg)
        {
            if (cfg == null) return null;
            Type t = cfg.GetType();
            object v = FindField(t, "name")?.GetValue(cfg);
            if (v == null)
            {
                PropertyInfo p = FindProperty(t, "name");
                if (p != null) v = p.GetValue(cfg);
            }
            return v as string;
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

        private static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                PropertyInfo p = type.GetProperty(name, Flags);
                if (p != null) return p;
                type = type.BaseType;
            }
            return null;
        }
    }
}
