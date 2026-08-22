using UnityEditor;
using UnityEngine;

namespace HoloCard.Editor
{
    /// <summary>
    /// Depth·Foil 베이커의 UI. 실제 생성 로직은 <see cref="HoloCardBaker"/> 에 있고
    /// 이 창은 파라미터를 만지고 미리 보는 역할만 한다.
    ///
    /// 홀로 카드를 만들 때 제일 귀찮은 게 높이맵을 손으로 그리는 일인데,
    /// 카드 아트는 구조가 뻔해서(테두리 프레임 / 아트 창 / 텍스트 박스) 휘도와
    /// 채도만으로도 쓸 만한 근사가 나온다. 여기서 뽑고 필요하면 포토샵에서 손보면 된다.
    /// </summary>
    public class HoloCardTextureBaker : EditorWindow
    {
        Texture2D _source;
        HoloBakeSettings _settings = HoloBakeSettings.Default;

        Texture2D _depthPreview;
        Texture2D _foilPreview;
        Vector2 _scroll;

        [MenuItem("Tools/Holo Card/Depth and Foil Baker", false, 20)]
        public static void Open()
        {
            var window = GetWindow<HoloCardTextureBaker>(false, "Holo Baker", true);
            window.minSize = new Vector2(380f, 520f);
            window.Show();
        }

        void OnDisable()
        {
            DestroyPreview(ref _depthPreview);
            DestroyPreview(ref _foilPreview);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _source = (Texture2D)EditorGUILayout.ObjectField("Card Art", _source, typeof(Texture2D), false);
            bool dirty = EditorGUI.EndChangeCheck();

            if (_source == null)
            {
                EditorGUILayout.HelpBox(
                    "카드 아트를 넣으세요. Depth 맵(흰색 = 앞으로 튀어나옴)과 Foil 마스크를 " +
                    "같은 폴더에 PNG 로 저장하고 임포트 설정까지 맞춰줍니다.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Preset", GUILayout.Width(46f));
                if (GUILayout.Button("Default", EditorStyles.miniButtonLeft))
                {
                    _settings = HoloBakeSettings.Default; dirty = true;
                }
                if (GUILayout.Button("구형 포켓몬 카드", EditorStyles.miniButtonMid))
                {
                    _settings = HoloBakeSettings.ClassicPokemonCard; dirty = true;
                }
                if (GUILayout.Button("현행 풀아트", EditorStyles.miniButtonRight))
                {
                    _settings = HoloBakeSettings.ModernFullArt; dirty = true;
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Depth Map", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            _settings.depthSource = (DepthSource)EditorGUILayout.EnumPopup(
                new GUIContent("Height From", "무엇을 높이로 볼지. 대개 휘도가 무난하다."), _settings.depthSource);
            _settings.blurRadius = EditorGUILayout.IntSlider(
                new GUIContent("Blur", "노이즈를 뭉갠다. 안 하면 패럴랙스가 지글거린다."), _settings.blurRadius, 0, 16);
            _settings.blurIterations = EditorGUILayout.IntSlider("Blur Passes", _settings.blurIterations, 1, 4);
            _settings.contrast   = EditorGUILayout.Slider("Contrast", _settings.contrast, 0.2f, 3f);
            _settings.blackPoint = EditorGUILayout.Slider("Black Point", _settings.blackPoint, 0f, 0.9f);
            _settings.whitePoint = EditorGUILayout.Slider("White Point", _settings.whitePoint, 0.1f, 1f);
            _settings.invertDepth = EditorGUILayout.Toggle(
                new GUIContent("Invert", "어두운 곳이 튀어나오게."), _settings.invertDepth);
            _settings.frameLift = EditorGUILayout.Slider(
                new GUIContent("Frame Lift", "카드 테두리를 들어올려 프레임이 아트 위에 뜨게 한다."), _settings.frameLift, 0f, 1f);
            _settings.frameWidth = EditorGUILayout.Slider("Frame Width", _settings.frameWidth, 0.005f, 0.3f);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Shape", EditorStyles.miniBoldLabel);
            _settings.edgeRadius = EditorGUILayout.IntSlider(
                new GUIContent("Edge Radius", "엣지를 지키면서 안쪽만 펴는 범위. 0 이면 끈다."),
                _settings.edgeRadius, 0, 24);
            _settings.edgeStrength = EditorGUILayout.Slider(
                new GUIContent("Edge Sharpness", "작을수록 피사체 실루엣을 날카롭게 남긴다."),
                _settings.edgeStrength, 0.01f, 0.3f);
            _settings.depthLayers = EditorGUILayout.IntSlider(
                new GUIContent("Layers", "0 = 연속 높이. 2~8 이면 그 개수의 평면으로 끊어 디오라마처럼 보이게 한다."),
                _settings.depthLayers, 0, 8);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Flat (움직이면 안 되는 영역)", EditorStyles.miniBoldLabel);
            _settings.flattenOutsideArtWindow = EditorGUILayout.Toggle(
                new GUIContent("Flatten Outside Art", "아트 창 바깥(프레임·텍스트)을 통째로 고정한다."),
                _settings.flattenOutsideArtWindow);
            _settings.flatFeather = EditorGUILayout.Slider(
                new GUIContent("Flat Feather", "고정 영역 경계를 부드럽게."), _settings.flatFeather, 0.001f, 0.1f);

            int rectCount = _settings.flatRects != null ? _settings.flatRects.Length : 0;
            int newCount = EditorGUILayout.IntSlider(
                new GUIContent("Flat Rects", "추가로 고정할 사각형. 풀아트 카드의 텍스트 블록 등."),
                rectCount, 0, 4);
            if (newCount != rectCount)
            {
                var next = new Rect[newCount];
                for (int i = 0; i < newCount; i++)
                    next[i] = i < rectCount ? _settings.flatRects[i] : new Rect(0f, 0f, 1f, 0.3f);
                _settings.flatRects = next;
            }
            if (_settings.flatRects != null)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < _settings.flatRects.Length; i++)
                {
                    Rect r = _settings.flatRects[i];
                    var xy = EditorGUILayout.Vector2Field($"#{i} pos (x,y)", new Vector2(r.x, r.y));
                    var wh = EditorGUILayout.Vector2Field($"#{i} size (w,h)", new Vector2(r.width, r.height));
                    _settings.flatRects[i] = new Rect(xy.x, xy.y, Mathf.Max(wh.x, 0.01f), Mathf.Max(wh.y, 0.01f));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Foil Mask", EditorStyles.boldLabel);
            _settings.foilMode = (FoilMode)EditorGUILayout.EnumPopup(
                new GUIContent("Mode", "포일이 깔릴 영역. 전면 홀로면 Full Card."), _settings.foilMode);

            if (_settings.foilMode == FoilMode.ArtWindow)
            {
                Rect w = _settings.artWindow;
                w.x      = EditorGUILayout.Slider("Window X", w.x, 0f, 1f);
                w.y      = EditorGUILayout.Slider("Window Y", w.y, 0f, 1f);
                w.width  = EditorGUILayout.Slider("Window W", w.width, 0.02f, 1f);
                w.height = EditorGUILayout.Slider("Window H", w.height, 0.02f, 1f);
                _settings.artWindow = w;
                _settings.artFeather = EditorGUILayout.Slider("Feather", _settings.artFeather, 0f, 0.3f);
            }
            else if (_settings.foilMode == FoilMode.FullArtAdaptive)
            {
                _settings.highlightStart = EditorGUILayout.Slider(
                    new GUIContent("Highlight Start", "이 휘도부터 포일을 줄인다."),
                    _settings.highlightStart, 0f, 1f);
                _settings.highlightRolloff = EditorGUILayout.Slider(
                    new GUIContent("Highlight Rolloff", "가장 밝은 곳에서 포일을 얼마나 줄일지."),
                    _settings.highlightRolloff, 0f, 1f);
            }
            else if (_settings.foilMode != FoilMode.FullCard)
            {
                _settings.foilThreshold = EditorGUILayout.Slider("Threshold", _settings.foilThreshold, 0f, 1f);
                _settings.foilSoftness  = EditorGUILayout.Slider("Softness", _settings.foilSoftness, 0.01f, 1f);
            }

            dirty |= EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Preview", GUILayout.Height(24)) || dirty)
                BuildPreviews();

            if (_depthPreview != null && _foilPreview != null)
            {
                EditorGUILayout.Space(6);
                float w = (EditorGUIUtility.currentViewWidth - 34f) * 0.5f;
                float h = w * _source.height / Mathf.Max(_source.width, 1);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField("Depth", EditorStyles.miniBoldLabel);
                        GUILayout.Box(_depthPreview, GUIStyle.none, GUILayout.Width(w), GUILayout.Height(h));
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField("Foil", EditorStyles.miniBoldLabel);
                        GUILayout.Box(_foilPreview, GUIStyle.none, GUILayout.Width(w), GUILayout.Height(h));
                    }
                }
            }

            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(_depthPreview == null))
            {
                if (GUILayout.Button("Generate and Import", GUILayout.Height(30)))
                    Generate();
            }

            EditorGUILayout.EndScrollView();
        }

        void BuildPreviews()
        {
            HoloCardBaker.Bake(_source, _settings, out Texture2D depth, out Texture2D foil);
            if (depth == null) return;

            DestroyPreview(ref _depthPreview);
            DestroyPreview(ref _foilPreview);
            _depthPreview = depth;
            _foilPreview = foil;
        }

        void Generate()
        {
            if (!HoloCardBaker.Generate(_source, _settings, out string depthPath, out string foilPath))
            {
                EditorUtility.DisplayDialog("Holo Baker", "소스가 프로젝트 에셋이 아닙니다.", "확인");
                return;
            }

            AssetDatabase.Refresh();
            var depthAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(depthPath);
            EditorGUIUtility.PingObject(depthAsset);
            Debug.Log($"[Holo Baker] 생성 완료\n  {depthPath}\n  {foilPath}", depthAsset);
        }

        static void DestroyPreview(ref Texture2D tex)
        {
            if (tex == null) return;
            DestroyImmediate(tex);
            tex = null;
        }
    }
}
