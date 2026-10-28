using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LookismFighter.EditorTools
{
    /// <summary>
    /// One click: Tools > Lookism Fighter > Setup Fight Scene.
    /// Adds every animation to FighterAnimator, turns the "gun" character into Player 1, creates Player 2,
    /// builds a simple stage, and wires up the camera and RoundManager. Safe to run again.
    /// </summary>
    public static class SetupFightScene
    {
        const string ControllerPath = "Assets/Animations/FighterAnimator.controller";
        const string AnimFolder = "Assets/Animations";
        const string MatFolder = "Assets/Materials";

        [MenuItem("Tools/Lookism Fighter/Setup Fight Scene")]
        public static void Setup()
        {
            // ---------------------------------------------------------------- 1. Animator states
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) { EditorUtility.DisplayDialog("Lookism Fighter", "Could not find " + ControllerPath, "OK"); return; }
            var sm = ctrl.layers[0].stateMachine;
            var names = new List<string>();
            var lengths = new List<float>();
            var missing = new List<string>();
            foreach (var (stateName, speed) in MoveTable.AllStates)
            {
                string path = $"{AnimFolder}/{stateName}.fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) { missing.Add(stateName); continue; }
                var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName) ?? sm.AddState(stateName);
                st.motion = clip;
                st.speed = speed;
                st.writeDefaultValues = true;
                names.Add(stateName);
                lengths.Add(clip.length);
            }
            var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "fighting_idle");
            if (idle != null) sm.defaultState = idle;
            EditorUtility.SetDirty(ctrl);

            // ---------------------------------------------------------------- 2. Player 1 and Player 2
            var fighters = FindInOpenScene<Fighter>();
            GameObject p1 = fighters.FirstOrDefault(f => f.playerIndex == 0)?.gameObject;
            GameObject p2 = fighters.FirstOrDefault(f => f.playerIndex == 1)?.gameObject;
            if (p1 == null)
            {
                var anims = FindInOpenScene<Animator>();
                var a = anims.FirstOrDefault(x => x.runtimeAnimatorController == ctrl) ?? anims.FirstOrDefault(x => x.gameObject.name.ToLower().Contains("gun"));
                if (a == null) { EditorUtility.DisplayDialog("Lookism Fighter", "Drag Assets/Characters/gun.fbx into the scene first, then run this again.", "OK"); return; }
                p1 = a.gameObject;
            }
            Undo.RecordObject(p1, "Setup fight");
            p1.name = "Player1";

            if (p2 == null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(p1);
                p2 = source != null ? (GameObject)PrefabUtility.InstantiatePrefab(source, p1.scene) : Object.Instantiate(p1);
                Undo.RegisterCreatedObjectUndo(p2, "Create Player2");
                var oldFighter = p2.GetComponent<Fighter>();
                if (oldFighter != null) Object.DestroyImmediate(oldFighter);
            }
            p2.name = "Player2";

            foreach (var go in new[] { p1, p2 })
            {
                var an = GetOrAdd<Animator>(go);
                an.runtimeAnimatorController = ctrl;
                an.applyRootMotion = false;
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            p1.transform.SetPositionAndRotation(new Vector3(-1.5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            p2.transform.SetPositionAndRotation(new Vector3(1.5f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f));

            var f1 = GetOrAdd<Fighter>(p1);
            var f2 = GetOrAdd<Fighter>(p2);
            f1.playerIndex = 0; f2.playerIndex = 1;
            f1.displayName = "GUN"; f2.displayName = "GUN";
            f1.opponent = f2; f2.opponent = f1;
            f1.stateNames = f2.stateNames = names.ToArray();
            f1.stateLengths = f2.stateLengths = lengths.ToArray();
            EditorUtility.SetDirty(f1); EditorUtility.SetDirty(f2);

            TintPlayer2(p2);

            // ---------------------------------------------------------------- 3. Stage
            if (GameObject.Find("Stage") == null) BuildStage();

            // ---------------------------------------------------------------- 4. Camera + RoundManager
            var cam = Camera.main;
            if (cam != null)
            {
                var fc = GetOrAdd<FightCamera>(cam.gameObject);
                fc.fighterA = f1; fc.fighterB = f2;
                cam.transform.position = new Vector3(0f, 1.45f, -5.5f);
                cam.transform.LookAt(new Vector3(0f, 1.05f, 0f));
                cam.fieldOfView = 35f;
                EditorUtility.SetDirty(fc);
            }
            var rm = FindInOpenScene<RoundManager>().FirstOrDefault();
            if (rm == null) rm = new GameObject("RoundManager").AddComponent<RoundManager>();
            rm.player1 = f1; rm.player2 = f2;
            EditorUtility.SetDirty(rm);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(p1.scene);
            EditorSceneManager.SaveScene(p1.scene);

            string msg = "Fight scene is ready. Press Play!\n\nWASD move · U I punch · J K kick · hold back to block.\nF1 VS CPU · F2 2 Players · F3 Training · H help";
            if (missing.Count > 0) msg += "\n\nMissing animations (not found in Assets/Animations): " + string.Join(", ", missing);
            EditorUtility.DisplayDialog("Lookism Fighter", msg, "OK");
        }

        // Finds components (including inactive ones) in the open scene without the deprecated Find* APIs.
        static T[] FindInOpenScene<T>() where T : Component
        {
            var scene = EditorSceneManager.GetActiveScene();
            return scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static Shader LitShader() { var sh = Shader.Find("Universal Render Pipeline/Lit"); return sh != null ? sh : Shader.Find("Standard"); }

        static Material MakeMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets", "Materials");
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(LitShader()); AssetDatabase.CreateAsset(m, path); }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void TintPlayer2(GameObject p2)
        {
            foreach (var r in p2.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null || src.name.EndsWith("_P2")) continue;
                    if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets", "Materials");
                    string path = $"{MatFolder}/{src.name}_P2.mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (copy == null)
                    {
                        copy = new Material(src) { name = src.name + "_P2" };
                        Color tint = new Color(1f, 0.72f, 0.72f);
                        if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", copy.GetColor("_BaseColor") * tint);
                        else copy.color *= tint;
                        AssetDatabase.CreateAsset(copy, path);
                    }
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
        }

        static void BuildStage()
        {
            var stage = new GameObject("Stage");
            Undo.RegisterCreatedObjectUndo(stage, "Build stage");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(stage.transform);
            floor.transform.localScale = new Vector3(3f, 1f, 1.4f);
            floor.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Floor", new Color(0.24f, 0.25f, 0.28f));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Parapet";
            wall.transform.SetParent(stage.transform);
            wall.transform.position = new Vector3(0f, 0.45f, 6f);
            wall.transform.localScale = new Vector3(30f, 0.9f, 0.4f);
            wall.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Concrete", new Color(0.33f, 0.33f, 0.36f));

            var railMat = MakeMaterial("Rail", new Color(0.7f, 0.72f, 0.76f));
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Rail";
            rail.transform.SetParent(stage.transform);
            rail.transform.position = new Vector3(0f, 1.6f, 6f);
            rail.transform.localScale = new Vector3(30f, 0.06f, 0.06f);
            rail.GetComponent<Renderer>().sharedMaterial = railMat;
            for (float x = -15f; x <= 15f; x += 2f)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post";
                post.transform.SetParent(stage.transform);
                post.transform.position = new Vector3(x, 1.25f, 6f);
                post.transform.localScale = new Vector3(0.06f, 0.75f, 0.06f);
                post.GetComponent<Renderer>().sharedMaterial = railMat;
            }

            var lineMat = MakeMaterial("EdgeLine", new Color(1f, 0.8f, 0.2f));
            foreach (float x in new[] { -7f, 7f })
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "StageEdge";
                line.transform.SetParent(stage.transform);
                line.transform.position = new Vector3(x, 0.005f, 0f);
                line.transform.localScale = new Vector3(0.1f, 0.01f, 4f);
                line.GetComponent<Renderer>().sharedMaterial = lineMat;
                Object.DestroyImmediate(line.GetComponent<Collider>());
            }
        }
    }
}
