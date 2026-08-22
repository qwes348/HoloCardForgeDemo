using UnityEditor;
using UnityEngine;

namespace HoloCard.Editor
{
    /// <summary>
    /// 홀로 카드 머티리얼 인스펙터. 기본 프로퍼티 목록 위에 프리셋 버튼과
    /// 텍스처 임포트 설정 경고를 얹는다.
    /// </summary>
    public class HoloCardMaterialEditor : ShaderGUI
    {
        static readonly (string label, HoloCardPreset.Builtin kind)[] Presets =
        {
            ("Standard Holo", HoloCardPreset.Builtin.StandardHolo),
            ("Rainbow Rare",  HoloCardPreset.Builtin.RainbowRare),
            ("Galaxy Foil",   HoloCardPreset.Builtin.GalaxyFoil),
            ("Deep Diorama",  HoloCardPreset.Builtin.DeepDiorama),
            ("Mobile Lite",   HoloCardPreset.Builtin.MobileLite),
            ("Vintage Print", HoloCardPreset.Builtin.VintagePrint),
            ("Full Art Foil", HoloCardPreset.Builtin.FullArtFoil),
        };

        HoloCardPreset _scratch;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;

            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);

            const int perRow = 4;
            for (int start = 0; start < Presets.Length; start += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = start; i < Mathf.Min(start + perRow, Presets.Length); i++)
                    {
                        var (label, kind) = Presets[i];
                        if (!GUILayout.Button(label, EditorStyles.miniButton)) continue;

                        if (_scratch == null) _scratch = ScriptableObject.CreateInstance<HoloCardPreset>();
                        _scratch.LoadBuiltin(kind);

                        Undo.RecordObject(material, "Apply Holo Preset");
                        foreach (var t in materialEditor.targets)
                            _scratch.ApplyTo(t as Material);
                        EditorUtility.SetDirty(material);
                    }
                }
            }

            EditorGUILayout.Space(6);
            WarnAboutDepthImport(material);
            EditorGUILayout.Space(2);

            base.OnGUI(materialEditor, properties);
        }

        /// <summary>
        /// 높이맵이 sRGB 로 임포트되면 패럴랙스가 뭉갠다. 가장 흔한 실수라 눈에 띄게 잡아준다.
        /// </summary>
        static void WarnAboutDepthImport(Material material)
        {
            if (material == null || !material.HasProperty(HoloCardIDs.DepthMap)) return;

            var depth = material.GetTexture(HoloCardIDs.DepthMap) as Texture2D;
            if (depth == null) return;

            string path = AssetDatabase.GetAssetPath(depth);
            if (string.IsNullOrEmpty(path)) return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !importer.sRGBTexture) return;

            EditorGUILayout.HelpBox(
                "Depth 맵이 sRGB(Color Texture)로 임포트되어 있습니다. 높이값이 왜곡돼 " +
                "패럴랙스가 뭉갭니다.", MessageType.Warning);

            if (!GUILayout.Button("sRGB 끄고 Clamp 로 다시 임포트")) return;

            importer.sRGBTexture   = false;
            importer.wrapMode      = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
