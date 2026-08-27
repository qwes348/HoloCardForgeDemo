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
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -3.3f), Quaternion.identity);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(212, 225, 250, 255);
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;

            CreateLights();

            PackStage stage = BuildStage(cam);

            // ── 팩. 구워 둔 봉지 셸을 이음매로 자른다. 셸은 한 덩어리라
            //    앞/뒤/옆이 전부 같은 필름 머티리얼이다 (실제 포장지도 그렇다).
            var packGo = new GameObject("Card Pack");
            var pack = packGo.AddComponent<CardPack>();
            pack.shellMesh = PackShellBaker.LoadOrBake();
            pack.frontMaterial = wrapMat;
            pack.backMaterial = wrapMat;
            pack.sideMaterial = wrapMat;
            // 셸은 v 0.865 부터 두께가 눌린다. 그 아래를 뜯어야 입이 벌어진다.
            pack.stripHeightRatio = 0.115f;
            // 빛으로 베는 연출이라 절단면은 톱니가 아니라 직선이어야 한다.
            pack.tearJitter = 0f;
            pack.Rebuild();
            packGo.transform.localPosition = Vector3.zero;

            PackSlash slash = BuildSlash(packGo.transform);

            // ── 카드 풀
            var poolGo = new GameObject("Card Pool");
            var catalog = BuildRarityLookup();

            foreach (HoloCardSetup.CardSet set in sets)
            {
                Material cardMat = HoloCardSetup.CreateCardMaterial(set.name, set.art, HoloCardPreset.Builtin.VintagePrint);
                GameObject card = BuildCard(set, cardMat, backMat, rimMat, out HoloCardMesh mesh);
                card.transform.SetParent(poolGo.transform, false);

                var info = card.AddComponent<HoloCardInfo>();
                info.displayName = set.name;
                info.rarityLabel = catalog.TryGetValue(set.name, out string rarity) ? rarity : "Rare Holo";
                info.rarity = TierFor(set.name, info.rarityLabel);

                // 테두리 포일은 등급을 눈으로 읽게 해 주는 두 번째 장치다 (첫 번째는
                // 카드 밑의 ◇/★). 표식은 보고 세어야 알지만 테두리는 카드를 기울이는
                // 순간 바로 온다.
                ApplyBorderFoil(cardMat, info.rarity, mesh);

                card.SetActive(false);
            }

            // ── 파티클
            ParticleSystem tear = CreateTearBurst();
            ParticleSystem rare = CreateRareBurst();

            // ── 캐러셀 & 감독
            CardCarousel carousel = BuildCarousel(cam);
            RarityDisplay rarityDisplay = BuildRarityDisplay(carousel.transform.parent);

            var directorGo = new GameObject("Pack Opening Director");
            var director = directorGo.AddComponent<PackOpeningDirector>();
            director.pack = pack;
            director.cardPool = poolGo.transform;
            director.carousel = carousel;
            director.rarityDisplay = rarityDisplay;
            director.targetCamera = cam;
            director.tearBurst = tear;
            director.rareBurst = rare;
            director.stage = stage;
            director.slash = slash;
            director.packCameraDistance = 3.3f;
            director.carouselCameraDistance = 2.35f;

            // 무대가 밝아졌으니 앰비언트도 같이 올린다. 여기가 어두우면 Lit 인
            // 카드 뒷면·옆면만 어둡게 남아서 배경에서 오려 붙인 것처럼 뜬다.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.62f, 0.70f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.60f, 0.76f);
            RenderSettings.ambientGroundColor  = new Color(0.34f, 0.40f, 0.52f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = packGo;
            Debug.Log($"[Pack Opening] 씬 생성 완료 ({sets.Count}장 풀): {ScenePath}\n" +
                      "Play → 팩을 클릭해 개봉. 그다음 ‹ › 화살표·좌우 키·드래그로 카드를 넘긴다. R 키로 다시 뽑기.");
        }

        // ── 조각들 ───────────────────────────────────────────────────────

        static Dictionary<string, string> BuildRarityLookup()
        {
            var map = new Dictionary<string, string>();
            foreach (var entry in HoloCardDownloader.Catalog)
                map[entry.name] = entry.rarity;
            return map;
        }

        static GameObject BuildCard(HoloCardSetup.CardSet set, Material front, Material back, Material rim,
                                    out HoloCardMesh mesh)
        {
            var go = new GameObject(set.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.AddComponent<BoxCollider>();

            mesh = go.AddComponent<HoloCardMesh>();
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
            // 손을 놓으면 저 혼자 천천히 흔들린다. 개봉 씬에서는 이게 필수다 —
            // 테두리 포일도 회절 무지개도 **각도가 변해야** 색이 사는데, 가만히
            // 두면 카드가 인쇄물 사진처럼 죽어 버린다.
            controller.fallbackToAutoDemo = true;
            controller.idleDelay = 0.9f;
            controller.rotateTransform = true;
            controller.maxTiltAngle = 13f;
            controller.popDistance = 0.04f;
            controller.enabled = false;

            return go;
        }

        // ── 무대 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 깊이가 다른 판 세 장을 세운다. 하나짜리 배경은 아무리 예뻐도 평평해서,
        /// 층이 서로 다른 속도로 밀려야 비로소 깊이가 읽힌다.
        ///   Sky   맨 뒤. 세로 그라디언트. 거의 안 움직인다.
        ///   Rays  중간. 위에서 퍼지는 빛살. 조금 움직인다.
        ///   Motes 앞. 떠다니는 빛 알갱이. 크게 움직이고 천천히 흐른다.
        /// </summary>
        static PackStage BuildStage(Camera cam)
        {
            var root = new GameObject("Stage");
            var stage = root.AddComponent<PackStage>();

            var sky   = AddStageQuad(root.transform, cam, "Sky",   7.0f,  1.35f, "StageSky",   false);
            var rays  = AddStageQuad(root.transform, cam, "Rays",  4.5f,  1.30f, "StageRays",  true);
            var motes = AddStageQuad(root.transform, cam, "Motes", 2.2f,  1.40f, "StageMotes", true);

            stage.layers = new[]
            {
                new PackStage.Layer { target = sky.transform,   parallax = 0.05f },
                new PackStage.Layer { target = rays.transform,  parallax = 0.22f, drift = 0.05f, driftSpeed = 0.13f },
                new PackStage.Layer { target = motes.transform, parallax = 0.55f, drift = 0.10f, driftSpeed = 0.21f },
            };
            // 하늘만 물들인다. 가산 층까지 같이 올리면 레어 연출에서 화면이 날아간다.
            stage.tinted = new[] { sky.GetComponent<Renderer>() };

            // 레어 무지개. 카드(z=0)보다 **뒤**라 카드는 안 덮는다 — 앞에 두면
            // 가산이라 카드가 통째로 하얗게 날아간다. 패럴랙스 층에는 넣지 않는다:
            // 번쩍이는 판이 같이 밀리면 화면이 흔들리는 것처럼 읽힌다.
            var rainbow = AddStageQuad(root.transform, cam, "Rainbow", 1.2f, 1.4f, "StageRainbow", true);
            stage.rainbow = rainbow.GetComponent<Renderer>();
            stage.rainbow.enabled = false;

            // 층을 다 붙인 뒤에 제자리를 못 박아야 한다. AddComponent 시점의
            // OnEnable 은 layers 가 비어 있어서 아무것도 못 잡는다.
            stage.CaptureHomes();
            return stage;
        }

        static GameObject AddStageQuad(Transform parent, Camera cam, string name,
                                       float z, float margin, string texture, bool additive)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            // 카메라에서 이 판까지의 거리에서 화면을 덮을 크기 + 패럴랙스 여유.
            float distance = z - cam.transform.position.z;
            float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * margin;
            float width = height * 16f / 9f;
            go.transform.localScale = new Vector3(width, height, 1f);

            go.GetComponent<MeshRenderer>().sharedMaterial =
                CreateStageMaterial($"{MaterialsDir}/Stage{name}.mat", texture,
                                    additive ? StageBlend.Additive : StageBlend.Opaque);
            return go;
        }

        /// <summary>무대 판의 합성 방식.</summary>
        enum StageBlend
        {
            /// <summary>배경판. 깊이를 쓰고 맨 뒤에 그린다.</summary>
            Opaque,
            /// <summary>빛살·광선·먼지. 배경을 더한다.</summary>
            Additive,
            /// <summary>화살표처럼 밝은 배경 위에 **얹혀야** 하는 것. 가산은 흰 배경에서 사라진다.</summary>
            Alpha,
        }

        /// <summary>
        /// 무대 판용 언릿 머티리얼. 가산 합성은 _Surface / _Blend 만 써서는 안 걸리고
        /// 블렌드 스테이트와 키워드까지 직접 세워야 한다.
        /// 양면으로 두는 이유는 Quad 의 앞면 방향 규약에 결과가 걸리지 않게 하려는 것
        /// (화살표는 한쪽을 스케일 -1 로 뒤집어 쓰므로 이게 없으면 사라진다).
        /// </summary>
        static Material CreateStageMaterial(string path, string texture, StageBlend blend)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            string texPath = $"{TexturesDir}/Stage/{texture}.png";
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Cull", (float)CullMode.Off);

            if (blend != StageBlend.Opaque)
            {
                bool additive = blend == StageBlend.Additive;
                mat.SetFloat("_Surface", 1f);
                // URP 의 _Blend 는 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply 다.
                // 1 을 넣으면 머티리얼 검증기가 블렌드 스테이트를 프리멀티플라이로
                // 덮어써서(One / OneMinusSrcAlpha) RGB 가 알파와 무관하게 통째로
                // 더해진다 — 흰 텍스처면 화면 전체가 새하얘진다.
                mat.SetFloat("_Blend", additive ? 2f : 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Background;
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // ── 등급 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 예시 카드 13장을 TCG Pocket 식 사다리에 배치한다.
        ///
        /// 카탈로그의 레어도 문자열만으로는 못 가른다 — Base Set 홀로 다섯 장이
        /// 전부 "Rare Holo" 라서 한 등급에 몰린다. 그러면 ◇ 자리 세 개를 채울
        /// 카드가 모자라거나 남아서 뽑기 구성이 매번 똑같아진다. 그래서 이름으로
        /// 나눠 3 / 2 로 흩어 놓는다.
        ///
        /// 이 표는 씬 생성 전용이다. 카탈로그(<see cref="HoloCardDownloader"/>)는
        /// 셰이더 키트 쪽이라 PackOpening 의 등급을 알면 안 된다.
        /// </summary>
        static readonly Dictionary<string, CardRarity> TierByName = new Dictionary<string, CardRarity>
        {
            { "Zapdos",            CardRarity.Common },
            { "Blastoise",         CardRarity.Common },
            { "Venusaur",          CardRarity.Common },
            { "Mewtwo",            CardRarity.Uncommon },
            { "Charizard",         CardRarity.Uncommon },
            { "Rayquaza V",        CardRarity.Rare },
            { "Ditto VMAX",        CardRarity.Rare },
            { "Charizard VSTAR",   CardRarity.DoubleRare },
            { "Celebi",            CardRarity.DoubleRare },
            { "Mew V",             CardRarity.ArtRare },
            { "Charizard TG",      CardRarity.ArtRare },
            { "Radiant Charizard", CardRarity.SuperRare },
            { "Flareon VMAX",      CardRarity.Immersive },
        };

        /// <summary>표에 없는 카드는 레어도 문자열에서 추측한다.</summary>
        static CardRarity TierFor(string name, string label)
        {
            if (TierByName.TryGetValue(name, out CardRarity tier)) return tier;
            if (string.IsNullOrEmpty(label)) return CardRarity.Common;

            if (label.Contains("Rainbow")) return CardRarity.Immersive;
            if (label.Contains("Radiant")) return CardRarity.SuperRare;
            if (label.Contains("Ultra") || label.Contains("Gallery")) return CardRarity.ArtRare;
            if (label.Contains("Amazing") || label.Contains("VSTAR")) return CardRarity.DoubleRare;
            if (label.Contains("VMAX") || label.Contains(" V")) return CardRarity.Rare;
            return CardRarity.Common;
        }

        /// <summary>
        /// 등급에 따라 테두리 포일 세기를 정한다.
        ///
        /// 낮은 등급도 0 으로 두지 않는다. 완전히 꺼 두면 흔한 카드가 "포일이 약한
        /// 카드" 가 아니라 "다른 셰이더를 쓰는 카드" 로 보인다 — 은박 실선 정도는
        /// 남겨 두고 색이 얼마나 사느냐로 등급을 가른다.
        ///
        /// 모서리 반경은 룩이 아니라 메시에서 그대로 받아 적는다. 셰이더 쪽 좌표계는
        /// 카드 높이를 1 로 두므로 높이로 나눠야 한다.
        /// </summary>
        static void ApplyBorderFoil(Material mat, CardRarity rarity, HoloCardMesh mesh)
        {
            if (mat == null) return;

            float strength;
            switch (rarity)
            {
                case CardRarity.Common:
                case CardRarity.Uncommon:   strength = 0.28f; break;
                case CardRarity.Rare:       strength = 0.55f; break;
                case CardRarity.DoubleRare: strength = 0.80f; break;
                case CardRarity.ArtRare:    strength = 1.05f; break;
                case CardRarity.SuperRare:  strength = 1.25f; break;
                default:                    strength = 1.5f;  break;
            }

            // 값을 하나도 빠짐없이 적는다. CreateCardMaterial 은 프리셋을 **새로 만든
            // 머티리얼에만** 붓기 때문에, 이미 존재하는 13장은 여기서 안 적으면
            // 셰이더 기본값에 남는다 (씬을 다시 만들어도 안 바뀐다).
            mat.SetFloat(HoloCardIDs.BorderFoil, strength);
            // 등급이 오를수록 띠가 넓어지고 색이 여러 번 돈다.
            mat.SetFloat(HoloCardIDs.BorderWidth, Mathf.Lerp(0.030f, 0.062f, strength / 1.5f));
            mat.SetFloat(HoloCardIDs.BorderHue,   Mathf.Lerp(1.2f, 2.6f, strength / 1.5f));
            mat.SetFloat(HoloCardIDs.BorderStreak, Mathf.Lerp(0.30f, 0.75f, strength / 1.5f));
            mat.SetFloat(HoloCardIDs.BorderStreakScale, 54f);
            // 은박 바탕은 등급과 무관하게 밝다 — 흔한 카드도 "은박이 약한" 게 아니라
            // "색이 덜 어리는" 것이다. 등급으로 가르는 건 색 쪽(Chroma).
            mat.SetFloat(HoloCardIDs.BorderSilver, 0.92f);
            mat.SetFloat(HoloCardIDs.BorderChroma, Mathf.Lerp(1.1f, 2.6f, strength / 1.5f));
            // 띠가 인쇄를 덮는 정도. 등급이 낮을수록 얇게 얹힌다.
            mat.SetFloat(HoloCardIDs.BorderCover, Mathf.Lerp(0.45f, 0.92f, strength / 1.5f));
            mat.SetFloat(HoloCardIDs.BorderShift, 1.8f);
            mat.SetFloat(HoloCardIDs.BorderInset, 0.006f);
            // 띠 안쪽 경계를 세운다. 무르면 카드 이름 위까지 번져 인쇄가 씻긴다.
            mat.SetFloat(HoloCardIDs.BorderSharp, 2.9f);

            if (mesh != null && mesh.height > 1e-4f)
                mat.SetFloat(HoloCardIDs.BorderRadius, mesh.cornerRadius / mesh.height);

            EditorUtility.SetDirty(mat);
        }

        // ── 레어도 표식 ──────────────────────────────────────────────────

        /// <summary>
        /// 카드 밑의 ◇/★ 표식과 왼쪽 위 NEW 뱃지.
        ///
        /// 섬광은 표식의 **자식이 아니라 형제**다. 표식은 0 에서 튀어나오느라
        /// 스케일이 트윈되는데, 자식으로 달면 거기에 섬광 스케일까지 곱해진다.
        /// </summary>
        static RarityDisplay BuildRarityDisplay(Transform parent)
        {
            var go = new GameObject("Rarity Display");
            go.transform.SetParent(parent, false);
            var display = go.AddComponent<RarityDisplay>();

            Material diamond = CreateStageMaterial($"{MaterialsDir}/PipDiamond.mat", "PipDiamond", StageBlend.Alpha);
            Material star    = CreateStageMaterial($"{MaterialsDir}/PipStar.mat",    "PipStar",    StageBlend.Alpha);
            Material spark   = CreateStageMaterial($"{MaterialsDir}/PipSpark.mat",   "SlashFlash", StageBlend.Additive);
            Material badge   = CreateStageMaterial($"{MaterialsDir}/NewBadge.mat",   "NewBadge",   StageBlend.Alpha);

            display.diamondMaterial = diamond;
            display.starMaterial = star;

            const int maxPips = 4;
            display.pips = new RarityDisplay.Pip[maxPips];
            for (int i = 0; i < maxPips; i++)
            {
                var pipRoot = new GameObject($"Pip {i}");
                pipRoot.transform.SetParent(go.transform, false);
                pipRoot.transform.localScale = Vector3.zero;

                // 크기는 **자식 쿼드**가 들고 있고 루트는 0→1 로만 튄다. 루트에
                // 직접 크기를 주면 팝인 트윈이 그 값을 1 로 덮어써서 표식이
                // 1 유닛짜리로 부풀어 오른다.
                MakeQuad(pipRoot.transform, "Mark", display.pipSize, display.pipSize, 0f,
                         diamond, out Renderer markRenderer);

                Transform sparkT = MakeQuad(go.transform, $"Spark {i}", display.sparkleSize, display.sparkleSize,
                                            0.01f, spark, out Renderer sparkRenderer);
                sparkT.gameObject.SetActive(false);

                display.pips[i] = new RarityDisplay.Pip
                {
                    root = pipRoot.transform,
                    mark = markRenderer,
                    sparkle = sparkT,
                    sparkleRenderer = sparkRenderer,
                };

                pipRoot.SetActive(false);
            }

            // 표식과 같은 이유로 뱃지도 루트 + 자식 쿼드다.
            var badgeRoot = new GameObject("New Badge");
            badgeRoot.transform.SetParent(go.transform, false);
            badgeRoot.transform.localScale = Vector3.zero;
            MakeQuad(badgeRoot.transform, "Tag", display.badgeSize.x, display.badgeSize.y, 0f,
                     badge, out Renderer badgeRenderer);
            badgeRoot.SetActive(false);
            display.newBadge = badgeRoot.transform;
            display.newBadgeRenderer = badgeRenderer;

            return display;
        }

        // ── 캐러셀 ───────────────────────────────────────────────────────

        /// <summary>
        /// 카드를 한 장씩 넘겨 보는 캐러셀.
        ///
        /// 트랙과 화살표는 **형제**다. 화살표를 트랙 밑에 넣으면 넘길 때 카드와
        /// 같이 흘러 나가 버린다 — 화살표는 화면에 붙박이어야 한다.
        /// </summary>
        static CardCarousel BuildCarousel(Camera cam)
        {
            var root = new GameObject("Carousel");
            root.transform.localPosition = Vector3.zero;

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(root.transform, false);
            var carousel = trackGo.AddComponent<CardCarousel>();
            carousel.targetCamera = cam;

            carousel.leftArrow  = BuildArrow(root.transform, "Arrow Left",  -1f);
            carousel.rightArrow = BuildArrow(root.transform, "Arrow Right",  1f);
            return carousel;
        }

        /// <summary>
        /// 화살표 한 벌. 제자리 획 + 바깥으로 흘러 나가는 고스트 + 넉넉한 클릭 판정.
        /// 왼쪽은 텍스처를 뒤집어 쓰므로 머티리얼이 양면이어야 한다(Cull Off).
        /// </summary>
        static CarouselArrow BuildArrow(Transform parent, string name, float sign)
        {
            const float w = 0.098f, h = 0.15f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // 오른쪽을 가리키는 텍스처를 x 로 뒤집어 왼쪽 화살표를 만든다.
            // 이렇게 두면 "바깥" 이 양쪽 다 로컬 +X 라 펄스 코드가 하나로 끝난다.
            go.transform.localScale = new Vector3(sign, 1f, 1f);

            var arrow = go.AddComponent<CarouselArrow>();
            Material mat = CreateStageMaterial($"{MaterialsDir}/CarouselArrow.mat", "Chevron", StageBlend.Alpha);

            arrow.blade = MakeQuad(go.transform, "Blade", w, h, 0f, mat, out arrow.bladeRenderer);
            arrow.ghost = MakeQuad(go.transform, "Ghost", w, h, 0.001f, mat, out arrow.ghostRenderer);

            var hit = new GameObject("Hit", typeof(BoxCollider));
            hit.transform.SetParent(go.transform, false);
            var box = hit.GetComponent<BoxCollider>();
            // 획 자체는 얇다. 판정까지 얇으면 누를 수가 없다.
            box.size = new Vector3(0.30f, 0.50f, 0.02f);
            box.center = new Vector3(0.06f, 0f, 0f);
            arrow.hitArea = box;

            arrow.SetShownImmediate(false);
            return arrow;
        }

        static Transform MakeQuad(Transform parent, string name, float w, float h, float z,
                                  Material mat, out Renderer renderer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.transform.localScale = new Vector3(w, h, 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>팩을 가르는 빛줄기. 팩 밑에 붙여 팩과 같이 기울게 한다.</summary>
        static PackSlash BuildSlash(Transform pack)
        {
            var root = new GameObject("Slash");
            root.transform.SetParent(pack, false);
            var slash = root.AddComponent<PackSlash>();

            var streak = GameObject.CreatePrimitive(PrimitiveType.Quad);
            streak.name = "Streak";
            streak.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(streak.GetComponent<Collider>());
            streak.GetComponent<MeshRenderer>().sharedMaterial =
                CreateStageMaterial($"{MaterialsDir}/SlashStreak.mat", "SlashStreak", StageBlend.Additive);

            var flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flash.name = "Flash";
            flash.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(flash.GetComponent<Collider>());
            flash.GetComponent<MeshRenderer>().sharedMaterial =
                CreateStageMaterial($"{MaterialsDir}/SlashFlash.mat", "SlashFlash", StageBlend.Additive);

            slash.length = 3.8f;
            slash.thickness = 0.13f;
            slash.duration = 0.16f;
            slash.flashDuration = 0.20f;
            slash.flashSize = 0.62f;

            slash.streak = streak.transform;
            slash.streakRenderer = streak.GetComponent<Renderer>();
            slash.flash = flash.transform;
            slash.flashRenderer = flash.GetComponent<Renderer>();
            slash.Hide();
            return slash;
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
            frontGo.transform.rotation = Quaternion.Euler(22f, -16f, 0f);   // 정면 살짝 위·왼쪽

            // 필름의 정반사는 GetMainLight() 한 개만 본다. 밝기로 자동 선택되게
            // 두면 키와 정면 필이 엎치락뒤치락하므로 못을 박는다. 정면 필을 쓰는
            // 이유는 카메라 쪽에서 와야 팩 앞면에 하이라이트가 얹히기 때문.
            RenderSettings.sun = front;
        }

        // ── 머티리얼 ─────────────────────────────────────────────────────

        /// <summary>
        /// 실사 스캔이 있으면 그 폴더를, 없으면 절차 생성 폴더를 돌려준다.
        /// <c>Textures/Pokemon/</c> 은 저작물이라 <c>.gitignore</c> 로 빠져 있으므로
        /// 클린 클론은 자동으로 절차 생성본을 쓴다.
        /// </summary>
        static string ArtDir(string artName)
        {
            string licensed = $"{TexturesDir}/Pokemon/{artName}.png";
            return File.Exists(licensed) ? $"{TexturesDir}/Pokemon" : TexturesDir;
        }

        static Material CreateHoloMaterial(string path, string artName, HoloCardPreset.Builtin preset,
                                           string shaderName = "Holo/Holographic Card (3D)")
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) return null;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            string dir = ArtDir(artName);
            HoloCard.Editor.HoloCardBaker.ConfigureAsCardArt($"{dir}/{artName}.png");
            mat.SetTexture(HoloCardIDs.BaseMap, AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{artName}.png"));
            mat.SetTexture(HoloCardIDs.DepthMap, AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{artName}_Depth.png"));
            mat.SetTexture(HoloCardIDs.FoilMask, AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{artName}_Foil.png"));

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
        /// 팩 포장지 = 비닐 필름. 카드 셰이더가 아니라 Holo/Pack Film 을 쓴다.
        ///
        /// 카드와 정반대로 가는 값이 셋 있다.
        ///   ViewBlend 0     셸이 곡면이라 실제 카메라 시선이 지배해야 시트 반사가
        ///                   몸통을 감아 돈다. 가상 시선을 섞으면 곡률이 죽는다.
        ///   Parallax 거의 0 깊이감은 이제 지오메트리가 준다. POM 을 세게 주면
        ///                   곡면 그레이징에서 실루엣 뒤를 늘여 채운다.
        ///   무지개 약하게    포장지의 정체는 회절이 아니라 정반사다. 무지개를 올리면
        ///                   다시 홀로 카드처럼 보인다. 대신 Spec / Studio 를 올린다.
        /// </summary>
        static Material CreateWrapMaterial()
        {
            Material mat = CreateHoloMaterial($"{MaterialsDir}/PackWrap.mat", "PackWrap",
                                              HoloCardPreset.Builtin.StandardHolo, "Holo/Pack Film");
            if (mat == null) return null;

            // 패럴랙스는 완전히 끈다. 곡면 위에서 시선을 구김으로 틀어 놓은 상태로
            // POM 을 돌리면 인쇄가 통째로 출렁여서 물에 잠긴 것처럼 보인다.
            mat.SetFloat(HoloCardIDs.ParallaxDepth, 0f);
            mat.SetFloat(HoloCardIDs.ParallaxSteps, 24f);
            mat.SetFloat(HoloCardIDs.ParallaxChroma, 0f);
            mat.SetFloat(HoloCardIDs.DepthShade, 0.45f);

            // 실사 스캔을 물렸으면 사진에 조명·구김·명암이 이미 구워져 있다.
            // 셰이더가 얹는 것들은 전부 낮춰야 한다 — 안 낮추면 더한 값이 어두운
            // 인쇄를 들어 올려서 전체가 뿌옇게 뜬다.
            bool photo = ArtDir("PackWrap") != TexturesDir;

            mat.SetFloat(HoloCardIDs.HoloIntensity, photo ? 0.07f : 0.10f);
            mat.SetFloat(HoloCardIDs.HoloScale, 5.0f);
            mat.SetFloat(HoloCardIDs.HoloSpread, 3.4f);
            mat.SetFloat(HoloCardIDs.HoloContrast, 1.5f);
            mat.SetFloat(HoloCardIDs.HoloBlend, 0.25f);

            // 무지개의 그레이징 부스트는 낮게. 구김으로 시선이 흔들리는 면에서
            // 이 값이 높으면 무지개가 얼룩으로 터진다.
            mat.SetFloat(HoloCardIDs.HoloGrazing, 0.25f);

            mat.SetFloat(HoloCardIDs.SparkleIntensity, photo ? 0.05f : 0.08f);
            mat.SetFloat(HoloCardIDs.SparkleDensity, 70f);

            mat.SetFloat(HoloCardIDs.GlareIntensity, photo ? 0.04f : 0.07f);
            mat.SetFloat(HoloCardIDs.GlareSize, 0.62f);
            mat.SetFloat(HoloCardIDs.GlarePower, 1.8f);
            mat.SetFloat(HoloCardIDs.SheenIntensity, photo ? 0.10f : 0.18f);

            // 가장자리 하이라이트는 흰색. 파란 림은 인쇄색과 섞여 얼룩처럼 보인다.
            mat.SetFloat(HoloCardIDs.Bevel, photo ? 0.02f : 0.04f);
            mat.SetFloat(HoloCardIDs.RimIntensity, photo ? 0.18f : 0.28f);
            mat.SetFloat(HoloCardIDs.RimPower, 3.5f);
            mat.SetColor("_RimColor", new Color(1f, 0.99f, 0.96f, 1f));
            mat.SetFloat(HoloCardIDs.ViewBlend, 0f);

            // ── 필름 전용
            // 구김은 "면은 평평하고 접힌 선만" 이어야 한다. 완만하게 접으면 둥근
            // 혹이 떠서 물처럼 보이고, 촘촘하게 주면 빗줄기가 된다.
            // Stretch 0.32 는 팩 uv 가 이미 세로로 1.9배 눌려 있는 걸 감안한 값
            // (실제로는 6:1 쯤 되는 긴 주름).
            mat.SetFloat("_CrinkleScale", 4f);
            mat.SetFloat("_CrinkleStrength", photo ? 0.11f : 0.16f);
            mat.SetFloat("_CrinkleRidge", 0.9f);
            mat.SetFloat("_CrinkleSharpness", 6f);
            mat.SetFloat("_CrinkleStretch", 0.32f);

            mat.SetFloat("_SpecIntensity", 1.3f);
            mat.SetFloat("_SpecPower", 120f);

            // 반사되는 "방" 은 어둡고 소프트박스만 밝아야 한다. 환경 전체를 밝히면
            // 스치는 각도에서 팩이 통째로 하얗게 뜬다.
            mat.SetColor("_StudioSky", new Color(0.15f, 0.19f, 0.29f, 1f));
            mat.SetColor("_StudioGround", new Color(0.02f, 0.02f, 0.035f, 1f));
            mat.SetColor("_StudioBox", new Color(0.95f, 0.97f, 1.05f, 1f));
            // 소프트박스는 카메라 쪽 위·왼편. 이 팩은 부푼 방향이 가로라
            // reflect.y 만 보는 방식으로는 정면에서 하이라이트가 절대 안 걸린다.
            mat.SetVector("_StudioBoxDir", new Vector4(0.25f, 0.30f, -0.92f, 0f));
            mat.SetFloat("_StudioBoxSize", photo ? 0.26f : 0.40f);
            mat.SetFloat("_StudioIntensity", photo ? 0.35f : 0.70f);

            // 아트가 언릿이라 곡률이 색에 안 나타난다. 이걸 안 주면 하이라이트만
            // 떠 있고 몸통은 끝까지 판때기로 보인다. 실사 사진에는 이미 명암이
            // 구워져 있으므로 약하게만.
            mat.SetFloat("_FormShade", photo ? 0.22f : 0.30f);

            mat.SetFloat("_Cull", 0f);            // 뜯긴 자리로 봉지 속이 보여야 한다
            mat.SetFloat("_InteriorDarken", 0.18f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// 카드 뒷면 텍스처. 실제 카드 뒷면 스캔을 <c>Textures/Pokemon/CardBack.png</c>
        /// 에 두면 그걸 쓰고, 없으면 절차 생성본으로 떨어진다.
        /// 저작물이라 그 폴더는 <c>.gitignore</c> 로 빠져 있다 — 클린 클론에서는
        /// 자동으로 절차 생성본이 걸린다.
        /// </summary>
        static Texture2D LoadCardBackTexture()
        {
            string path = $"{ArtDir("CardBack")}/CardBack.png";
            // 카드 아트와 같은 규칙. npotScale 기본값이 종횡비를 망가뜨린다.
            HoloCard.Editor.HoloCardBaker.ConfigureAsCardArt(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
            mat.SetTexture("_BaseMap", LoadCardBackTexture());
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

        static Material CreateParticleMaterial(string path, Color tint, string texture = null)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);       // Transparent
            mat.SetFloat("_Blend", 2f);         // Additive (1 은 Premultiply 다)
            mat.SetColor("_BaseColor", tint);
            // 텍스처를 안 물리면 흰 **정사각형**이 날아다닌다. 반짝임이라기보다
            // 색종이라, 스파클로 쓰려면 반드시 물려야 한다.
            if (!string.IsNullOrEmpty(texture))
                mat.SetTexture("_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Stage/{texture}.png"));
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
            // "폭발" 이라 부를 만하려면 한 번에 나와야 한다. 조금씩 오래 뿜으면
            // 분수가 된다.
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 170) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.30f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                CreateParticleMaterial(MaterialsDir + "/RareSparks.mat", Color.white, "SlashFlash");

            return ps;
        }
    }
}
