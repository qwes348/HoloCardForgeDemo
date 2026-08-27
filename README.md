# Holo Card Forge

[poke-holo.simey.me](https://poke-holo.simey.me/) 의 홀로그래픽 카드 효과를 Unity URP 로
옮기고, 웹이 못 하는 것들을 얹은 개인 토이 프로젝트.

- **레이마칭 패럴랙스(POM)** — CSS 로는 못 하는 진짜 깊이. 인쇄면 뒤로 아트가 파인다
- **두께 있는 메시** — 판때기가 아니라 앞/뒤/옆이 있는 프리즘
- **카드팩 개봉 연출** — 봉지를 빛으로 갈라 카드를 뽑고, 손에 든 뭉치처럼 넘겨 본다

셰이더 키트(`Shaders/`, `Scripts/`)는 **외부 의존성이 없다.** DOTween 은 개봉 연출에서만 쓴다.

| | |
|---|---|
| Unity | 6000.4.1f1 |
| 파이프라인 | URP 17.4 |
| 컬러스페이스 | Linear |
| 입력 | New Input System 전용 |

---

## 무엇이 들어 있나

### 홀로 셰이더 — 일곱 레이어

프래그먼트 셰이더의 실행 순서가 그대로 레이어 순서다.

| # | 레이어 | 하는 일 |
|---|---|---|
| 01 | Parallax Occlusion Mapping | 높이맵을 시선 방향으로 레이마칭. 앞 레이어가 뒤를 실제로 가린다 |
| 02 | 색수차 분리 | R·G·B 를 서로 다른 깊이에서 샘플 → 두꺼운 유리의 굴절 |
| 03 | 회절 무지개 | 각도가 다른 두 격자의 간섭. IQ 코사인 팔레트라 밴딩이 없다 |
| 04 | 마이크로 패싯 글리터 | 해시로 만든 미세 반사면. 특정 각도에서만 개별 입자가 터진다 |
| 05 | 글레어 & 시트 반사 | 포인터 하이라이트 + 가우시안 밴드 |
| 06 | 베벨 & 프레넬 림 | 테두리 두께감 |
| 07 | 테두리 포일 | 카드 둘레를 도는 홀로그램 띠. 은색 바탕에 색이 어리고, 결마다 색이 갈라진다 |

프리셋 7종(`Standard Holo` / `Rainbow Rare` / `Galaxy Foil` / `Deep Diorama` /
`Mobile Lite` / `Vintage Print` / `Full Art Foil`)이 머티리얼 인스펙터 버튼으로 바로 붙는다.

**깊이맵과 포일 마스크는 카드 아트 한 장에서 구워 낸다.** 별도 리소스를 만들 필요가 없다 —
`Tools > Holo Card > Depth and Foil Baker` 가 채도·명도·엣지에서 높이를 추정하고
가이디드 필터로 다듬는다. 인쇄된 글자와 프레임은 높이 1.0 으로 못 박아서 패럴랙스가
돌아도 흔들리지 않는다.

### 카드팩 개봉

| 단계 | 내용 |
|---|---|
| `Idle` | 팩이 공중에 떠서 포인터를 따라 기운다 |
| `Tearing` | 빛줄기가 팩을 가르고 상단 스트립이 날아간다. 뜯긴 팩은 그 무게로 내려앉는다 |
| `Revealing` | 첫 카드가 팩 **입구에서** 솟아오르며 커지고, 빈 봉지는 화면 밖 아래로 빠진다 |
| `Browsing` | 카드 뭉치를 한 장씩 넘겨 본다 |
| `Gallery` | 마지막 장까지 넘기면 뽑은 카드를 전부 펼친 결과 화면 |

- **팩은 판때기가 아니라 봉지 메시다.** 크림프와 부푼 단면이 있는 셸을 뜯는 선으로 잘라 쓴다
- **카드는 봉지 두께 *안쪽*에서 나온다.** 팩 앞면이 불투명하니 깊이만으로 아랫동강이 가려진다
- **캐러셀은 손에 든 카드 뭉치다.** 옆에서 새 카드가 들어오는 게 아니라, 앞장이 치워지면서
  그 밑에 있던 장이 드러난다
- **레어도 7단계**(`◇`~`◇◇◇◇`, `★`~`★★★`)와 자리별 확률표. 표식은 하나씩 튀어나오고,
  레어면 배경이 무지개로 번쩍인다
- **결과 화면**은 순환하지 않는다. 다 넘기면 뽑은 카드가 한 장씩 펼쳐지고 등급이 붙는다

배경·슬래시·화살표·레어도 표식·NEW 뱃지는 전부 **절차적으로 그린다**
(`PackArtGenerator` / `PackStageArt`). 폰트 에셋도 스프라이트 시트도 없다.

---

## 시작하기

1. 이 리포지토리를 클론하고 Unity **6000.4.1f1** 로 연다
2. `Tools > Holo Card > Download Sample Cards` — 예시 카드 13장을 받고 Depth·Foil 을 굽는다
3. `Tools > Holo Card > Create Demo Scene` 으로 카드 한 장짜리 씬을 열고 Play

카드팩 개봉까지 보려면 `Tools > Holo Card > Create Pack Opening Scene`.
팩을 클릭해 개봉하고, `‹` `›` 클릭 / `←` `→` / `A` `D` / 가로 드래그로 카드를 넘긴다.
마지막 장에서 한 번 더 넘기면 결과 화면, `‹` 로 복귀, `R` 로 다시 뽑는다.

> 클린 클론에는 카드 스캔이 없다(아래 **저작권** 참고). 2번을 먼저 돌려야
> 머티리얼과 씬의 텍스처 참조가 채워진다.

### 메뉴 (`Tools > Holo Card`)

| 메뉴 | 하는 일 |
|---|---|
| Create Demo Scene | 카드 한 장짜리 데모 씬 |
| Create Card Gallery Scene | 가진 카드를 전부 늘어놓은 씬 |
| Create UI Demo Scene | uGUI 캔버스 위의 카드 |
| Create Card Prefab | 프리팹만 |
| Bake Pack Shell | 카드팩 FBX → 프로젝트 규약 봉지 메시 |
| Depth and Foil Baker | 카드 아트 → Depth·Foil (미리보기 창) |
| Bake Selected Textures | 선택한 텍스처 일괄 굽기 |
| Download Sample Cards | 예시 카드 13장 다운로드 + 굽기 + 머티리얼 |
| Rebake Sample Cards | 다시 받지 않고 Depth·Foil·프리셋만 재생성 |
| Generate Pack Art | 팩 포장지·카드 뒷면 텍스처 생성 |
| Generate Stage Art | 배경·슬래시·화살표·표식·뱃지 텍스처 생성 |
| Create Pack Opening Scene | 팩 개봉 연출 씬 |

---

## 구조

```
Assets/HoloCard/
  Shaders/     HoloCardCore.hlsl + 3D / UI 셰이더
  Scripts/     Controller · Mesh · Prism · Inspector · Preset
    Editor/    Baker · Downloader · Setup · MaterialEditor
  PackOpening/ CardPack · Slicer · Director · Carousel · Stage · Slash
               RarityDisplay · CarouselArrow · Recorder
               ShellBaker · ArtGenerator · StageArt · Setup
               + PackFilm.shader                            (DOTween 의존)
  Presets/     프리셋 7종
  Textures/    오리지널 샘플 카드
```

자세한 기능 문서는 **[Assets/HoloCard/README.md](Assets/HoloCard/README.md)**,
코드를 건드리기 전에 알아야 할 함정은 **[CLAUDE.md](CLAUDE.md)** 에 모여 있다.

---

## 연출을 눈으로 검증하는 법

개봉 연출은 스크린샷으로 확인할 수 없다. 0.16초짜리 광선은 캡처 왕복 지연 안에 끝난다.
그래서 `PackOpeningRecorder.Capture()` 가 `Time.captureFramerate` 로 프레임당 정확히
`1/fps` 씩 시간을 진행시키며 RenderTexture 를 파일로 뽑는다. 실제 속도와 무관하게
원하는 타이밍이 정확히 찍히고, 뽑은 PNG 는 ffmpeg 으로 GIF 로 묶으면 된다.

```csharp
PackOpeningRecorder.Capture(Camera.main, director, outDir,
                            frames: 260, fps: 30, width: 1280, height: 720,
                            advanceEvery: 1.3f);
```

---

## 저작권

**예시 카드 스캔과 팩 사진은 이 리포지토리에 없다.**

`Assets/HoloCard/Textures/Pokemon/` 과 `Assets/HoloCard/PackOpening/Textures/Pokemon/` 은
The Pokémon Company 저작물이라 `.gitignore` 로 제외돼 있다. 로컬에서는
`Tools > Holo Card > Download Sample Cards` 로 받아 쓰고, **커밋하지 않는다.**

이 때문에 클린 클론에서는 `HoloCard_<이름>.mat` 13개와 `HoloCardGallery` / `PackOpening`
씬의 텍스처 참조가 비어 있다. 다운로드 후 씬을 다시 생성하면 복구된다.

팩 포장지와 카드 뒷면 도안, 배경·이펙트·UI 표식은 저작물을 피해 절차적으로 그린
오리지널이다.

### 제3자 에셋 표기

팩 셸의 원본 FBX(`Assets/HoloCard/Model/`)는 CC BY 4.0 이라 **저작자 표기와 함께
리포에 들어 있다.** 다만 모델에 딸려 온 `texture/DIFFUSE.png` 는 실제 팩을 찍은
**사진**이라 제외했다 — 업로더의 CC BY 는 자기 모델까지만 미치고 사진에 찍힌
저작물에는 미치지 않는다. 클린 클론에서 임포트된 모델이 무지로 보이는 건 이 때문이고,
`PackShellBaker` 는 지오메트리만 쓰므로 `Bake Pack Shell` 은 그대로 돌아간다.

리포에는 한 번 구운 `PackOpening/Meshes/PackShell.asset` 도 함께 들어간다 —
**원본의 파생물이다.**

> "[Trading Card Pack](https://sketchfab.com/3d-models/trading-card-pack-26d1a87e47814d0ea3a710d169e3a671)"
> by [Mhew2 Creations](https://sketchfab.com/goonmize1),
> licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
> 이 프로젝트의 `PackShell.asset` 은 해당 모델을 리토폴로지·리베이크한 파생물이다.

홀로그램 효과의 원본 아이디어: [poke-holo](https://poke-holo.simey.me/) by Simey.
