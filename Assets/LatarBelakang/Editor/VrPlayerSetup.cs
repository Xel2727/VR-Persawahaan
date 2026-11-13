using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace LatarBelakang.EditorTools
{
    /// <summary>
    /// Membuat player first-person VR (OpenXR) + fallback keyboard untuk uji di Editor.
    /// Menu: Tools > Latar Belakang > 6. Buat Player VR
    /// </summary>
    public static class VrPlayerSetup
    {
        const string ROOT = "Assets/LatarBelakang";
        const string PREFABS = ROOT + "/Prefabs";
        const string PREFAB_PATH = PREFABS + "/Player_VR.prefab";
        const string XR_FOLDER = "Assets/XR";

        [MenuItem("Tools/Latar Belakang/6. Buat Player VR", false, 6)]
        public static void CreatePlayer()
        {
            LatarBelakangSetup.EnsureLayer("Player", 11);
            SetupXR();
            BuildRig();
        }

        // ---------------------------------------------------------------- XR settings

        static void SetupXR()
        {
            try
            {
                XRGeneralSettingsPerBuildTarget buildTargetSettings = null;
                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out buildTargetSettings);

                if (buildTargetSettings == null)
                {
                    buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    if (!AssetDatabase.IsValidFolder(XR_FOLDER))
                        AssetDatabase.CreateFolder("Assets", "XR");
                    AssetDatabase.CreateAsset(buildTargetSettings, XR_FOLDER + "/XRGeneralSettings.asset");
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
                }

                var general = buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (general == null)
                {
                    general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                    general.name = "Standalone Settings";
                    buildTargetSettings.SetSettingsForBuildTarget(BuildTargetGroup.Standalone, general);
                    AssetDatabase.AddObjectToAsset(general, buildTargetSettings);
                }

                if (general.Manager == null)
                {
                    var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    manager.name = "Standalone Providers";
                    general.Manager = manager;
                    AssetDatabase.AddObjectToAsset(manager, buildTargetSettings);
                }

                general.InitManagerOnStart = true;
                EditorUtility.SetDirty(general);

                bool ok = XRPackageMetadataStore.AssignLoader(general.Manager,
                    "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Standalone);

                // refresh daftar feature OpenXR (butuh paket selesai diimpor)
                FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);

                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
                int enabledCount = 0;
                if (settings != null)
                {
                    foreach (var feature in settings.GetFeatures<OpenXRFeature>())
                    {
                        if (feature == null) continue;
                        string typeName = feature.GetType().Name;
                        if (typeName.IndexOf("ControllerProfile", System.StringComparison.Ordinal) >= 0)
                        {
                            feature.enabled = true;
                            enabledCount++;
                        }
                    }
                    EditorUtility.SetDirty(settings);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(string.Format("[LatarBelakang] XR setup: loader OpenXR={0}, interaction profile aktif={1}",
                    ok, enabledCount));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LatarBelakang] Setup XR gagal (paket XR mungkin belum selesai diimpor): " + e.Message);
            }
        }

        // ---------------------------------------------------------------- rig

        static void BuildRig()
        {
            int playerLayer = Mathf.Max(0, LayerMask.NameToLayer("Player"));

            var old = GameObject.Find("Player_VR");
            if (old != null) Object.DestroyImmediate(old);

            var spawn = GameObject.Find("SpawnPoint");
            Vector3 spawnPos = spawn != null ? spawn.transform.position : new Vector3(0f, 14.1f, -140f);

            var root = new GameObject("Player_VR");
            root.layer = playerLayer;
            root.transform.position = spawnPos;

            // badan player 2x tinggi normal (POV mata di 3.3 m)
            var cc = root.AddComponent<CharacterController>();
            cc.height = 3.6f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 1.8f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;

            var camOffset = new GameObject("CameraOffset");
            camOffset.layer = playerLayer;
            camOffset.transform.SetParent(root.transform, false);
            // offset tinggi (m): kamera + tangan naik bersama -> POV mata ~3.3 m di VR
            camOffset.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            // pakai kamera yang sudah ada supaya setting & tag tetap
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            camGo.transform.SetParent(camOffset.transform, false);
            // DesktopPlayerFallback menimpa ke eyeHeight (3.3 m) saat tanpa headset
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            camGo.transform.localRotation = Quaternion.identity;
            camGo.layer = playerLayer;

            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            if (camGo.GetComponent<VRHeadTracker>() == null) camGo.AddComponent<VRHeadTracker>();

            var head = camGo.transform;

            var locomotion = root.AddComponent<VRPlayerLocomotion>();
            locomotion.head = head;
            locomotion.walkSpeed = 4.8f;
            locomotion.sprintSpeed = 8.4f;
            locomotion.gravity = -24f;
            locomotion.jumpHeight = 1.8f;

            var desktop = root.AddComponent<DesktopPlayerFallback>();
            desktop.head = head;
            desktop.eyeHeight = 3.3f;
            desktop.walkSpeed = 8f;
            desktop.runSpeed = 15f;
            desktop.gravity = -24f;
            desktop.jumpHeight = 1.8f;

            var groundCheck = root.AddComponent<GroundCheck>();
            groundCheck.rayLength = 1.2f;

            var fall = root.AddComponent<FallRespawn>();
            fall.killY = -40f;
            fall.spawnPoint = spawn != null ? spawn.transform : null;
            fall.spawnPosition = spawnPos;

            var spawner = root.AddComponent<PlayerSpawnOnGround>();
            spawner.spawnPoint = spawn != null ? spawn.transform : null;

            CreateHand("LeftHand", XRNode.LeftHand, camOffset.transform, playerLayer);
            CreateHand("RightHand", XRNode.RightHand, camOffset.transform, playerLayer);

            // prefab
            if (!AssetDatabase.IsValidFolder(PREFABS)) AssetDatabase.CreateFolder(ROOT, "Prefabs");
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PREFAB_PATH, InteractionMode.AutomatedAction);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log(string.Format("[LatarBelakang] Player VR dibuat di {0} (prefab: {1})", spawnPos, PREFAB_PATH));
        }

        static void CreateHand(string name, XRNode node, Transform parent, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.layer = layer;
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.06f, 0.04f, 0.12f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = node == XRNode.LeftHand ? new Color(0.25f, 0.55f, 1f) : new Color(1f, 0.45f, 0.25f);
                mat.SetFloat("_Glossiness", 0.2f);
                renderer.sharedMaterial = mat;
            }

            var tracker = go.AddComponent<VRHandTracker>();
            tracker.node = node;
        }
    }
}
