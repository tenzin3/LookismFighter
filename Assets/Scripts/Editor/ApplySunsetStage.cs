using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LookismFighter.EditorTools
{
    /// <summary>
    /// Tools > Lookism Fighter > Apply Rooftop Sunset Stage
    /// Puts Assets/Stage/rooftop_sunset.png behind the fight as a giant backdrop, builds a wet-concrete 3D floor
    /// that meets the painted railing, and sets sunset lighting. Safe to run again.
    /// </summary>
    public static class ApplySunsetStage
    {
        const string Folder = "Assets/Stage";
        const string BackdropPath = Folder + "/rooftop_sunset.png";
        const string FloorTexPath = Folder + "/wet_concrete.png";

        // Where the painted floor meets the parapet wall in the image, measured from the bottom (0..1).
        const float ParapetBaseFromBottom = 0.392f;
        const float BackdropZ = 30f;          // how far behind the fighters the backdrop stands
        const float BackdropWidth = 48f;      // metres; height follows the image's aspect ratio
        const float FloorNearZ = -9f;         // floor extends from here (in front of the camera's view) ...
        const float FloorWidth = 60f;

        [MenuItem("Tools/Lookism Fighter/Apply Rooftop Sunset Stage")]
        public static void Apply()
        {
            // ---------------------------------------------------------------- backdrop texture
            var backTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);
            if (backTex == null) { EditorUtility.DisplayDialog("Lookism Fighter", "Put the background image at " + BackdropPath, "OK"); return; }
            var imp = (TextureImporter)AssetImporter.GetAtPath(BackdropPath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = 4096;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
                backTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);
            }

            var scene = EditorSceneManager.GetActiveScene();
            var stage = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Stage");
            if (stage == null) { stage = new GameObject("Stage"); Undo.RegisterCreatedObjectUndo(stage, "Stage"); }

            // hide the old placeholder parapet, rail, posts and edge lines (the painting has its own railing)
            foreach (Transform child in stage.transform)
                if (child.name == "Parapet" || child.name == "Rail" || child.name == "Post" || child.name == "StageEdge")
                    child.gameObject.SetActive(false);

            // ---------------------------------------------------------------- backdrop quad
            var backdrop = FindOrCreate(stage.transform, "Backdrop", PrimitiveType.Quad);
            float aspect = backTex.width / (float)backTex.height;
            float h = BackdropWidth / aspect;
            backdrop.transform.position = new Vector3(0f, h * (0.5f - ParapetBaseFromBottom), BackdropZ);
            backdrop.transform.rotation = Quaternion.identity;               // quad faces -Z, toward the camera
            backdrop.transform.localScale = new Vector3(BackdropWidth, h, 1f);
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var backMat = MakeMat("Backdrop", unlit != null ? unlit : Shader.Find("Unlit/Texture"));
            backMat.mainTexture = backTex;
            if (backMat.HasProperty("_BaseMap")) backMat.SetTexture("_BaseMap", backTex);
            if (backMat.HasProperty("_BaseColor")) backMat.SetColor("_BaseColor", Color.white);
            var br = backdrop.GetComponent<Renderer>();
            br.sharedMaterial = backMat;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.receiveShadows = false;
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());

            // ---------------------------------------------------------------- wet concrete floor
            var floorTex = MakeFloorTexture();
            var floor = stage.transform.Find("Floor") ? stage.transform.Find("Floor").gameObject : FindOrCreate(stage.transform, "Floor", PrimitiveType.Plane);
            float depth = BackdropZ - FloorNearZ;
            floor.transform.position = new Vector3(0f, 0f, (BackdropZ + FloorNearZ) * 0.5f);
            floor.transform.rotation = Quaternion.identity;
            floor.transform.localScale = new Vector3(FloorWidth / 10f, 1f, depth / 10f); // Unity plane is 10 m
            var floorMat = MakeMat("WetConcrete", Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            floorMat.mainTexture = floorTex;
            if (floorMat.HasProperty("_BaseMap")) floorMat.SetTexture("_BaseMap", floorTex);
            floorMat.mainTextureScale = new Vector2(FloorWidth / 4f, depth / 4f);   // one texture tile = 4 m (2x2 slabs)
            if (floorMat.HasProperty("_BaseMap")) floorMat.SetTextureScale("_BaseMap", floorMat.mainTextureScale);
            var tint = new Color(0.52f, 0.47f, 0.52f);   // dark, slightly warm wet concrete
            if (floorMat.HasProperty("_BaseColor")) floorMat.SetColor("_BaseColor", tint); else floorMat.color = tint;
            if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.92f);
            if (floorMat.HasProperty("_SmoothnessTextureChannel")) { floorMat.SetFloat("_SmoothnessTextureChannel", 1f); floorMat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); } // puddles (alpha) shinier than dry concrete
            if (floorMat.HasProperty("_Metallic")) floorMat.SetFloat("_Metallic", 0.15f);
            if (floorMat.HasProperty("_EnvironmentReflections")) floorMat.SetFloat("_EnvironmentReflections", 1f);
            if (floorMat.HasProperty("_SpecularHighlights")) floorMat.SetFloat("_SpecularHighlights", 1f);
            var fr = floor.GetComponent<Renderer>();
            fr.sharedMaterial = floorMat;
            fr.receiveShadows = true;
            floor.SetActive(true);

            // ---------------------------------------------------------------- reflections of the city in the wet floor
            var probeGo = stage.transform.Find("CityReflections") ? stage.transform.Find("CityReflections").gameObject : null;
            if (probeGo == null) { probeGo = new GameObject("CityReflections"); probeGo.transform.SetParent(stage.transform, false); Undo.RegisterCreatedObjectUndo(probeGo, "Probe"); }
            probeGo.transform.position = new Vector3(0f, 1.2f, 0f);
            var probe = probeGo.GetComponent<ReflectionProbe>();
            if (probe == null) probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(70f, 30f, 80f);
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.farClipPlane = 120f;
            probe.intensity = 1.25f;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.36f, 0.37f, 0.6f);
            probe.cullingMask = ~0;

            // ---------------------------------------------------------------- sunset lighting
            var sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.name != "FillLight");
            if (sun != null)
            {
                Undo.RecordObject(sun, "Sunset light");
                Undo.RecordObject(sun.transform, "Sunset light");
                sun.color = new Color(1f, 0.66f, 0.46f);
                sun.intensity = 1.9f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(12f, 200f, 0f);    // low sun behind the stage on the right, shining toward the camera
            }
            // soft purple fill from the camera side so the fighters' faces aren't in shadow
            var fillGo = stage.transform.Find("FillLight") ? stage.transform.Find("FillLight").gameObject : null;
            if (fillGo == null) { fillGo = new GameObject("FillLight"); fillGo.transform.SetParent(stage.transform, false); Undo.RegisterCreatedObjectUndo(fillGo, "Fill"); }
            var fill = fillGo.GetComponent<Light>();
            if (fill == null) fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.78f, 0.72f, 1f);
            fill.intensity = 0.9f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(28f, 25f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.47f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.46f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.17f, 0.22f);
            RenderSettings.fog = false;

            var cam = Camera.main;
            if (cam != null)
            {
                Undo.RecordObject(cam, "Camera background");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.37f, 0.6f);
                if (cam.farClipPlane < 150f) cam.farClipPlane = 150f;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Lookism Fighter", "Rooftop sunset stage applied. Press Play!", "OK");
        }

        static GameObject FindOrCreate(Transform parent, string name, PrimitiveType type)
        {
            var t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go;
        }

        static Material MakeMat(string name, Shader shader)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Stage");
            string path = $"{Folder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Generates a 1024px tile: 2x2 concrete slabs with dark seams, a painted white line, stains and puddle patches.</summary>
        static Texture2D MakeFloorTexture()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Stage");
            const int N = 1024;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            var rng = new System.Random(42);
            float r01() => (float)rng.NextDouble();

            // stains / puddles as soft blobs
            var blobs = Enumerable.Range(0, 18).Select(_ => (x: r01() * N, y: r01() * N, rad: 40f + r01() * 140f, dark: r01() < 0.6f)).ToArray();

            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 0.6f + Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.3f + r01() * 0.1f;
                float v = 0.30f + n * 0.12f;
                float wet = 0.55f + n * 0.15f;                       // dry concrete: semi-gloss
                foreach (var b in blobs)
                {
                    float dx = Mathf.Repeat(x - b.x + N / 2f, N) - N / 2f, dy = Mathf.Repeat(y - b.y + N / 2f, N) - N / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / b.rad;
                    if (d < 1f)
                    {
                        if (b.dark) { v *= Mathf.Lerp(0.55f, 1f, d * d); wet = Mathf.Max(wet, Mathf.Lerp(0.98f, 0.6f, d * d * d)); } // puddle: darker + mirror-like
                        else v *= Mathf.Lerp(1.12f, 1f, d);
                    }
                }
                // slab seams every half tile
                int sx = x % (N / 2), sy = y % (N / 2);
                if (sx < 4 || sy < 4) { v *= 0.45f; wet = 0.35f; }
                // painted white line across the slabs (runs along the fighting direction)
                if (y >= N / 2 - 10 && y <= N / 2 + 10 && x % (N / 2) > 30 && r01() > 0.08f) v = Mathf.Lerp(v, 0.82f, 0.8f);
                px[y * N + x] = new Color(v * 0.96f, v * 0.95f, v * 1.02f, wet);
            }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(FloorTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(FloorTexPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(FloorTexPath);
            if (imp != null) { imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 8; imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.alphaIsTransparency = false; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexPath);
        }
    }
}
