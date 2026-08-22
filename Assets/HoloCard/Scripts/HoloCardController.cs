using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace HoloCard
{
    /// <summary>
    /// 카드를 기울이고 셰이더에 시선·포인터를 먹인다.
    ///
    /// 두 가지를 동시에 굴린다.
    ///   1) Transform 회전 — 카드가 실제로 3D 공간에서 기운다. 실루엣이 움직이고
    ///      리플렉션 프로브와 그림자가 따라온다. 웹 버전이 못 하는 부분.
    ///   2) _VirtualView — poke-holo 웹 프리뷰와 같은 가상 시선 벡터.
    ///      머티리얼의 _ViewBlend 로 실제 카메라 시선과 섞인다.
    ///
    /// 감쇠는 임계 감쇠에 가까운 스프링(ζ≈0.86)이라 손을 떼면 부드럽게 돌아온다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Holo Card/Holo Card Controller")]
    public class HoloCardController : MonoBehaviour
    {
        public enum TiltSource
        {
            /// <summary>포인터가 카드 위에 있을 때만 반응</summary>
            PointerHover,
            /// <summary>누르고 있는 동안만 반응 (모바일 권장)</summary>
            PointerDrag,
            /// <summary>기기 자이로</summary>
            Gyro,
            /// <summary>항상 자동 회전</summary>
            AutoDemo
        }

        [Header("Target")]
        [Tooltip("3D 카드. 비우면 자기 자신에서 찾는다.")]
        public Renderer targetRenderer;
        [Tooltip("uGUI 카드. Image 또는 RawImage.")]
        public Graphic targetGraphic;
        [Tooltip("포인터 좌표를 계산할 카메라. 비우면 Camera.main.")]
        public Camera targetCamera;

        [Header("Input")]
        public TiltSource source = TiltSource.PointerHover;
        [Tooltip("입력이 끊기면 자동 회전으로 돌아간다.")]
        public bool fallbackToAutoDemo = true;
        [Tooltip("마지막 입력 후 자동 회전까지의 시간(초).")]
        public float idleDelay = 2.2f;
        [Tooltip("카드 밖으로 나가도 계속 반응할지. 켜면 화면 전체가 입력 영역이 된다.")]
        public bool trackOutsideBounds = false;

        [Header("Transform Tilt")]
        public bool rotateTransform = true;
        [Tooltip("최대 기울기(도). 아티팩트 기본값 16.")]
        public float maxTiltAngle = 16f;
        [Tooltip("기울었을 때 카메라 쪽으로 띄우는 거리(로컬 단위).")]
        public float popDistance = 0.06f;

        [Header("Shader")]
        [Tooltip("셰이더에 넣는 가상 시선의 세기. 0 이면 Transform 만 기운다.")]
        [Range(0f, 1.5f)] public float shaderTiltRatio = 1f;
        [Tooltip("시작할 때 이 프리셋을 머티리얼에 적용한다.")]
        public HoloCardPreset preset;

        [Header("Spring")]
        [Tooltip("각진동수. 클수록 빠르게 따라붙는다.")]
        public float stiffness = 11f;
        [Tooltip("감쇠비. 1 이면 오버슛 없음.")]
        public float dampingRatio = 0.86f;
        [Tooltip("포인터 하이라이트가 따라오는 속도.")]
        public float pointerFollow = 11f;

        // ── 내부 상태 ────────────────────────────────────────────────────
        Vector2 _tilt;            // 현재 기울기 -1..1
        Vector2 _tiltVelocity;
        Vector2 _tiltTarget;
        Vector2 _pointerUV = new Vector2(0.5f, 0.5f);
        Vector2 _pointerTarget = new Vector2(0.5f, 0.5f);

        float _lastInputTime = -999f;
        bool  _hasInput;

        Vector3    _basePosition;
        Quaternion _baseRotation;

        MaterialPropertyBlock _mpb;
        Material _uiMaterial;
        RectTransform _rect;

        void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetGraphic  == null) targetGraphic  = GetComponent<Graphic>();
            _rect = transform as RectTransform;

            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;

            if (targetRenderer != null)
                _mpb = new MaterialPropertyBlock();

            // UI 는 카드마다 포인터 값이 달라야 하므로 머티리얼 인스턴스가 필요하다.
            if (targetGraphic != null && targetGraphic.material != null)
            {
                _uiMaterial = new Material(targetGraphic.material) { name = targetGraphic.material.name + " (Instance)" };
                targetGraphic.material = _uiMaterial;
            }

            ApplyPreset();
        }

        void OnDestroy()
        {
            if (_uiMaterial != null) Destroy(_uiMaterial);
        }

        void OnEnable()
        {
            if (source == TiltSource.Gyro) EnableGyro(true);
        }

        void OnDisable()
        {
            EnableGyro(false);
        }

        /// <summary>
        /// 기울기의 기준이 되는 위치·회전. 카드를 다른 곳으로 옮겼을 때
        /// (예: 확대 보기) 여기를 갱신해야 그 자리에서 기울어진다.
        /// </summary>
        public Vector3 HomePosition => _basePosition;
        public Quaternion HomeRotation => _baseRotation;

        public void SetHome(Vector3 localPosition, Quaternion localRotation)
        {
            _basePosition = localPosition;
            _baseRotation = localRotation;
        }

        /// <summary>인스펙터에 물린 프리셋을 지금 머티리얼에 적용한다.</summary>
        public void ApplyPreset()
        {
            if (preset == null) return;
            if (_uiMaterial != null) preset.ApplyTo(_uiMaterial);
            else if (targetRenderer != null) preset.ApplyTo(targetRenderer.material);
        }

        /// <summary>외부에서 기울기를 직접 몰고 싶을 때. 각 성분 -1..1.</summary>
        public void SetTiltTarget(Vector2 target)
        {
            _tiltTarget = new Vector2(Mathf.Clamp(target.x, -1.2f, 1.2f),
                                      Mathf.Clamp(target.y, -1.2f, 1.2f));
            _pointerTarget = target * 0.5f + new Vector2(0.5f, 0.5f);
            _lastInputTime = Time.time;
            _hasInput = true;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            GatherInput();

            bool idle = !_hasInput || (Time.time - _lastInputTime > idleDelay);
            if (source == TiltSource.AutoDemo || (idle && fallbackToAutoDemo))
                DriveAutoDemo();

            IntegrateSpring(dt);
            ApplyTransform();
            ApplyShader();
        }

        // ── 입력 ─────────────────────────────────────────────────────────

        void GatherInput()
        {
            switch (source)
            {
                case TiltSource.PointerHover: ReadPointer(false); break;
                case TiltSource.PointerDrag:  ReadPointer(true);  break;
                case TiltSource.Gyro:         ReadGyro();         break;
                case TiltSource.AutoDemo:     _hasInput = false;  break;
            }
        }

        void ReadPointer(bool requirePress)
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) { _hasInput = false; return; }

            if (requirePress && !pointer.press.isPressed) { _hasInput = false; return; }

            Vector2 screen = pointer.position.ReadValue();
            if (!TryGetCardUV(screen, out Vector2 uv)) { _hasInput = false; return; }

            bool inside = uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
            if (!inside && !trackOutsideBounds) { _hasInput = false; return; }

            _pointerTarget = uv;
            _tiltTarget = new Vector2(Mathf.Clamp((uv.x - 0.5f) * 2f, -1.2f, 1.2f),
                                      Mathf.Clamp((uv.y - 0.5f) * 2f, -1.2f, 1.2f));
            _lastInputTime = Time.time;
            _hasInput = true;
        }

        /// <summary>스크린 좌표 → 카드 UV. UI 는 RectTransform, 3D 는 카드 평면 교차.</summary>
        bool TryGetCardUV(Vector2 screen, out Vector2 uv)
        {
            uv = new Vector2(0.5f, 0.5f);

            // ── uGUI
            if (_rect != null)
            {
                Camera uiCam = ResolveUICamera();
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, uiCam, out Vector2 local))
                    return false;

                Rect r = _rect.rect;
                if (r.width <= 0f || r.height <= 0f) return false;
                uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
                return true;
            }

            // ── 3D
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || targetRenderer == null) return false;

            Ray ray = cam.ScreenPointToRay(screen);

            // 카드는 로컬 XY 평면 위에 있고 앞면이 -Z 를 본다.
            Vector3 originLocal = transform.InverseTransformPoint(ray.origin);
            Vector3 dirLocal    = transform.InverseTransformDirection(ray.direction);
            if (Mathf.Abs(dirLocal.z) < 1e-6f) return false;

            float t = -originLocal.z / dirLocal.z;
            if (t < 0f) return false;

            Vector3 hitLocal = originLocal + dirLocal * t;

            // 로컬 바운즈로 정규화. 메시가 무엇이든(쿼드/생성 메시) 동작한다.
            Bounds b = targetRenderer.localBounds;
            if (b.size.x <= 0f || b.size.y <= 0f) return false;

            uv = new Vector2((hitLocal.x - b.min.x) / b.size.x,
                             (hitLocal.y - b.min.y) / b.size.y);
            return true;
        }

        Camera ResolveUICamera()
        {
            if (targetCamera != null) return targetCamera;
            Canvas canvas = _rect != null ? _rect.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return null;
            // Overlay 캔버스는 카메라를 null 로 넘겨야 좌표가 맞는다.
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        void ReadGyro()
        {
            AttitudeSensor sensor = AttitudeSensor.current;
            if (sensor == null || !sensor.enabled) { _hasInput = false; return; }

            Quaternion attitude = sensor.attitude.ReadValue();
            Vector3 up = attitude * Vector3.up;

            _tiltTarget = new Vector2(Mathf.Clamp(up.x * 2f, -1.2f, 1.2f),
                                      Mathf.Clamp(up.z * 2f, -1.2f, 1.2f));
            _pointerTarget = _tiltTarget * 0.5f + new Vector2(0.5f, 0.5f);
            _lastInputTime = Time.time;
            _hasInput = true;
        }

        void EnableGyro(bool on)
        {
            AttitudeSensor sensor = AttitudeSensor.current;
            if (sensor == null) return;
            if (on) InputSystem.EnableDevice(sensor);
            else    InputSystem.DisableDevice(sensor);
        }

        void DriveAutoDemo()
        {
            float s = Time.time * 0.6f;
            _tiltTarget = new Vector2(Mathf.Sin(s) * 0.75f,
                                      Mathf.Sin(s * 0.73f + 1.1f) * 0.75f);
            _pointerTarget = _tiltTarget * 0.5f + new Vector2(0.5f, 0.5f);
        }

        // ── 스프링 ───────────────────────────────────────────────────────

        void IntegrateSpring(float dt)
        {
            float k = stiffness * stiffness;
            float d = 2f * stiffness * dampingRatio;

            _tiltVelocity += ((_tiltTarget - _tilt) * k - _tiltVelocity * d) * dt;
            _tilt += _tiltVelocity * dt;

            float f = 1f - Mathf.Exp(-pointerFollow * dt);
            _pointerUV += (_pointerTarget - _pointerUV) * f;
        }

        // ── 출력 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 기울기 -1..1 을 Unity Euler 각으로. Transform 회전과 셰이더 시선이
        /// 반드시 같은 각도에서 나와야 서로 상쇄되지 않는다.
        ///
        /// 부호는 poke-holo 의 거동(포인터를 둔 쪽 모서리가 뒤로 물러난다)에 맞췄다.
        /// 웹은 CSS 좌표계라 +Z 가 시청자 쪽이고, Unity 는 카드 앞면이 -Z 를 보므로
        /// 같은 그림을 내려면 pitch·yaw 부호가 웹 코드와 반대가 된다.
        /// </summary>
        Vector2 GetEulerAngles(float ratio)
        {
            return new Vector2( _tilt.y * maxTiltAngle * ratio,    // pitch (X축)
                               -_tilt.x * maxTiltAngle * ratio);   // yaw   (Y축)
        }

        void ApplyTransform()
        {
            if (!rotateTransform) return;

            Vector2 e = GetEulerAngles(1f);
            float pitch = e.x, yaw = e.y;

            transform.localRotation = _baseRotation * Quaternion.Euler(pitch, yaw, 0f);

            if (popDistance != 0f)
            {
                float pop = Mathf.Clamp01(_tilt.magnitude) * popDistance;
                // 앞면이 -Z 를 보므로 카메라 쪽으로 띄우려면 -Z.
                transform.localPosition = _basePosition + _baseRotation * new Vector3(0f, 0f, -pop);
            }
        }

        void ApplyShader()
        {
            Vector2 e = GetEulerAngles(shaderTiltRatio);
            float pitch = e.x * Mathf.Deg2Rad;
            float yaw   = e.y * Mathf.Deg2Rad;

            // 카드를 Euler(pitch, yaw, 0) 으로 돌렸을 때 카메라가 탄젠트 스페이스의
            // 어디에 있는지. 앞면이 -Z 인 카드에 대해 R⁻¹·(0,0,-1) 을 T/B/N 에 투영한 값.
            // 이 식과 ApplyTransform 의 회전이 정확히 같은 각도를 써야
            // 가상 시선이 실제 카메라 시선을 상쇄하지 않고 보강한다.
            Vector4 view = new Vector4( Mathf.Sin(yaw),
                                       -Mathf.Sin(pitch) * Mathf.Cos(yaw),
                                        Mathf.Max(Mathf.Cos(pitch) * Mathf.Cos(yaw), 0.05f),
                                        0f);

            float tilt = Mathf.Clamp01(_tilt.magnitude);
            Vector4 puv = new Vector4(_pointerUV.x, _pointerUV.y, 0f, 0f);

            if (_uiMaterial != null)
            {
                _uiMaterial.SetVector(HoloCardIDs.VirtualView, view);
                _uiMaterial.SetVector(HoloCardIDs.PointerUV, puv);
                _uiMaterial.SetFloat(HoloCardIDs.Tilt, tilt);
            }
            else if (targetRenderer != null && _mpb != null)
            {
                targetRenderer.GetPropertyBlock(_mpb);
                _mpb.SetVector(HoloCardIDs.VirtualView, view);
                _mpb.SetVector(HoloCardIDs.PointerUV, puv);
                _mpb.SetFloat(HoloCardIDs.Tilt, tilt);
                targetRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
