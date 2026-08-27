using System.Collections;
using System.IO;
using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 개봉 연출을 프레임 단위로 파일에 뽑는다. 연출 검증 전용 도구.
    ///
    /// 왜 필요한가: 플레이 중에 밖에서 스크린샷을 찍으면 왕복 지연이 그대로
    /// 샘플 간격이 되어 0.2초짜리 이펙트를 절대 못 잡는다. <see cref="Time.captureFramerate"/>
    /// 를 쓰면 Unity 가 실제 속도와 무관하게 프레임당 정확히 1/fps 만큼 시간을
    /// 진행시키므로, 몇 초가 걸리든 원하는 타이밍이 정확히 찍힌다.
    /// </summary>
    [AddComponentMenu("")]
    public class PackOpeningRecorder : MonoBehaviour
    {
        /// <summary>연출을 처음부터 다시 돌리며 프레임을 저장한다.</summary>
        /// <param name="advanceEvery">
        /// 0 보다 크면 개봉이 끝난 뒤 이 간격(초)마다 캐러셀을 한 칸 넘긴다.
        /// 레코더는 입력을 흉내 낼 수 없어서 (포인터·키보드는 실제 장치를 본다)
        /// 넘기기 연출을 담으려면 여기서 직접 불러 줘야 한다.
        /// </param>
        /// <param name="onFrame">
        /// 프레임마다 부른다(인자는 프레임 번호). 입력이 필요한 상호작용 —
        /// 갤러리에서 카드를 확대한다든가 — 을 정확한 프레임에 끼워 넣는 자리다.
        /// 레코더는 포인터·키보드를 흉내 낼 수 없다(실제 장치를 본다).
        /// </param>
        public static void Capture(Camera camera, PackOpeningDirector director,
                                   string directory, int frames, int fps, int width, int height,
                                   float advanceEvery = 0f, System.Action<int> onFrame = null)
        {
            var go = new GameObject("~PackOpeningRecorder");
            var rec = go.AddComponent<PackOpeningRecorder>();
            rec.StartCoroutine(rec.Run(camera, director, directory, frames, fps, width, height,
                                       advanceEvery, onFrame));
        }

        IEnumerator Run(Camera camera, PackOpeningDirector director,
                        string directory, int frames, int fps, int width, int height,
                        float advanceEvery, System.Action<int> onFrame)
        {
            Directory.CreateDirectory(directory);
            foreach (string old in Directory.GetFiles(directory, "*.png")) File.Delete(old);

            float nextAdvance = advanceEvery;
            int previousCapture = Time.captureFramerate;
            Time.captureFramerate = fps;

            if (director != null)
            {
                director.ResetToIdle();
                yield return null;
                director.BeginOpen();
            }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            for (int i = 0; i < frames; i++)
            {
                onFrame?.Invoke(i);

                // 이 프레임의 시뮬레이션이 다 끝난 뒤에 그려야 트윈이 반영된다.
                yield return new WaitForEndOfFrame();

                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = previousTarget;

                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                shot.Apply();
                RenderTexture.active = previousActive;

                File.WriteAllBytes(Path.Combine(directory, $"f{i:D3}.png"), shot.EncodeToPNG());

                // 개봉이 끝났으면 일정 간격으로 한 칸씩 넘겨 캐러셀도 담는다.
                // 결과 화면에 들어가면 자동 넘기기를 멈춘다. 계속 부르면 확대해 놓은
                // 걸 하네스가 스스로 풀어 버린다.
                if (advanceEvery > 0f && director != null &&
                    director.Current == PackOpeningDirector.Stage.Browsing &&
                    director.carousel != null && !director.carousel.InGallery)
                {
                    nextAdvance -= 1f / fps;
                    if (nextAdvance <= 0f)
                    {
                        director.carousel.Go(1);
                        nextAdvance = advanceEvery;
                    }
                }
            }

            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            Destroy(shot);
            rt.Release();
            Destroy(rt);

            Time.captureFramerate = previousCapture;
            Debug.Log($"[Pack Opening] 프레임 {frames}장 기록 완료 ({fps}fps) → {directory}");
            Destroy(gameObject);
        }
    }
}
