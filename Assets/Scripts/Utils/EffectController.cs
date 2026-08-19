using UnityEngine;
using System;

namespace ColorCrash.Effects
{
    /// <summary>
    /// 파티클 시스템에 부착되어 재생이 끝나면 자동으로 풀에 반납하는 컨트롤러
    /// (Particle System의 Stop Action이 Callback으로 설정되어 있어야 작동합니다)
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class EffectController : MonoBehaviour
    {
        private ParticleSystem ps;
        private EffectType myType;
        private Action<EffectController, EffectType> onReleaseCallback;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
        }

        public void Initialize(EffectType type, Action<EffectController, EffectType> releaseAction)
        {
            // Awake에서 캐싱된 ps를 그대로 사용합니다 (GetComponent 중복 호출 제거)
            if (ps == null) ps = GetComponent<ParticleSystem>();

            myType = type;
            onReleaseCallback = releaseAction;
            
            // 파티클 시스템의 Stop Action을 강제로 Callback으로 설정
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        public void Play(Color tintColor)
        {
            var main = ps.main;
            main.startColor = tintColor; // 진영 색상으로 Color Tint 지정
            
            // 이전 위치 파티클 찌꺼기 리셋
            // (Stop을 호출하면 Callback이 즉시 실행되어 풀에 강제 반납되므로 Clear만 사용)
            ps.Clear(true);
            ps.Play(true);
        }

        // ParticleSystemStopAction.Callback 설정 시 재생이 멈추면 유니티가 자동 호출함
        private void OnParticleSystemStopped()
        {
            if (onReleaseCallback != null)
            {
                onReleaseCallback.Invoke(this, myType);
            }
        }
    }
}
