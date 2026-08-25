# ProjectHoloCardForgeDemo

[poke-holo.simey.me](https://poke-holo.simey.me/) 의 홀로그래픽 카드 효과를 Unity URP 로
옮기고, 웹이 못 하는 레이마칭 패럴랙스와 두께 있는 메시, 카드팩 개봉 연출까지 얹은
개인 토이 프로젝트.

기능 문서는 [Assets/HoloCard/README.md](Assets/HoloCard/README.md) 에 있다.
**이 파일은 코드를 건드리기 전에 알아야 할 함정만 모아 둔 것이다.**

## 환경

| | |
|---|---|
| Unity | 6000.4.1f1 |
| 파이프라인 | URP 17.4 |
| 컬러스페이스 | **Linear** |
| 입력 | **New Input System 전용** (`activeInputHandler: 1`) |
| 트위닝 | DOTween (PackOpening 에서만) |
| 언어 | 코드 주석·커밋 메시지 모두 한국어 |

`UnityEngine.Input.*` (레거시)는 **런타임 예외가 난다.** `UnityEngine.InputSystem` 만 쓸 것.

## 밟기 쉬운 지뢰

### Run In Background 가 꺼져 있다
에디터가 포커스를 잃으면 플레이 모드가 사실상 멈춘다(프레임이 안 돈다).
MCP 로 검증할 때 **스크린샷이 계속 이전 프레임을 잡는 원인**이므로,
플레이 진입 후 `Application.runInBackground = true` 를 먼저 넣고 시작할 것.

### 스크린샷 축소가 판단을 망친다
인라인 프리뷰로 내려받은 축소 이미지는 미세한 글리터를 평균 내서 실제보다 훨씬
뿌옇게 보인다. **룩 판단은 반드시 원본 해상도 1:1 크롭으로** 할 것.
(축소본 보고 튜닝했다가 값을 잘못 잡은 전례가 있다)

### 텍스처 임포트
- 카드 아트: `npotScale = None` **필수**. 기본값 `ToNearest` 가 600×825 를
  512×1024 로 리샘플해서 종횡비가 0.727 → 0.5 로 망가진다.
- 카드 비율을 코드에서 읽을 때는 임포트된 `Texture2D.width/height` 가 아니라
  `HoloCardBaker.SourceAspect()` (원본 파일 크기) 를 쓸 것.
- Depth·Foil: sRGB **끄고** Wrap `Clamp`. 색이 아니라 데이터다.

### 메시 규약 (`HoloCardPrism`)
- 외곽선은 XY 평면 **CCW**
- 앞면은 **-Z** 를 본다 (Unity 기본 Quad 와 동일)
- 탄젠트 `(1,0,0,-1)`
- 서브메시 **0=앞면 / 1=뒷면 / 2=옆면** → 머티리얼 배열 길이 3

### 셰이더: 합성은 감마 공간에서
poke-holo 의 `color-dodge` 는 sRGB 공간 연산이다. 프로젝트가 Linear 라서
`HoloCardCore.hlsl` 은 포일 합성 구간만 감마로 넘어갔다가 마지막에 되돌린다.
**깊이 셰이딩 곱셈도 반드시 그 블록 안에 있어야 한다.** 밖으로 빼면 어두운 영역이
통째로 들려서 카드가 뿌옇게 뜬다.

### 컨트롤러: 두 시선이 상쇄될 수 있다
`HoloCardController` 는 Transform 회전과 셰이더 가상 시선(`_VirtualView`)을 동시에
굴린다. 둘은 반드시 같은 `GetEulerAngles()` 에서 나와야 한다. 부호가 어긋나면
정확히 상쇄되어 **패럴랙스가 사라진다** (CSS 와 Unity 의 회전 방향 규약이 반대다).

### POM 의 한계
- 높이가 **정확히 1.0** 인 영역은 UV 오프셋이 0 이 된다 → 완전히 정지.
  인쇄된 글자를 붙잡는 방법이 이것이다 (블러로 얼버무리지 말 것).
- **수직 절벽 높이맵은 최악 케이스.** 솟은 실루엣이 밀려나면 그 뒤에 가려져 있던
  픽셀이 텍스처에 없어서 가장자리를 길게 늘여 메운다. 계단화 후에는 가이디드
  필터로 충분히 둥글릴 것.
- 깊이를 올리는 것보다 스텝을 올리는 게 안전하다. 확대해서 크게 기울일수록
  시야각이 커져 같은 늘어짐이 난다 (`focusedTiltAngle` 을 낮게).

### 글리터 밀도는 화면 크기에 맞춰야 한다
셀이 픽셀보다 작아지면 반짝임이 아니라 균일한 흰 막이 된다.
셰이더에 `fwidth` 기반 페이드가 들어 있지만, `_SparkleDensity` 자체를
"카드가 화면에서 몇 픽셀인가"에 맞추는 게 먼저다.

### 조명
카드 앞면과 팩 표면은 **언릿**(홀로 셰이더)이고, 카드 뒷면·옆면만 URP Lit 이다.
그래서 키·필 라이트를 아무리 만져도 앞면은 안 변하고, 반대로 뒤집힌 카드는
정면을 비추는 라이트가 없으면 새까맣게 나온다 (팩 씬의 `Front Fill` 이 그 역할).

### 베이커 성능
`HoloCardBaker.BoxBlur` 는 슬라이딩 윈도우라 픽셀당 O(1) 이다.
가이디드 필터가 이걸 여섯 번 부르므로 **반경에 비례하는 구현으로 되돌리면
에디터가 멈춘다.** 현재 카드 한 장 굽는 데 약 0.6초.

## 저작권

`Assets/HoloCard/Textures/Pokemon/` 의 카드 스캔은 The Pokémon Company 저작물이라
`.gitignore` 로 제외돼 있다. **커밋하지 말 것.** 로컬에서는
`Tools > Holo Card > Download Sample Cards` 로 받는다.

이 때문에 `HoloCard_<이름>.mat` 13개와 `HoloCardGallery` / `PackOpening` 씬은
클린 클론에서 텍스처 참조가 비어 있다. 다운로드 후 씬을 다시 생성하면 복구된다.

팩 포장지와 카드 뒷면 도안은 저작물을 피해 `PackArtGenerator` 가 절차적으로 그린
오리지널이다.

## 메뉴 (Tools > Holo Card)

| 메뉴 | 하는 일 |
|---|---|
| Create Demo Scene | 카드 한 장짜리 데모 씬 |
| Create Card Gallery Scene | 가진 카드를 전부 늘어놓은 씬 |
| Create UI Demo Scene | uGUI 캔버스 위의 카드 |
| Create Card Prefab | 프리팹만 |
| Depth and Foil Baker | 카드 아트 → Depth·Foil (미리보기 창) |
| Bake Selected Textures | 선택한 텍스처 일괄 굽기 |
| Download Sample Cards | 예시 카드 13장 다운로드 + 굽기 + 머티리얼 |
| Rebake Sample Cards | 다시 받지 않고 Depth·Foil·프리셋만 재생성 |
| Generate Pack Art | 팩 포장지·카드 뒷면 텍스처 생성 |
| Create Pack Opening Scene | 팩 개봉 연출 씬 |

**베이크 설정이나 프리셋을 고쳤으면 `Rebake Sample Cards` → 해당 씬 재생성**
순서로 반영해야 한다. 씬에 이미 만들어진 머티리얼은 자동으로 안 바뀐다.

## 구조

```
Assets/HoloCard/
  Shaders/     HoloCardCore.hlsl + 3D / UI 셰이더
  Scripts/     Controller · Mesh · Prism · Inspector · Preset
    Editor/    Baker · Downloader · Setup · MaterialEditor
  PackOpening/ CardPack · Director · ArtGenerator · Setup  (DOTween 의존)
  Presets/     프리셋 7종
  Textures/    오리지널 샘플 카드 (Pokemon/ 은 gitignore)
```

셰이더 키트 본체(`Shaders/`, `Scripts/`)는 **외부 의존성이 없다.**
DOTween 은 `PackOpening/` 에서만 쓴다. 이 경계를 유지할 것.
