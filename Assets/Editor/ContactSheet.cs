using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DjVisuals.EditorTools
{
    /// <summary>
    /// Renders every model prefab under a folder onto one PNG, to pick assets by eye in a headless run:
    ///   Unity.exe -batchmode -projectPath . -executeMethod DjVisuals.EditorTools.ContactSheet.Run -djsheet &lt;assetFolder&gt; &lt;outPng&gt;
    /// </summary>
    public static class ContactSheet
    {
        public static void Run()
        {
            string folder = null, outPng = null;
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 2; i++) if (a[i] == "-djsheet") { folder = a[i + 1]; outPng = a[i + 2]; }
            if (folder == null) { Debug.LogError("missing -djsheet args"); EditorApplication.Exit(1); return; }

            UrpSetup.Ensure();
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            var guids = AssetDatabase.FindAssets("", new[] { folder });
            var paths = new List<string>();
            foreach (var g in guids)
            {
                var pth = AssetDatabase.GUIDToAssetPath(g);
                var ext = Path.GetExtension(pth).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".glb" && ext != ".gltf" && ext != ".obj" && ext != ".prefab") continue;
                if (ext == ".glb") AssetDatabase.ImportAsset(pth, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var main = AssetDatabase.LoadMainAssetAtPath(pth);
                Debug.Log("[DjVisuals] candidate " + pth + " -> " + (main == null ? "null" : main.GetType().Name));
                if (main is GameObject) paths.Add(pth);
            }
            paths.Sort();
            Debug.Log("[DjVisuals] sheet models: " + paths.Count);

            var light = new GameObject("L").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35, -40, 0);
            light.intensity = 1.4f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.5f);

            const int cell = 420;
            int cols = 4, rows = Mathf.Max(1, (paths.Count + cols - 1) / cols);
            var sheet = new Texture2D(cell * cols, cell * rows, TextureFormat.RGBA32, false);
            var rt = new RenderTexture(cell, cell, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("C");
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.13f);
            cam.fieldOfView = 30f;

            for (int i = 0; i < paths.Count; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                // Quaternius ships use a palette texture next to the FBX (…/Textures/*.png); apply the first one.
                var texDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(paths[i])), "Textures").Replace((char)92, (char)47);
                if (AssetDatabase.IsValidFolder(texDir))
                {
                    var tg = AssetDatabase.FindAssets("t:Texture2D", new[] { texDir });
                    if (tg.Length > 0)
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(tg[0]));
                        foreach (var rr in inst.GetComponentsInChildren<Renderer>())
                        {
                            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            mat.SetTexture("_BaseMap", tex);
                            mat.SetColor("_BaseColor", Color.white);
                            mat.SetFloat("_Smoothness", 0.4f);
                            rr.sharedMaterial = mat;
                        }
                    }
                }
                foreach (var t in inst.GetComponentsInChildren<Transform>())
                {
                    var tr = t.GetComponent<Renderer>();
                    Debug.Log("[DjVisuals] node " + System.IO.Path.GetFileNameWithoutExtension(paths[i]) + " / " + t.name + (tr != null ? "  boundsCenter " + tr.bounds.center.ToString("F2") + " size " + tr.bounds.size.ToString("F2") : ""));
                }
                if (paths[i].Contains("Executioner") || paths[i].Contains("Spitfire")) LogRear(inst, Path.GetFileNameWithoutExtension(paths[i]));
                var rends = inst.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) { UnityEngine.Object.DestroyImmediate(inst); continue; }
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float rad = Mathf.Max(0.001f, b.extents.magnitude);
                camGo.transform.position = b.center + new Vector3(0.6f, 0.45f, -1f).normalized * rad * 3.1f;
                camGo.transform.LookAt(b.center);
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tile = new Texture2D(cell, cell, TextureFormat.RGBA32, false);
                tile.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
                RenderTexture.active = prev;
                int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell;
                sheet.SetPixels(cx, cy, cell, cell, tile.GetPixels());
                Debug.Log("[DjVisuals] sheet " + i + " = " + paths[i] + "  size " + b.size.ToString("F2"));
                UnityEngine.Object.DestroyImmediate(tile);
                UnityEngine.Object.DestroyImmediate(inst);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outPng));
            File.WriteAllBytes(outPng, sheet.EncodeToPNG());
            EditorApplication.Exit(0);
        }

        static void LogRear(GameObject inst, string name)
        {
            var pts = new List<Vector3>();
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                foreach (var v in mf.sharedMesh.vertices) pts.Add(mf.transform.TransformPoint(v));
            float zmin = float.MaxValue;
            foreach (var v in pts) zmin = Mathf.Min(zmin, v.z);
            var cells = new Dictionary<Vector2Int, List<Vector3>>();
            foreach (var v in pts)
            {
                if (v.z > zmin + 1.2f) continue;
                var k = new Vector2Int(Mathf.RoundToInt(v.x / 1.2f), Mathf.RoundToInt(v.y / 1.2f));
                if (!cells.ContainsKey(k)) cells[k] = new List<Vector3>();
                cells[k].Add(v);
            }
            Debug.Log("[DjVisuals] rear " + name + " zmin " + zmin.ToString("F2") + " cells " + cells.Count);
            foreach (var kv in cells)
            {
                var c = Vector3.zero; float zlo = float.MaxValue;
                foreach (var v in kv.Value) { c += v; zlo = Mathf.Min(zlo, v.z); }
                c /= kv.Value.Count;
                Debug.Log("[DjVisuals] rear " + name + " cell " + kv.Key + " n=" + kv.Value.Count + " centroid " + c.ToString("F2") + " zlo " + zlo.ToString("F2"));
            }
        }
    }
}
