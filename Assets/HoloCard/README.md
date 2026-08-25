# Holo Card Forge

[poke-holo](https://poke-holo.simey.me/) 의 홀로그래픽 포일을 Unity URP 로 옮기고,
그 위에 **진짜 레이마칭 패럴랙스(POM)** 와 **두께 있는 카드 메시**를 얹은 것.

웹 원본은 CSS 그라디언트 두 장을 `color-dodge` 로 겹치는 방식이라 카드가 평평하다.
여기서는 높이맵을 시선 방향으로 레이마칭해서 앞쪽 레이어가 뒤를 실제로 가리고,
카드 자체도 부피가 있는 메시라 기울이면 옆면이 드러난다.

---

## 5분 안에 돌려보기

Unity 메뉴에서:

| 메뉴 | 하는 일 |
|---|---|
| `Tools > Holo Card > Create Demo Scene` | 카드 한 장짜리 3D 데모 씬 (카메라·조명·배경·프리셋·머티리얼 포함) |
| `Tools > Holo Card > Create Card Gallery Scene` | **가지고 있는 카드를 전부 늘어놓고 구경하는 씬.** 클릭하면 확대 |
| `Tools > Holo Card > Create UI Demo Scene` | uGUI 캔버스 위의 카드 데모 씬 |
| `Tools > Holo Card > Create Card Prefab` | 씬은 건드리지 않고 카드 프리팹만 |
| `Tools > Holo Card > Depth and Foil Baker` | 카드 이미지 한 장에서 Depth·Foil 맵 생성 (미리보기 있음) |
| `Tools > Holo Card > Bake Selected Textures` | 프로젝트 창에서 고른 텍스처들을 기본값으로 일괄 굽기 |
| `Tools > Holo Card > Download Sample Cards` | 예시 카드 13장을 CDN 에서 받아 굽고 머티리얼까지 생성 |
| `Tools > Holo Card > Rebake Sample Cards` | 다시 받지 않고 Depth·Foil·프리셋만 재생성 |
| `Tools > Holo Card > Generate Pack Art` | 팩 포장지와 카드 뒷면 텍스처를 절차적으로 생성 |
| `Tools > Holo Card > Create Pack Opening Scene` | 카드팩 개봉 연출 씬 |

### 씬 네 개

| 씬 | 용도 |
|---|---|
| `Scenes/HoloCardDemo` | 샘플 카드 한 장. 셰이더 값을 만져 보는 용도 |
| `Scenes/HoloCardGallery` | **카드를 전부 늘어놓고 구경하는 씬** |
| `Scenes/HoloCardUIDemo` | uGUI 캔버스 위의 카드 |
| `PackOpening/Scenes/PackOpening` | 카드팩 개봉 가챠 연출 |

갤러리와 팩 개봉 양쪽에서 **카드를 클릭하면 크게 볼 수 있다.**

| 조작 | 동작 |
|---|---|
| 좌클릭 | 카드 확대 / 다시 클릭하면 해제 |
| 빈 곳 클릭, `ESC` | 해제 |
| `←` `→` | 확대 상태에서 이웃 카드로 이동 |

확대된 카드는 화면 어디서든 포인터를 따라가고 기울기 범위도 넓어진다
(`HoloCardInspector` 의 `focusedTiltAngle`). 갤러리에서 작게 볼 때는 잘 안 보이는
패럴랙스 깊이와 글리터가 이 상태에서 제대로 드러난다.

Play 를 누르고 카드 위에서 마우스를 움직이면 된다. 손을 떼고 2.2초가 지나면
자동 회전으로 돌아간다.

---

## 파일

```
Assets/HoloCard/
  Shaders/
    HoloCardCore.hlsl            여섯 레이어 전부. 3D·UI 셰이더가 공유하는 본체
    HolographicCard3D.shader     월드 공간 카드. 그림자·뎁스 패스 포함
    HolographicCardUI.shader     uGUI 용. 스텐실 마스킹·Rect 클리핑 대응
  Scripts/
    HoloCardController.cs        마우스·터치·자이로·자동 데모 + 스프링 감쇠
    HoloCardMesh.cs              두께와 둥근 모서리를 가진 카드 메시 생성
    HoloCardPrism.cs             외곽선 → 두께 있는 프리즘. 카드와 팩이 공유
    HoloCardInspector.cs         클릭하면 카드를 카메라 앞으로 끌어와 확대
    HoloCardPreset.cs            룩 프리셋 ScriptableObject + 프로퍼티 ID 캐시
    Editor/
      HoloCardSetup.cs           데모·갤러리·UI 씬, 프리팹, 머티리얼 원클릭 생성
      HoloCardBaker.cs           Depth·Foil 생성 로직 + 일괄 굽기 메뉴
      HoloCardTextureBaker.cs    베이커 UI (미리보기 창)
      HoloCardDownloader.cs      예시 카드 목록 + CDN 다운로드 + 종류별 굽기
      HoloCardMaterialEditor.cs  머티리얼 인스펙터 (프리셋 버튼·임포트 경고)
  Textures/                      Sample_Art / Sample_Depth / Sample_Foil
    Pokemon/                     예시용 실제 카드 스캔 (아래 참고)
  Materials/  Presets/  Prefabs/  Scenes/
```

---

## 카드팩 개봉

`Tools > Holo Card > Create Pack Opening Scene` 으로 만드는 가챠 연출 씬이다.

| 단계 | 내용 |
|---|---|
| `Idle` | 팩이 공중에 떠서 포인터를 따라 기운다. 클릭하면 시작 |
| `Tearing` | 상단 스트립이 지그재그 이음매를 따라 찢겨 날아가고 포일 조각이 터진다 |
| `Dealing` | 팩이 뒤로 물러나며 카드가 입구에서 한 장씩 **뒷면으로** 솟아 부채꼴로 깔린다. 카메라도 같이 후퇴 |
| `Browsing` | 카드를 클릭하면 뒤집히며 확대된다 |

카드가 나오는 동안 팩은 `packRecedeZ` 만큼 뒤로 물러난다. 카드는 카메라 쪽(작은 z)에
깔리므로 이렇게 해야 메시가 겹치지 않는다. 빈 팩은 마지막 카드가 나오자마자 떨어진다.
`fanSpacing` 은 카드 폭(약 0.64)보다 커야 카드끼리도 겹치지 않는다.

조작: 팩 클릭 → 개봉 / 카드 클릭 → 뒤집기+확대 / `←` `→` 이웃 카드 / `ESC` 해제 /
`R` 다시 뽑기.

**뒤집기에 별도 코드가 없다.** 카드를 뒷면(`Y 180°`)으로 깔아 두면,
`HoloCardInspector` 가 확대할 때 카드를 카메라 정면으로 돌리는 그 회전이
곧 뒤집기가 된다. 한 번 본 카드는 `PackOpeningDirector.OnFocusChanged` 가
원래 자세를 앞면으로 갱신해서 확대를 풀어도 앞면을 유지한다.

**뽑기 규칙** — `cardsPerPack`(기본 5) 중 `guaranteedRares`(기본 1) 만큼 레어를
확정으로 넣고 나머지는 일반에서 채운다. 레어는 마지막에 배치해 절정에서 나오게 한다.
구형 홀로(Base Set)가 일반, 현행 카드(V / VMAX / VSTAR / 레인보우 등)가 레어다.

### 구성

```
PackOpening/
  Scripts/
    CardPack.cs              팩 메시. 본체와 상단 스트립이 같은 지그재그 이음매를 공유
    PackOpeningDirector.cs   DOTween 시퀀스로 짠 연출
    HoloCardInfo.cs          카드 이름·등급
    Editor/
      PackArtGenerator.cs    팩 포장지·카드 뒷면 텍스처를 절차적으로 생성
      PackOpeningSetup.cs    씬 원클릭 생성
  Textures/                  PackWrap / CardBack (+ 각각의 Depth·Foil)
```

### 팩은 비닐, 뒷면은 종이

둘의 재질이 다르므로 셰이더도 다르게 간다.

**팩 포장지**는 홀로 셰이더를 쓰되 카드와는 값이 다르다. 무지개는 은은하게
깔고(`_HoloIntensity` 0.30) 대신 시트 반사와 넓은 글레어를 크게 올려야
"코팅된 종이"가 아니라 "필름"으로 읽힌다. 생성기가 Depth 에 **구김**을 새겨서
패럴랙스가 접힘을 실제로 파낸다. 등방성 노이즈를 그대로 쓰면 대리석 무늬가 되므로
노이즈를 세로로 길게 늘여 손으로 쥔 자국 같은 긴 접힘을 만든다.

**카드 뒷면은 홀로 셰이더를 쓰지 않는다.** URP Lit 에 Smoothness 0.12 인 무광
카드지다. 실제 카드 뒷면은 코팅이 없고, 여기에 무지개가 얹히면 앞면과 구분이 안 가서
"카드를 뒤집었다"는 느낌이 죽는다.

> 뒷면이 Lit 이라 **조명을 받아야 보인다.** 씬의 키·필 라이트는 뒤·옆에서 오므로
> 카메라를 향한 면을 비추는 `Front Fill` 을 따로 둔다. 카드 앞면과 팩 표면은
> 언릿이라 이 빛에 영향받지 않는다.

> 이 부분만 **DOTween 에 의존한다**(프로젝트에 이미 들어 있다). 셰이더 키트
> 본체(`Shaders/`, `Scripts/`)는 외부 의존성이 없다.

### 실제 카드 디자인을 쓰지 않은 이유

포장지와 카드 뒷면 도안은 저작물이라 그대로 쓸 수 없다. 대신 같은 문법
(방사형 광선 + 중앙 엠블럼 + 금색 테두리)만 빌린 오리지널 도안을 코드로 그린다.
`PackArtGenerator` 를 고치면 색·엠블럼·레이아웃을 바꿀 수 있다.

---

## 여섯 레이어

프래그먼트 셰이더의 실행 순서가 그대로 레이어 순서다.

| # | 레이어 | 핵심 프로퍼티 |
|---|---|---|
| 01 | **Parallax Occlusion Mapping** — 높이맵을 시선 방향으로 레이마칭 | `_ParallaxDepth` `_ParallaxSteps` |
| 02 | **색수차 분리** — R·G·B 를 서로 다른 깊이에서 샘플 | `_ParallaxChroma` |
| 03 | **회절 무지개** — 각도가 다른 두 격자의 간섭. IQ 코사인 팔레트라 밴딩 없음 | `_HoloScale` `_HoloAngle` `_HoloSpread` `_HoloContrast` `_HoloBlend` |
| 04 | **마이크로 패싯 글리터** — 해시로 만든 미세 반사면 | `_SparkleDensity` `_SparklePower` `_SparkleDepth` |
| 05 | **글레어 & 시트 반사** — 포인터 하이라이트 + 가우시안 밴드 | `_PointerUV` `_GlareSize` `_SheenIntensity` |
| 06 | **베벨 & 프레넬 림** — 테두리 두께감 | `_BevelWidth` `_RimPower` `_RimIntensity` |

프리셋은 머티리얼 인스펙터 상단 버튼으로 바로 적용된다.

| 프리셋 | 쓰는 곳 |
|---|---|
| `Standard Holo` | 기본. 아티팩트 프리뷰의 초기값 |
| `Rainbow Rare` | 무지개를 세게, 격자를 촘촘하게 |
| `Galaxy Foil` | 굵은 격자 + 글리터 폭발 |
| `Deep Diorama` | 패럴랙스 0.2 / 56스텝. 깊이를 보여줄 때 |
| `Mobile Lite` | 16스텝, 색수차 off |
| `Vintage Print` | **구형 카드 스캔용.** 아트 창에만 포일이 깔린 카드 |
| `Full Art Foil` | **현행 풀아트 카드용.** V / VMAX / VSTAR / 레인보우 / Radiant |

앞의 다섯은 아티팩트 프리뷰와 값이 같다. 뒤의 둘은 추가분이다.

나머지가 전부 *어두운 배경의 카드 아트*를 전제로 튜닝돼 있어서, 실제 카드 스캔처럼
밝고 불투명한 이미지에 그대로 쓰면 `_GlareIntensity` 0.32 × `_GlareSize` 0.7 이
카드 대부분을 덮어 인쇄면이 우윳빛으로 날아간다. `Vintage Print` 는 가산 레이어를
낮추고 포일 마스크 안쪽의 무지개를 올린 값이다.

`Full Art Foil` 은 한 걸음 더 간다. 현행 카드는 아트가 카드 전면을 덮고 그 자체로
이미 화려해서, 무지개를 세게 얹으면 그냥 탁해진다. 무지개는 인쇄면을 *스치는*
정도(`_HoloIntensity` 0.30)로만 두고 '반짝임'은 글리터로 낸다. 각도에 따라 입자가
터지는 쪽이 실제 풀아트 카드를 기울일 때의 느낌에 훨씬 가깝다.

---

## 내 카드 이미지 넣기

1. 카드 아트를 `Assets/` 아무 데나 임포트한다.
2. `Tools > Holo Card > Depth and Foil Baker` 를 열고 Card Art 에 물린다.
3. 슬라이더를 만지며 Preview 를 보고 **Generate and Import**.
   → 같은 폴더에 `<이름>_Depth.png`, `<이름>_Foil.png` 가 생기고 임포트 설정까지 맞춰진다.
4. 머티리얼의 Base Art / Depth / Foil Mask 에 각각 물린다.

### 깊이는 어떻게 만들어지나

**휘도는 깊이가 아니다.** 밝기를 그대로 높이로 쓰면 밝은 테두리가 "높고" 어두운
그림자가 "낮게" 나온다. 실제 깊이와 무관하다. 그래서 글자가 제멋대로 높이를 갖고
POM 에 밀려 번지고, 피사체 경계가 배경과 안 떨어져서 *누끼가 덜 딴 느낌*이 난다.

파이프라인은 다섯 단계다.

1. **원천 신호** — `Height From`
   - `Luminance` : 인쇄면의 은은한 요철. 풀아트처럼 배경/피사체 구분이 없을 때
   - `Subject From Background` : 아트 창 테두리에서 배경색을 표본으로 뽑고,
     거기서 얼마나 먼 색인가를 높이로 쓴다. **어두운 피사체와 밝은 배경을 구분할 수
     있어서 실루엣이 실제로 떨어져 나온다.** 액자형 구도(구형 카드)에 쓴다.
2. **잔 노이즈 제거** — `Blur`. 스캔 망점만 지우는 정도로 1~2면 충분하다.
3. **엣지 보존 스무딩** — `Edge Radius` / `Edge Sharpness`
   박스 블러를 크게 주면 노이즈와 함께 **피사체 실루엣까지** 뭉개진다. 그러면
   높이맵의 경계가 그림의 경계와 어긋나고, 패럴랙스가 경계를 넘나들며 번진다.
   가이디드 필터는 그림 자체를 가이드로 삼아 경계는 남기고 안쪽만 편다.
   `Edge Sharpness` 가 작을수록 실루엣이 날카롭게 남는다.
4. **계단화** — `Layers`
   0 이면 연속 높이. 연속 그라디언트는 물렁한 부조로 보이는데, 3~4개 평면으로
   끊으면 배경 / 중경 / 피사체가 분리된 디오라마로 읽힌다.
5. **평면 영역** — `Flatten Outside Art`, `Flat Rects`
   지정 영역을 높이 **1.0** 으로 못 박는다. POM 은 높이 1.0 에서 레이마칭을
   시작하므로 첫 반복에서 바로 교차 판정이 나고 **UV 오프셋이 정확히 0** 이 된다.
   즉 그 영역은 완전히 정지한다. 인쇄된 글자를 붙잡아 두는 확실한 방법이다.

`Frame Lift` 는 5단계가 없던 시절의 근사치다. 평면 영역을 쓰면 필요 없다.

**Foil** — 포일이 깔릴 영역.
- `Full Card` : 전면 홀로
- `Saturation` : 채도 높은 곳에만 (일러스트 위주)
- `Art Window` : 사각형으로 직접 지정 (포켓몬 카드처럼 아트 창이 뚜렷할 때)

### 텍스처 임포트 규칙

높이맵과 마스크는 **색이 아니라 데이터**다. 이걸 놓치면 패럴랙스가 뭉갠다.

```
sRGB (Color Texture) : OFF
Wrap Mode            : Clamp
Compression          : None 또는 High Quality
Non-Power of 2       : None        ← 카드 아트도 반드시
```

**Non-Power of 2 는 카드 아트에도 반드시 None 이어야 한다.** Unity 기본값
`ToNearest` 는 2의 거듭제곱이 아닌 텍스처를 가장 가까운 거듭제곱으로 리샘플하는데,
600×825 카드가 **512×1024** 가 되면서 종횡비가 0.727 → 0.5 로 망가진다. 이 값을
읽어 메시 폭을 잡으면 카드가 눈에 띄게 홀쭉해진다(약 1.45배). 아트도 비균등
리샘플로 뭉개진다.

베이커와 다운로더는 이 설정을 자동으로 맞춘다. 카드 비율을 코드에서 읽을 때도
임포트된 `Texture2D.width/height` 가 아니라 `HoloCardBaker.SourceAspect()` 로
**원본 파일 크기**를 읽어야 안전하다.

베이커가 만든 파일은 자동으로 이 설정이 들어간다. 손으로 그린 높이맵을 쓸 때만
직접 맞추면 되고, 머티리얼 인스펙터가 sRGB 가 켜져 있으면 경고와 함께 고쳐주는
버튼을 띄운다.

**높이맵 규약: 흰색 = 앞으로 튀어나옴.**

### 실제 카드 스캔을 쓸 때

`Tools > Holo Card > Download Sample Cards` 를 누르면 `images.pokemontcg.io` 에서
예시 카드 13장을 받아 종류에 맞게 굽고 머티리얼까지 만든다. 구형 홀로 5장과
현행 카드 8장(V / 풀아트 V / VMAX / VSTAR / 레인보우 / Radiant / Amazing /
Trainer Gallery)이라 셰이더가 각 레이아웃에서 어떻게 보이는지 한 씬에서 비교할 수 있다.

이미지를 리포에 커밋하지 않고 다운로드 스크립트만 두는 건 poke-holo 원본
(`simeydotme/pokemon-cards-css`)과 같은 방식이다. 그쪽도 카드 스캔은 한 장도
커밋하지 않고 자기가 만든 포일·글리터 텍스처만 넣어 뒀다.

> 이 아트는 The Pokémon Company 저작물이다. 로컬 실험용으로만 두고, 빌드 배포나
> 공개 리포 커밋에는 넣지 말 것. `.gitignore` 에 `Textures/Pokemon/` 을 추가해 뒀다.
>
> 폴더를 지우면 `Materials/HoloCard_<이름>.mat` 과 `HoloCardGallery.unity` 의
> 텍스처 참조가 끊긴다. 그 둘도 같이 지우면 되고, 나중에 다른 카드를 구운 뒤
> `Create Card Gallery Scene` 을 다시 돌리면 그대로 재생성된다.
> `HoloCardDemo` / `HoloCardUIDemo` 는 샘플 카드만 쓰므로 영향이 없다.

카드 종류마다 포일이 깔리는 곳이 달라서 처리도 갈린다. `HoloCardDownloader.CardStyle`
이 그 분류이고, 베이크 설정과 프리셋이 여기에 묶여 있다.

| Style | Foil 모드 | Depth | 프리셋 | 해당 카드 |
|---|---|---|---|---|
| `ClassicHolo` | `Art Window` | 진짜 디오라마 | `Vintage Print` | Base Set 계열 구형 홀로 |
| `ModernFullArt` | `Full Art Adaptive` | 거의 평면 | `Full Art Foil` | V / VMAX / VSTAR / 레인보우 / 시크릿 / Amazing / Trainer Gallery |
| `Radiant` | `Full Art Adaptive` | 거의 평면 | `Full Art Foil` | Radiant Rare |

**두 부류는 깊이를 다루는 철학이 다르다.**

구형 카드는 *액자 안에 그림이 들어 있는* 구조라 진짜 디오라마가 성립한다.
프레임과 텍스트를 높이 1.0 으로 못 박고, 아트 창 안에서만
`Subject From Background` + 계단화로 피사체를 배경에서 떼어낸다.
`_ParallaxDepth` 를 0.09 까지 줘도 인쇄면은 흔들리지 않는다.

풀아트는 카드 전체가 그림이라 **배경/피사체 경계라는 게 없다.** 여기에 디오라마를
억지로 넣으면 정확히 "누끼가 덜 따진" 느낌이 난다. 그래서 그림은 거의 움직이지
않게 두고(`_ParallaxDepth` 0.015), 입체감은 **인쇄면 위에 뜬 포일 층**이 만든다 —
`_SparkleDepth` 0.9 가 글리터를 아트보다 훨씬 크게 밀어내서 *유리 아래 인쇄 /
그 위 포일* 로 갈라진다. 실제 카드가 그렇게 생겼다.

이런 스캔 이미지를 다룰 때 알아둘 것:

- **구형 카드는 `Art Window` 모드.** 아트 창 좌표는
  `x 0.103 / y 0.479 / w 0.794 / h 0.402` (베이커의 "구형 포켓몬 카드" 버튼).
  전면에 포일을 깔면 텍스트 영역까지 무지개가 흘러 글씨가 안 읽힌다.
- **현행 풀아트는 `Full Art Adaptive`.** 전면 포일이되 밝은 인쇄면에서는 세기를
  줄인다. 균일하게 깔면 가산 합성이 포화돼 카드가 통째로 파스텔로 날아간다.
  실제 포일도 어두운 잉크 위에서 가장 잘 보인다.
- **글자가 있는 곳은 평면 영역으로 못 박는다.** 블러로 얼버무리지 말 것.
  블러를 세게 주면 글자와 함께 피사체 실루엣까지 뭉개져서 누끼가 무너진다.
  구형은 `Flatten Outside Art` 하나로 끝나고, 풀아트는 `Flat Rects` 로
  이름 바(`y 0.86~1.0`)와 기술 텍스트 블록(`y 0~0.44`)을 잡는다.
- **`_ParallaxDepth`** 는 구형 0.09, 풀아트 0.015. 위 표의 이유 참고.
- 스캔에는 이미 인쇄된 홀로 무늬가 찍혀 있다. 셰이더의 무지개가 그 위에 얹히면서
  **인쇄 무늬는 포일 기재처럼, 셰이더 무지개는 움직이는 반사처럼** 읽힌다.

카드 비율이 제각각이어도 갤러리 씬이 텍스처 종횡비를 읽어 메시 폭을 맞추므로
찌그러지지 않는다.

---

## 컨트롤러

`HoloCardController` 하나가 두 가지를 동시에 굴린다.

1. **Transform 회전** — 카드가 실제로 3D 공간에서 기운다. 실루엣이 움직이고
   그림자와 리플렉션이 따라온다.
2. **`_VirtualView`** — 웹 프리뷰와 같은 가상 시선 벡터.

머티리얼의 `_ViewBlend` 가 이 둘을 섞는다.

- `0` — 실제 카메라 시선만. 물리적으로 정확하다.
- `1` — 컨트롤러 시선만. poke-holo 웹과 동일한 거동. **UI 는 이쪽이 기본**
  (Screen Space - Overlay 캔버스에는 의미 있는 카메라 시선이 없다).
- `0.35` — 3D 기본값. 실제 기하에 웹 특유의 과장을 살짝 얹는다.

> 두 시선은 같은 각도에서 계산되므로 서로 보강한다. 부호를 뒤집으면 정확히
> 상쇄되어 패럴랙스가 사라지니 `GetEulerAngles()` 를 고칠 때 주의할 것.

| 필드 | 설명 |
|---|---|
| `source` | `PointerHover` / `PointerDrag`(모바일 권장) / `Gyro` / `AutoDemo` |
| `fallbackToAutoDemo` | 입력이 끊기면 자동 회전으로 |
| `maxTiltAngle` | 최대 기울기(도). 기본 16 |
| `popDistance` | 기울었을 때 카메라 쪽으로 띄우는 거리 |
| `stiffness` / `dampingRatio` | 스프링. ζ=0.86 이라 살짝 오버슛하고 안착한다 |
| `preset` | 시작할 때 머티리얼에 부어넣을 프리셋 |

이 프로젝트는 **New Input System 전용**(`activeInputHandler: 1`)이라 컨트롤러도
`UnityEngine.InputSystem` 만 쓴다. 자이로는 `AttitudeSensor` 로 읽는다.

---

## 카드 메시

`HoloCardMesh` 가 실제 TCG 카드 비율(63 × 88 mm)에 두께와 둥근 모서리를 가진
메시를 만든다. 서브메시가 둘로 나뉜다.

- **서브메시 0** — 앞면 → 홀로 셰이더
- **서브메시 1** — 뒷면 + 옆면 → 일반 Lit 머티리얼

앞면은 `-Z` 를 본다(Unity 기본 Quad 와 같은 규약). 탄젠트는 `(1,0,0,-1)`.

---

## 성능

카드 한 장은 화면을 거의 안 채우니 대부분 여유롭다. 20장 이상 동시에 띄운다면
아래 순서로 깎는다.

| 항목 | 비용 | 깎는 방법 |
|---|---|---|
| `_ParallaxSteps` | 픽셀당 텍스처 페치 N회 — 압도적 1위 | 화면에서 작아지면 16 이하로. 거리별 LOD 머티리얼이 가장 효과적 |
| `_ParallaxChroma` | 베이스 샘플링 1회 → 3회 | 모바일에서는 0. 근접 연출에서만 |
| Sparkle | 해시 3회 + pow 1회 | Density 를 낮추거나 노이즈 텍스처로 대체 |
| Rainbow | sin/cos 8회 | 생각보다 싸다. 거의 건드릴 필요 없음 |
| 목록 화면 | 안 보이는 카드도 풀 셰이더 | `Mobile Lite` 프리셋으로 교체 |

`_ParallaxDepth` 는 0.06~0.12 가 스위트 스폿. 0.2 를 넘기면 그레이징 각에서
늘어난 픽셀이 보인다. 더 깊게 가고 싶으면 Depth 대신 `_ParallaxSteps` 를 올린다.

---

## 컬러스페이스

이 프로젝트는 Linear 다. 그런데 poke-holo 의 `color-dodge` 는 sRGB 공간에서
일어나므로, 셰이더는 **포일 합성 구간만 감마 공간에서** 처리하고 마지막에
되돌린다 (`_GammaBlend`, 기본 켜짐).

깊이 셰이딩 곱셈도 반드시 이 구간 안쪽에 있어야 한다. 리니어에서 곱하면
어두운 배경이 통째로 들려서 카드가 뿌옇게 뜬다.

`_GammaBlend` 를 끄면 리니어 그대로 합성한다. 원본과 다른 그림이 나오지만
HDR·블룸과 섞을 때는 이쪽이 더 맞을 수 있다.

---

## 원본과의 검증

셰이더 수식은 아티팩트(Holo Card Forge)의 GLSL 프리뷰와 1:1 로 맞춰져 있다.
같은 시선 벡터·같은 파라미터에서 렌더한 결과를 픽셀 단위로 비교했을 때
평균 절대 오차 **2.6/255**, 평면 영역은 0(차이는 전부 리샘플링 에지에만 발생).

즉 프리뷰 슬라이더 값을 그대로 머티리얼에 넣으면 같은 그림이 나온다.

---

## 자주 겪는 문제

**패럴랙스가 안 보인다**
`_ParallaxDepth` 가 0 이거나 Depth 맵이 비어 있다. Depth 맵이 sRGB 로
임포트됐는지도 확인할 것.

**패럴랙스가 반대로 움직인다**
높이맵이 반전됐다. 베이커의 `Invert` 를 켜거나 이미지를 반전한다.
(규약: 흰색 = 튀어나옴)

**카드가 뿌옇게 뜬다**
먼저 **원본 해상도로** 보고 판단할 것. 스크린샷을 축소해서 보면 미세한 글리터
입자가 평균화되면서 실제보다 훨씬 뿌옇게 보인다. 실제로 과하다면 순서대로:
배경을 거의 검게 → `_SparkleDensity` 를 낮춤 → `_HoloIntensity`·`_HoloGrazing` 을 낮춤.

**멀리 있는 카드에서 글리터가 흰 막처럼 보인다**
셰이더가 화면상 셀 밀도를 재서 한 픽셀에 한 셀을 넘어가기 전에 글리터를 서서히
죽인다(`HoloSparkle` 의 `aaFade`). 그래도 뜨면 `_SparkleDensity` 를 낮춘다.
밀도는 "카드가 화면에서 몇 픽셀인가"에 맞춰야 한다 — 카드 높이 400px 에
밀도 210 이면 셀 하나가 2px 이라 개별 반짝임으로 보일 수가 없다.

**인쇄된 글자가 번진다**
글자 영역의 높이가 1.0 이 아니라서다. 베이커에서 `Flatten Outside Art` 를 켜거나
`Flat Rects` 로 그 영역을 잡는다. Blur 를 올려 얼버무리면 피사체 실루엣까지
뭉개져서 누끼가 무너지니 그쪽으로 가지 말 것.

**피사체가 배경에서 안 떨어진다 (누끼가 덜 딴 느낌)**
높이맵의 경계가 그림의 경계와 어긋나 있다. 순서대로:
`Height From` 을 `Subject From Background` 로 → `Blur` 를 1~2로 낮추고
`Edge Radius` 를 8~12로 → `Edge Sharpness` 를 낮춰 실루엣을 세우고
→ `Layers` 를 3~4로 끊는다.
그래도 안 되면 그 카드는 자동 추출로 안 되는 구도다. 베이커가 만든 Depth 를
포토샵에서 손보는 게 빠르다 (흰색 = 튀어나옴).

**그레이징 각에서 픽셀이 늘어난다**
`_ParallaxDepth` 를 낮추거나 `_ParallaxSteps` 를 올린다.

**UI 카드가 반응하지 않는다**
캔버스가 Screen Space - Overlay 면 카메라 시선이 없다. `_ViewBlend` 를 1 로
두고, 기울기를 보고 싶다면 Screen Space - Camera + 원근 카메라로 바꾼다.
