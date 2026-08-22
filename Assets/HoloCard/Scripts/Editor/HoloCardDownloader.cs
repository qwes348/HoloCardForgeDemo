using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace HoloCard.Editor
{
    /// <summary>카드 종류. 포일이 어디에 깔리는지가 다르다.</summary>
    public enum CardStyle
    {
        /// <summary>구형 카드. 아트 창에만 포일이 깔린다. (Base Set 계열)</summary>
        ClassicHolo,
        /// <summary>현행 풀아트. V / VMAX / VSTAR / 레인보우 / 시크릿 — 카드 전면이 포일.</summary>
        ModernFullArt,
        /// <summary>Radiant. 전면 포일이지만 각진 에칭이라 격자를 굵게 간다.</summary>
        Radiant,
    }

    /// <summary>
    /// 예시용 카드를 Pokémon TCG 이미지 CDN 에서 받아 Depth·Foil 까지 굽고
    /// 머티리얼을 만든다.
    ///
    /// 이미지를 리포에 커밋하지 않고 이 스크립트만 두는 건 poke-holo 원본
    /// (simeydotme/pokemon-cards-css)과 같은 방식이다. 그쪽도 카드 스캔은 한 장도
    /// 커밋하지 않고 자기가 만든 포일·글리터 텍스처만 넣어 뒀다.
    ///
    /// 받은 아트는 The Pokémon Company 저작물이다. 로컬 실험용으로만 쓸 것.
    /// </summary>
    public static class HoloCardDownloader
    {
        const string Dir = "Assets/HoloCard/Textures/Pokemon";
        const string UrlFormat = "https://images.pokemontcg.io/{0}/{1}_hires.png";

        public struct CardEntry
        {
            public string name;     // 파일명 겸 씬 오브젝트 이름
            public string setId;    // "swsh7"
            public string number;   // "110"
            public CardStyle style;
            public string rarity;   // 표시용

            public string Url => string.Format(UrlFormat, setId, number);
            public string AssetPath => $"{Dir}/{name}.png";
        }

        /// <summary>
        /// poke-holo 데모(public/data/cards.json)가 쓰는 카드들 중에서 처리 방식이
        /// 서로 다른 것들만 골랐다. 종류당 한 장씩이라 셰이더가 각 레이아웃에서
        /// 어떻게 보이는지 한 씬에서 비교할 수 있다.
        /// </summary>
        public static readonly CardEntry[] Catalog =
        {
            // ── 구형 홀로. 아트 창에만 포일이 깔린 초기 카드.
            new CardEntry { name = "Charizard",        setId = "base1",     number = "4",       style = CardStyle.ClassicHolo,   rarity = "Rare Holo" },
            new CardEntry { name = "Blastoise",        setId = "base1",     number = "2",       style = CardStyle.ClassicHolo,   rarity = "Rare Holo" },
            new CardEntry { name = "Venusaur",         setId = "base1",     number = "15",      style = CardStyle.ClassicHolo,   rarity = "Rare Holo" },
            new CardEntry { name = "Mewtwo",           setId = "base1",     number = "10",      style = CardStyle.ClassicHolo,   rarity = "Rare Holo" },
            new CardEntry { name = "Zapdos",           setId = "base1",     number = "16",      style = CardStyle.ClassicHolo,   rarity = "Rare Holo" },

            // ── 현행 카드. 전면 포일.
            new CardEntry { name = "Rayquaza V",       setId = "swsh7",     number = "110",     style = CardStyle.ModernFullArt, rarity = "Rare Holo V" },
            new CardEntry { name = "Mew V",            setId = "swsh8",     number = "250",     style = CardStyle.ModernFullArt, rarity = "Rare Ultra (Full Art)" },
            new CardEntry { name = "Ditto VMAX",       setId = "swsh45",    number = "51",      style = CardStyle.ModernFullArt, rarity = "Rare Holo VMAX" },
            new CardEntry { name = "Charizard VSTAR",  setId = "swsh9",     number = "18",      style = CardStyle.ModernFullArt, rarity = "Rare Holo VSTAR" },
            new CardEntry { name = "Flareon VMAX",     setId = "swshp",     number = "SWSH180", style = CardStyle.ModernFullArt, rarity = "Rare Rainbow" },
            new CardEntry { name = "Celebi",           setId = "swsh4",     number = "9",       style = CardStyle.ModernFullArt, rarity = "Amazing Rare" },
            new CardEntry { name = "Charizard TG",     setId = "swsh11tg",  number = "TG03",    style = CardStyle.ModernFullArt, rarity = "Trainer Gallery" },
            new CardEntry { name = "Radiant Charizard", setId = "pgo",      number = "11",      style = CardStyle.Radiant,       rarity = "Radiant Rare" },
        };

        [MenuItem("Tools/Holo Card/Download Sample Cards", false, 22)]
        public static void DownloadAll()
        {
            bool go = EditorUtility.DisplayDialog(
                "예시 카드 받기",
                $"Pokémon TCG 이미지 CDN(images.pokemontcg.io)에서 카드 {Catalog.Length}장을 받습니다.\n" +
                "약 10MB. 받은 뒤 Depth·Foil 을 굽고 머티리얼까지 만듭니다.\n\n" +
                "이 아트는 The Pokémon Company 저작물입니다. 로컬 실험용으로만 쓰세요.\n" +
                "(.gitignore 에 Textures/Pokemon/ 이 들어 있습니다)",
                "받기", "취소");
            if (!go) return;

            Run(true);
        }

        /// <summary>
        /// 실제 작업. 다이얼로그 없이 돌릴 수 있도록 메뉴 항목과 분리해 뒀다
        /// (CI·스크립트에서 호출하거나 배치로 돌릴 때 필요하다).
        /// </summary>
        public static int Run(bool interactive)
        {
            Directory.CreateDirectory(Dir);

            var downloaded = new List<CardEntry>();
            var failed = new List<string>();

            for (int i = 0; i < Catalog.Length; i++)
            {
                CardEntry card = Catalog[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "예시 카드 받는 중", $"{card.name}  ({i + 1}/{Catalog.Length})",
                        (float)i / Catalog.Length))
                    break;

                if (Download(card.Url, card.AssetPath, out string error)) downloaded.Add(card);
                else failed.Add($"{card.name} ({card.setId}-{card.number}): {error}");
            }
            EditorUtility.ClearProgressBar();

            if (downloaded.Count > 0) Process(downloaded);

            string summary = $"[Holo Card] 받기 완료: {downloaded.Count}/{Catalog.Length}장";
            if (failed.Count > 0) summary += "\n실패:\n  " + string.Join("\n  ", failed);
            Debug.Log(summary);

            if (interactive && downloaded.Count > 0)
                EditorUtility.DisplayDialog("예시 카드 받기",
                    $"{downloaded.Count}장 완료.\n\n" +
                    "Tools > Holo Card > Create Card Gallery Scene 을 실행하면 " +
                    "전부 늘어놓은 씬이 만들어집니다.", "확인");

            return downloaded.Count;
        }

        static bool Download(string url, string path, out string error)
        {
            error = null;
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 60;
                var op = request.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(30);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    error = request.error;
                    return false;
                }

                byte[] data = request.downloadHandler.data;
                if (data == null || data.Length < 1024)
                {
                    error = "빈 응답";
                    return false;
                }

                File.WriteAllBytes(path, data);
                return true;
            }
        }

        /// <summary>
        /// 이미 받아 둔 카드를 다시 굽는다. 베이크 설정이나 프리셋을 손본 뒤
        /// 다시 다운로드하지 않고 반영할 때 쓴다. 머티리얼의 프리셋도 덮어쓴다.
        /// </summary>
        [MenuItem("Tools/Holo Card/Rebake Sample Cards", false, 23)]
        public static void RebakeAll()
        {
            var present = new List<CardEntry>();
            foreach (var c in Catalog)
                if (File.Exists(c.AssetPath)) present.Add(c);

            if (present.Count == 0)
            {
                EditorUtility.DisplayDialog("Holo Card",
                    "받아 둔 예시 카드가 없습니다.\nTools > Holo Card > Download Sample Cards 를 먼저 실행하세요.", "확인");
                return;
            }

            Process(present, forcePreset: true);
            Debug.Log($"[Holo Card] 다시 굽기 완료: {present.Count}장");
        }

        /// <summary>임포트 설정 → Depth·Foil 굽기 → 머티리얼 생성.</summary>
        static void Process(List<CardEntry> cards, bool forcePreset = false)
        {
            try
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    CardEntry card = cards[i];
                    EditorUtility.DisplayProgressBar("Depth·Foil 굽는 중", card.name, (float)i / cards.Count);

                    AssetDatabase.ImportAsset(card.AssetPath, ImportAssetOptions.ForceUpdate);
                    HoloCardBaker.ConfigureAsCardArt(card.AssetPath);

                    var art = AssetDatabase.LoadAssetAtPath<Texture2D>(card.AssetPath);
                    if (art == null) continue;

                    HoloCardBaker.Generate(art, BakeSettingsFor(card.style), out _, out _);

                    Material mat = HoloCardSetup.CreateCardMaterial(card.name, art, PresetFor(card.style));
                    if (forcePreset && mat != null)
                    {
                        var p = ScriptableObject.CreateInstance<HoloCardPreset>();
                        p.LoadBuiltin(PresetFor(card.style));
                        p.ApplyTo(mat);
                        Object.DestroyImmediate(p);
                        EditorUtility.SetDirty(mat);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public static HoloBakeSettings BakeSettingsFor(CardStyle style)
        {
            switch (style)
            {
                case CardStyle.ClassicHolo: return HoloBakeSettings.ClassicPokemonCard;
                case CardStyle.Radiant:     return HoloBakeSettings.ModernFullArt;
                default:                    return HoloBakeSettings.ModernFullArt;
            }
        }

        public static HoloCardPreset.Builtin PresetFor(CardStyle style)
        {
            switch (style)
            {
                case CardStyle.ClassicHolo: return HoloCardPreset.Builtin.VintagePrint;
                // Radiant 도 풀아트와 같은 계열로 간다. RainbowRare 는 어두운 아트를
                // 전제로 한 값이라 이미 화려한 Radiant 인쇄면 위에 얹으면 다 타버린다.
                case CardStyle.Radiant:     return HoloCardPreset.Builtin.FullArtFoil;
                default:                    return HoloCardPreset.Builtin.FullArtFoil;
            }
        }
    }
}
