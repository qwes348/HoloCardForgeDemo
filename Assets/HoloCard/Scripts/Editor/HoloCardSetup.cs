using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoloCard.Editor
{
    /// <summary>
    /// 데모 씬과 에셋을 한 번에 찍어낸다. 프리셋 5종, 머티리얼, 카드 프리팹까지.
    /// </summary>
    public static class HoloCardSetup
    {
        const string Root         = "Assets/HoloCard";
        const string MaterialsDir = Root + "/Materials";
        const string PresetsDir   = Root + "/Presets";
        const string PrefabsDir   = Root + "/Prefabs";
        const string ScenesDir    = Root + "/Scenes";
        const string TexturesDir  = Root + "/Textures";

        const string ScenePath = ScenesDir + "/HoloCardDemo.unity";

        [MenuItem("Tools/Holo Card/Create Demo Scene", false, 0)]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();
            ConfigureSampleTextures();
            HoloCardPreset[] presets = CreatePresets();
            Material cardMat = CreateCardMaterial();
            Material bodyMat = CreateBodyMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.GetComponent<Camera>();
            // 망원. 광각으로 잡으면 카드 모서리의 시야각이 커져서 그레이징 부스트가
            // 과하게 걸린다. 실제 카드 촬영도 망원으로 하는 이유와 같다.
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2.4f), Quaternion.identity);
            cam.clearFlags = CameraClearFlags.SolidColor;
            // 아티팩트 스테이지와 같은 거의 검은 배경. 배경이 밝으면 홀로가 대비를 잃는다.
            cam.backgroundColor = new Color(0.0045f, 0.005f, 0.009f, 1f);
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;

            // ── 조명. 카드 옆면과 그림자가 살아나도록 살짝 비스듬히.
            var lightGo = new GameObject("Key Light", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(38f, -155f, 0f);

            var fillGo = new GameObject("Fill Light", typeof(Light));
            var fill = fillGo.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.42f, 0.55f, 0.9f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(-15f, 35f, 0f);

            // ── 배경. 시트 반사가 읽히려면 뒤에 뭔가 있어야 한다.
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            backdrop.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.6f), Quaternion.Euler(0f, 180f, 0f));
            backdrop.transform.localScale = new Vector3(6f, 6f, 1f);
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            backdrop.GetComponent<MeshRenderer>().sharedMaterial = CreateBackdropMaterial();

            // ── 카드
            GameObject card = BuildCard(cardMat, bodyMat, presets[0]);
            card.transform.position = Vector3.zero;

            // ── 리플렉션 프로브. _EnvIntensity 를 올리면 여기서 반사를 가져간다.
            var probeGo = new GameObject("Reflection Probe", typeof(ReflectionProbe));
            var probe = probeGo.GetComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.size = new Vector3(8f, 8f, 8f);
            probeGo.transform.position = Vector3.zero;
            probe.RenderProbe();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.045f, 0.055f, 0.09f);
            RenderSettings.ambientEquatorColor = new Color(0.025f, 0.030f, 0.05f);
            RenderSettings.ambientGroundColor  = new Color(0.008f, 0.008f, 0.016f);

            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = card;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log($"[Holo Card] 데모 씬 생성 완료: {ScenePath}\n" +
                      "Play 를 누르고 카드 위에서 마우스를 움직여 보세요. 손을 떼면 자동 회전합니다.");
        }

        /// <summary>
        /// Textures/ 아래에서 &lt;이름&gt;.png + &lt;이름&gt;_Depth + &lt;이름&gt;_Foil 삼종 세트를 찾아
        /// 카드를 한 줄로 늘어놓은 씬을 만든다. 베이커로 구운 카드가 몇 장이든 그대로 붙는다.
        /// </summary>
        [MenuItem("Tools/Holo Card/Create Card Gallery Scene", false, 1)]
        public static void CreateGalleryScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();
            HoloCardPreset[] presets = CreatePresets();

            var sets = FindCardSets();
            if (sets.Count == 0)
            {
                EditorUtility.DisplayDialog("Holo Card",
                    "Depth·Foil 이 함께 있는 카드 아트를 찾지 못했습니다.\n" +
                    "Tools > Holo Card > Depth and Foil Baker 로 먼저 구우세요.", "확인");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 6장 이하면 살짝 겹치는 부채꼴 한 줄, 그보다 많으면 격자로 접는다.
            // 한 줄로 밀어 넣으면 카드가 너무 작아진다.
            bool fan = sets.Count <= 6;
            int perRow = fan ? sets.Count : Mathf.CeilToInt(sets.Count / 2f);
            int rows   = Mathf.CeilToInt(sets.Count / (float)perRow);

            float spacingX = fan ? 0.46f : 0.72f;
            float spacingY = 1.02f;

            float spanW = spacingX * (perRow - 1) + 0.70f;
            float spanH = spacingY * (rows - 1) + 0.92f;

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.0045f, 0.005f, 0.009f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;

            // 배치 전체가 들어오도록 거리를 역산한다.
            float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * (16f / 9f);
            float dist = Mathf.Max(spanW * 0.5f / halfW, spanH * 0.5f / halfH) + 0.4f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -dist), Quaternion.identity);

            var lightGo = new GameObject("Key Light", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(38f, -155f, 0f);

            var fillGo = new GameObject("Fill Light", typeof(Light));
            var fill = fillGo.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.42f, 0.55f, 0.9f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(-15f, 35f, 0f);

            Material bodyMat = CreateBodyMaterial();
            var root = new GameObject("Cards");

            for (int i = 0; i < sets.Count; i++)
            {
                CardSet set = sets[i];
                // 다운로더가 카드 종류에 맞는 프리셋으로 이미 만들어 뒀으면 그대로 쓴다.
                // 새로 만들 때만 Vintage Print 를 기본값으로 넣는다.
                Material mat = CreateCardMaterial(set.name, set.art, HoloCardPreset.Builtin.VintagePrint);

                // 컨트롤러 preset 은 비운다. 채워 두면 Awake 에서 머티리얼 값을
                // 통째로 덮어써서 카드마다 다른 룩이 다 같아진다.
                GameObject card = BuildCard(mat, bodyMat, null);
                card.name = set.name;
                card.transform.SetParent(root.transform, false);

                // 텍스처 비율을 따라가 카드가 찌그러지지 않게
                var mesh = card.GetComponent<HoloCardMesh>();
                mesh.height = 0.88f;
                mesh.width  = 0.88f * set.aspect;
                mesh.Rebuild();

                int row = i / perRow;
                int col = i % perRow;
                int inRow = Mathf.Min(perRow, sets.Count - row * perRow);

                // 가장자리 카드일수록 뒤로 물리고 안쪽으로 돌린다.
                // 겹칠 때 가운데 카드가 앞에 오도록 z 는 |x| 에 비례해 커진다(카메라는 -Z).
                float x = (col - (inRow - 1) * 0.5f) * spacingX;
                float y = ((rows - 1) * 0.5f - row) * spacingY;
                card.transform.localPosition = new Vector3(x, y - Mathf.Abs(x) * 0.035f, Mathf.Abs(x) * 0.14f);
                card.transform.localRotation = Quaternion.Euler(0f, -x * 15f, 0f);
            }

            // 카드를 클릭하면 크게 볼 수 있게 한다.
            var inspectorGo = new GameObject("Card Inspector");
            var inspector = inspectorGo.AddComponent<HoloCardInspector>();
            inspector.targetCamera = cam;
            inspector.focusDistance = 1.15f;
            inspector.focusHeightRatio = 0.84f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.045f, 0.055f, 0.09f);
            RenderSettings.ambientEquatorColor = new Color(0.025f, 0.030f, 0.05f);
            RenderSettings.ambientGroundColor  = new Color(0.008f, 0.008f, 0.016f);

            Directory.CreateDirectory(ScenesDir);
            string path = ScenesDir + "/HoloCardGallery.unity";
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();

            Selection.activeGameObject = root;
            Debug.Log($"[Holo Card] 갤러리 씬 생성 완료 ({sets.Count}장): {path}");
        }

        struct CardSet
        {
            public string name;
            public Texture2D art, depth, foil;
            public float aspect;
        }

        static System.Collections.Generic.List<CardSet> FindCardSets()
        {
            var result = new System.Collections.Generic.List<CardSet>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesDir });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_Depth") || name.EndsWith("_Foil")) continue;

                string dir = Path.GetDirectoryName(path).Replace('\\', '/');
                var depth = LoadAny(dir, name + "_Depth");
                var foil  = LoadAny(dir, name + "_Foil");
                if (depth == null || foil == null) continue;

                var art = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (art == null) continue;

                // 비율은 반드시 원본 파일에서 읽는다. 임포트된 Texture2D 의 크기는
                // npotScale·maxTextureSize 에 흔들려서 카드가 홀쭉해진다.
                result.Add(new CardSet
                {
                    name = name,
                    art = art,
                    depth = depth,
                    foil = foil,
                    aspect = HoloCardBaker.SourceAspect(art),
                });
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        static Texture2D LoadAny(string dir, string baseName)
        {
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg", ".tga", ".psd" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{baseName}{ext}");
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// 카드 한 장의 머티리얼을 만들거나 갱신한다. Depth·Foil 은 아트 옆의
        /// &lt;이름&gt;_Depth / &lt;이름&gt;_Foil 을 자동으로 찾는다.
        ///
        /// 이미 있는 머티리얼이면 텍스처만 다시 물리고 프리셋 값은 건드리지 않는다.
        /// 사용자가 인스펙터에서 맞춰 둔 룩을 덮지 않기 위해서다.
        /// </summary>
        public static Material CreateCardMaterial(string name, Texture2D art, HoloCardPreset.Builtin preset)
        {
            var shader = Shader.Find("Holo/Holographic Card (3D)");
            if (shader == null)
            {
                Debug.LogError("[Holo Card] 셰이더 'Holo/Holographic Card (3D)' 를 찾을 수 없습니다.");
                return null;
            }

            Directory.CreateDirectory(MaterialsDir);
            string path = $"{MaterialsDir}/HoloCard_{name}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(art)).Replace('\\', '/');
            mat.SetTexture(HoloCardIDs.BaseMap,  art);
            mat.SetTexture(HoloCardIDs.DepthMap, LoadAny(dir, name + "_Depth"));
            mat.SetTexture(HoloCardIDs.FoilMask, LoadAny(dir, name + "_Foil"));

            if (isNew)
            {
                var p = ScriptableObject.CreateInstance<HoloCardPreset>();
                p.LoadBuiltin(preset);
                p.ApplyTo(mat);
                Object.DestroyImmediate(p);

                mat.SetFloat(HoloCardIDs.ViewBlend, 0.35f);
                mat.SetVector(HoloCardIDs.VirtualView, new Vector4(0f, 0f, 1f, 0f));
                mat.SetVector(HoloCardIDs.PointerUV, new Vector4(0.5f, 0.5f, 0f, 0f));
                mat.SetFloat(HoloCardIDs.Tilt, 0f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        [MenuItem("Tools/Holo Card/Create UI Demo Scene", false, 2)]
        public static void CreateUIDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();
            ConfigureSampleTextures();
            HoloCardPreset[] presets = CreatePresets();
            Material uiMat = CreateUIMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.GetComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.0045f, 0.005f, 0.009f, 1f);
            cam.fieldOfView = 32f;

            // Screen Space - Camera + 원근. Overlay 로 두면 카드가 기울어도 평면으로 보인다.
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
                                                   typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Image 는 스프라이트 에셋이 필요하지만 RawImage 는 텍스처만으로 된다.
            // 셰이더는 _BaseMap 을 쓰므로 RawImage 의 텍스처는 지오메트리용일 뿐이다.
            var cardGo = new GameObject("Holo Card (UI)", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            cardGo.transform.SetParent(canvasGo.transform, false);

            var rect = cardGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(504f, 704f);   // 63:88 비율
            rect.anchoredPosition = Vector2.zero;

            var raw = cardGo.GetComponent<UnityEngine.UI.RawImage>();
            raw.texture = LoadTexture("Sample_Art.jpg");
            raw.material = uiMat;

            var controller = cardGo.AddComponent<HoloCardController>();
            controller.targetGraphic = raw;
            controller.targetCamera = cam;
            controller.source = HoloCardController.TiltSource.PointerHover;
            controller.fallbackToAutoDemo = true;
            controller.rotateTransform = true;
            controller.maxTiltAngle = 16f;
            controller.popDistance = 0f;   // UI 는 캔버스 평면에 붙어 있어 띄우지 않는다
            controller.preset = presets[0];

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                                          typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            Directory.CreateDirectory(ScenesDir);
            string path = ScenesDir + "/HoloCardUIDemo.unity";
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();

            Selection.activeGameObject = cardGo;
            Debug.Log($"[Holo Card] UI 데모 씬 생성 완료: {path}");
        }

        [MenuItem("Tools/Holo Card/Create Card Prefab", false, 3)]
        public static void CreateCardPrefabOnly()
        {
            EnsureFolders();
            ConfigureSampleTextures();
            HoloCardPreset[] presets = CreatePresets();
            GameObject card = BuildCard(CreateCardMaterial(), CreateBodyMaterial(), presets[0]);

            Directory.CreateDirectory(PrefabsDir);
            string path = AssetDatabase.GenerateUniqueAssetPath(PrefabsDir + "/HoloCard.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(card, path, InteractionMode.UserAction);
            Selection.activeGameObject = card;
            Debug.Log($"[Holo Card] 프리팹 생성: {path}");
        }

        // ── 조립 ─────────────────────────────────────────────────────────

        static GameObject BuildCard(Material cardMat, Material bodyMat, HoloCardPreset preset)
        {
            var go = new GameObject("Holo Card", typeof(MeshFilter), typeof(MeshRenderer));

            // 확대 보기의 클릭 판정용. HoloCardMesh 가 Rebuild 때 크기를 맞춰 준다.
            go.AddComponent<BoxCollider>();

            var mesh = go.AddComponent<HoloCardMesh>();
            mesh.width  = 0.63f;
            mesh.height = 0.88f;
            mesh.thickness = 0.012f;
            mesh.cornerRadius = 0.03f;
            mesh.cornerSegments = 6;
            mesh.Rebuild();

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { cardMat, bodyMat };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var controller = go.AddComponent<HoloCardController>();
            controller.targetRenderer = renderer;
            controller.source = HoloCardController.TiltSource.PointerHover;
            controller.fallbackToAutoDemo = true;
            controller.rotateTransform = true;
            controller.maxTiltAngle = 16f;
            controller.popDistance = 0.06f;
            controller.preset = preset;

            return go;
        }

        // ── 에셋 ─────────────────────────────────────────────────────────

        static void EnsureFolders()
        {
            foreach (string dir in new[] { MaterialsDir, PresetsDir, PrefabsDir, ScenesDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        static HoloCardPreset[] CreatePresets()
        {
            var kinds = (HoloCardPreset.Builtin[])System.Enum.GetValues(typeof(HoloCardPreset.Builtin));
            var result = new HoloCardPreset[kinds.Length];

            for (int i = 0; i < kinds.Length; i++)
            {
                string path = $"{PresetsDir}/{kinds[i]}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<HoloCardPreset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<HoloCardPreset>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.LoadBuiltin(kinds[i]);
                EditorUtility.SetDirty(asset);
                result[i] = asset;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        static Material CreateCardMaterial()
        {
            const string path = MaterialsDir + "/HoloCard_Sample.mat";
            var shader = Shader.Find("Holo/Holographic Card (3D)");
            if (shader == null)
            {
                Debug.LogError("[Holo Card] 셰이더 'Holo/Holographic Card (3D)' 를 찾을 수 없습니다. 컴파일 오류를 먼저 확인하세요.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            mat.SetTexture(HoloCardIDs.BaseMap,  LoadTexture("Sample_Art.jpg"));
            mat.SetTexture(HoloCardIDs.DepthMap, LoadTexture("Sample_Depth.png"));
            mat.SetTexture(HoloCardIDs.FoilMask, LoadTexture("Sample_Foil.png"));

            var standard = ScriptableObject.CreateInstance<HoloCardPreset>();
            standard.LoadBuiltin(HoloCardPreset.Builtin.StandardHolo);
            standard.ApplyTo(mat);
            Object.DestroyImmediate(standard);

            // 컨트롤러가 런타임에 덮어쓰는 값들. 에디터 프리뷰용 중립값으로 되돌린다.
            mat.SetFloat(HoloCardIDs.ViewBlend, 0.35f);
            mat.SetVector(HoloCardIDs.VirtualView, new Vector4(0f, 0f, 1f, 0f));
            mat.SetVector(HoloCardIDs.PointerUV, new Vector4(0.5f, 0.5f, 0f, 0f));
            mat.SetFloat(HoloCardIDs.Tilt, 0f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Material CreateUIMaterial()
        {
            const string path = MaterialsDir + "/HoloCard_UI.mat";
            var shader = Shader.Find("Holo/Holographic Card (UI)");
            if (shader == null)
            {
                Debug.LogError("[Holo Card] 셰이더 'Holo/Holographic Card (UI)' 를 찾을 수 없습니다.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            mat.SetTexture(HoloCardIDs.BaseMap,  LoadTexture("Sample_Art.jpg"));
            mat.SetTexture(HoloCardIDs.DepthMap, LoadTexture("Sample_Depth.png"));
            mat.SetTexture(HoloCardIDs.FoilMask, LoadTexture("Sample_Foil.png"));

            var standard = ScriptableObject.CreateInstance<HoloCardPreset>();
            standard.LoadBuiltin(HoloCardPreset.Builtin.StandardHolo);
            standard.ApplyTo(mat);
            Object.DestroyImmediate(standard);

            // UI 는 캔버스 모드에 따라 카메라 시선이 무의미할 수 있어 컨트롤러 시선만 쓴다.
            mat.SetFloat(HoloCardIDs.ViewBlend, 1f);
            mat.SetVector(HoloCardIDs.VirtualView, new Vector4(0f, 0f, 1f, 0f));
            mat.SetVector(HoloCardIDs.PointerUV, new Vector4(0.5f, 0.5f, 0f, 0f));
            mat.SetFloat(HoloCardIDs.Tilt, 0f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Material CreateBodyMaterial()
        {
            const string path = MaterialsDir + "/HoloCard_Body.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.055f, 0.06f, 0.10f, 1f));
            mat.SetFloat("_Smoothness", 0.55f);
            mat.SetFloat("_Metallic", 0.1f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Material CreateBackdropMaterial()
        {
            const string path = MaterialsDir + "/HoloCard_Backdrop.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            // 조명을 받으므로 알베도를 아주 낮게. 배경이 밝으면 카드가 대비를 잃는다.
            mat.SetColor("_BaseColor", new Color(0.010f, 0.012f, 0.020f, 1f));
            mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Texture2D LoadTexture(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{fileName}");
        }

        /// <summary>
        /// 샘플 텍스처 임포트 설정. 높이맵/마스크가 sRGB 로 들어가면 패럴랙스가 뭉갠다.
        /// </summary>
        static void ConfigureSampleTextures()
        {
            SetDataMap($"{TexturesDir}/Sample_Depth.png");
            SetDataMap($"{TexturesDir}/Sample_Foil.png");

            // 샘플 아트도 384x537 이라 npotScale 기본값이면 512x512 로 찌그러진다.
            var art = AssetImporter.GetAtPath($"{TexturesDir}/Sample_Art.jpg") as TextureImporter;
            if (art != null && (art.wrapMode != TextureWrapMode.Clamp || art.npotScale != TextureImporterNPOTScale.None))
                HoloCardBaker.ConfigureAsCardArt($"{TexturesDir}/Sample_Art.jpg");
        }

        static void SetDataMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (!importer.sRGBTexture
                && importer.wrapMode == TextureWrapMode.Clamp
                && importer.npotScale == TextureImporterNPOTScale.None) return;

            HoloCardBaker.ConfigureAsDataMap(path);
        }
    }
}
