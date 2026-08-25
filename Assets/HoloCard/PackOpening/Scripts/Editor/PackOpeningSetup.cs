using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HoloCard.Editor;

namespace HoloCard.PackOpening.Editor
{
    /// <summary>카드팩 개봉 데모 씬을 통째로 생성한다.</summary>
    public static class PackOpeningSetup
    {
        const string Root        = "Assets/HoloCard/PackOpening";
        const string TexturesDir = Root + "/Textures";
        const string MaterialsDir = Root + "/Materials";
        const string ScenesDir   = Root + "/Scenes";
        const string ScenePath   = ScenesDir + "/PackOpening.unity";

        [MenuItem("Tools/Holo Card/Create Pack Opening Scene", false, 41)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists($"{TexturesDir}/PackWrap.png"))
            {
                if (!EditorUtility.DisplayDialog("Pack Opening",
                        "팩 아트가 없습니다. 지금 만들까요?", "만들기", "취소")) return;
                PackArtGenerator.GenerateAll();
            }

            var sets = HoloCardSetup.FindCardSets();
            if (sets.Count == 0)
            {
                EditorUtility.DisplayDialog("Pack Opening",
                    "카드가 없습니다.\nTools > Holo Card > Download Sample Cards 를 먼저 실행하세요.", "확인");
                return;
            }

            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(ScenesDir);

            Material wrapMat  = CreateWrapMaterial();
            Material backMat  = CreateCardBackMaterial();
            Material rimMat   = CreateRimMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라. 팩을 볼 때는 가까이, 카드가 깔리면 감독이 뒤로 물린다.
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.GetComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2.6f), Quaternion.identity);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.0045f, 0.005f, 0.009f, 1f);
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;

            CreateLights();

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            backdrop.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2.2f), Quaternion.Euler(0f, 180f, 0f));
            backdrop.transform.localScale = new Vector3(10f, 10f, 1f);
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            backdrop.GetComponent<MeshRenderer>().sharedMaterial = CreateBackdropMaterial();

            // ── 팩
            var packGo = new GameObject("Card Pack");
            var pack = packGo.AddComponent<CardPack>();
            pack.frontMaterial = wrapMat;
            pack.sideMaterial = rimMat;
            pack.Rebuild();
            // 뒷면도 포장지로 (서브메시 1)
            SetPieceMaterials(pack.Body, wrapMat, wrapMat, rimMat);
            SetPieceMaterials(pack.Strip, wrapMat, wrapMat, rimMat);
            packGo.transform.localPosition = Vector3.zero;

            // ── 카드 풀
            var poolGo = new GameObject("Card Pool");
            var catalog = BuildRarityLookup();

            foreach (HoloCardSetup.CardSet set in sets)
            {
                Material cardMat = HoloCardSetup.CreateCardMaterial(set.name, set.art, HoloCardPreset.Builtin.VintagePrint);
                GameObject card = BuildCard(set, cardMat, backMat, rimMat);
                card.transform.SetParent(poolGo.transform, false);

                var info = card.AddComponent<HoloCardInfo>();
                info.displayName = set.name;
                info.rarity = catalog.TryGetValue(set.name, out string rarity) ? rarity : "Rare Holo";
                // 구형 홀로(Base Set)는 일반, 현행 카드(V / VMAX / VSTAR / 레인보우 등)는 레어.
                info.isRare = info.rarity != "Rare Holo";

                card.SetActive(false);
            }

            // ── 파티클
            ParticleSystem tear = CreateTearBurst();
            ParticleSystem rare = CreateRareBurst();

            // ── 인스펙터 & 감독
            var inspectorGo = new GameObject("Card Inspector");
            var inspector = inspectorGo.AddComponent<HoloCardInspector>();
            inspector.targetCamera = cam;
            inspector.focusDistance = 1.25f;
            inspector.focusHeightRatio = 0.86f;
            inspector.dimAmount = 0.25f;
            // 확대해서 볼 때는 기울기를 더 줄인다. 코앞에서 크게 기울이면
            // 시야각이 커져 POM 이 실루엣 뒤를 늘여 채운다.
            inspector.focusedTiltAngle = 12f;
            inspector.enabled = false;

            var directorGo = new GameObject("Pack Opening Director");
            var director = directorGo.AddComponent<PackOpeningDirector>();
            director.pack = pack;
            director.cardPool = poolGo.transform;
            director.inspector = inspector;
            director.targetCamera = cam;
            director.tearBurst = tear;
            director.rareBurst = rare;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.05f, 0.06f, 0.10f);
            RenderSettings.ambientEquatorColor = new Color(0.028f, 0.033f, 0.055f);
            RenderSettings.ambientGroundColor  = new Color(0.008f, 0.008f, 0.016f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = packGo;
            Debug.Log($"[Pack Opening] 씬 생성 완료 ({sets.Count}장 풀): {ScenePath}\n" +
                      "Play → 팩을 클릭해 개봉, 카드를 클릭하면 뒤집히며 확대. R 키로 다시 뽑기.");
        }

        // ── 조각들 ───────────────────────────────────────────────────────

        static Dictionary<string, string> BuildRarityLookup()
        {
            var map = new Dictionary<string, string>();
            foreach (var entry in HoloCardDownloader.Catalog)
                map[entry.name] = entry.rarity;
            return map;
        }

        static GameObject BuildCard(HoloCardSetup.CardSet set, Material front, Material back, Material rim)
        {
            var go = new GameObject(set.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.AddComponent<BoxCollider>();

            var mesh = go.AddComponent<HoloCardMesh>();
            mesh.height = 0.88f;
            mesh.width = 0.88f * set.aspect;
            mesh.thickness = 0.012f;
            mesh.cornerRadius = 0.03f;
            mesh.Rebuild();

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { front, back, rim };
            renderer.shadowCastingMode = ShadowCastingMode.On;

            var controller = go.AddComponent<HoloCardController>();
            controller.targetRenderer = renderer;
            controller.source = HoloCardController.TiltSource.PointerHover;
            controller.fallbackToAutoDemo = false;
            controller.rotateTransform = true;
            controller.maxTiltAngle = 13f;
            controller.popDistance = 0.04f;
            controller.enabled = false;

            return go;
        }

        static void SetPieceMaterials(Transform piece, Material front, Material back, Material rim)
        {
            if (piece == null) return;
            var r = piece.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterials = new[] { front, back, rim };
        }

        static void CreateLights()
        {
            var keyGo = new GameObject("Key Light", typeof(Light));
            var key = keyGo.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.97f, 0.92f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.Soft;
            keyGo.transform.rotation = Quaternion.Euler(38f, -155f, 0f);

            var fillGo = new GameObject("Fill Light", typeof(Light));
            var fill = fillGo.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.42f, 0.55f, 0.9f);
            fill.intensity = 0.4f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(-15f, 35f, 0f);

            // 카메라 쪽에서 쏘는 정면 필. 카드 뒷면은 홀로 셰이더가 아니라 Lit 이라
            // 조명을 받아야 보이는데, 키/필이 전부 뒤·옆에서 와서 뒤집힌 카드가
            // 새까맣게 나온다. 카드 앞면과 팩 표면은 언릿이라 이 빛에 영향받지 않는다.
            var frontGo = new GameObject("Front Fill", typeof(Light));
            var front = frontGo.GetComponent<Light>();
            front.type = LightType.Directional;
            front.color = new Color(1f, 0.98f, 0.94f);
            front.intensity = 1.35f;
            front.shadows = LightShadows.None;
            frontGo.transform.rotation = Quaternion.Euler(8f, -6f, 0f);   // 거의 정면, 살짝 위에서
        }

        // ── 머티리얼 ─────────────────────────────────────────────────────

        static Material CreateHoloMaterial(string path, string artName, HoloCardPreset.Builtin preset)
        {
            var shader = Shader.Find("Holo/Holographic Card (3D)");
            if (shader == null) return null;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            mat.SetTexture(HoloCardIDs.BaseMap, AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{artName}.png"));
            mat.SetTexture(HoloCardIDs.DepthMap, AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{artName}_Depth.png"));
            mat.SetTexture(HoloCardIDs.FoilMask, AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{artName}_Foil.png"));

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

        /// <summary>
        /// 팩 포장지. 카드가 아니라 비닐 필름이라 값이 다르다.
        /// 무지개는 은은하게 깔되, 구겨진 마루를 훑고 지나가는 시트 반사와
        /// 넓은 글레어를 크게 올려야 "코팅된 종이"가 아니라 "필름"으로 읽힌다.
        /// 패럴랙스는 구김이 실제로 파이도록 조금 준다.
        /// </summary>
        static Material CreateWrapMaterial()
        {
            Material mat = CreateHoloMaterial($"{MaterialsDir}/PackWrap.mat", "PackWrap",
                                              HoloCardPreset.Builtin.StandardHolo);
            if (mat == null) return null;

            mat.SetFloat(HoloCardIDs.ParallaxDepth, 0.05f);
            mat.SetFloat(HoloCardIDs.ParallaxSteps, 40f);
            mat.SetFloat(HoloCardIDs.ParallaxChroma, 0f);
            mat.SetFloat(HoloCardIDs.DepthShade, 0.55f);

            mat.SetFloat(HoloCardIDs.HoloIntensity, 0.30f);
            mat.SetFloat(HoloCardIDs.HoloScale, 5.5f);
            mat.SetFloat(HoloCardIDs.HoloSpread, 3.4f);
            mat.SetFloat(HoloCardIDs.HoloContrast, 1.5f);
            mat.SetFloat(HoloCardIDs.HoloBlend, 0.25f);

            mat.SetFloat(HoloCardIDs.SparkleIntensity, 0.18f);
            mat.SetFloat(HoloCardIDs.SparkleDensity, 70f);

            mat.SetFloat(HoloCardIDs.GlareIntensity, 0.26f);
            mat.SetFloat(HoloCardIDs.GlareSize, 0.62f);
            mat.SetFloat(HoloCardIDs.GlarePower, 1.8f);
            mat.SetFloat(HoloCardIDs.SheenIntensity, 0.30f);

            mat.SetFloat(HoloCardIDs.Bevel, 0.06f);
            mat.SetFloat(HoloCardIDs.RimIntensity, 0.30f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// 카드 뒷면. 홀로 셰이더를 쓰지 않는다.
        /// 실제 카드 뒷면은 코팅 없는 무광 카드지고, 여기에 무지개가 얹히면
        /// 앞면과 구분이 안 가서 "카드를 뒤집었다"는 느낌이 죽는다.
        /// </summary>
        static Material CreateCardBackMaterial()
        {
            const string path = MaterialsDir + "/CardBack.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/CardBack.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);   // 무광 카드지
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Material CreateRimMaterial()
        {
            const string path = MaterialsDir + "/PackRim.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.76f, 1f));
            mat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Material CreateBackdropMaterial()
        {
            const string path = MaterialsDir + "/PackBackdrop.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.012f, 0.014f, 0.026f, 1f));
            mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Material CreateParticleMaterial(string path, Color tint)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);       // Transparent
            mat.SetFloat("_Blend", 1f);         // Additive
            mat.SetColor("_BaseColor", tint);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // ── 파티클 ───────────────────────────────────────────────────────

        static ParticleSystem CreateTearBurst()
        {
            var go = new GameObject("Tear Burst", typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.032f);
            main.gravityModifier = 1.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.78f, 1f), new Color(1f, 0.95f, 0.75f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 46) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                CreateParticleMaterial(MaterialsDir + "/FoilBits.mat", Color.white);

            return ps;
        }

        static ParticleSystem CreateRareBurst()
        {
            var go = new GameObject("Rare Burst", typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.05f);
            main.gravityModifier = -0.15f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.55f), new Color(1f, 1f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 320;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                CreateParticleMaterial(MaterialsDir + "/RareSparks.mat", Color.white);

            return ps;
        }
    }
}
