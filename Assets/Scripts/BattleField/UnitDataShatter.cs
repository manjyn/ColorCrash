using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.Units
{
    /// <summary>
    /// 유닛 파편화 사망 연출을 수행하는 컴포넌트입니다.
    /// 오브젝트 풀링과 호환되도록 가비지 컬렉터(GC) 발생 없이 구현되었습니다.
    /// </summary>
    public class UnitDataShatter : MonoBehaviour
    {
        [Header("Shatter Settings")]
        [Tooltip("파편들이 튕겨나가는 폭발력")]
        public float explosionForce = 10f;
        [Tooltip("가상 중력 (Y축 떨어지는 가속도)")]
        public float gravity = 25f;
        [Tooltip("파편 연출 총 시간")]
        public float duration = 2.5f;
        [Tooltip("파편이 사라질 때 투명해지는데 걸리는 시간 (duration 내에 포함됨)")]
        public float fadeDuration = 0.5f;

        // GC 방지를 위한 캐싱 구조체
        private struct FragmentData
        {
            public Transform transform;
            public Transform originalParent;
            public MeshRenderer renderer;
            public Material material;
            public Vector3 initialLocalPosition;
            public Quaternion initialLocalRotation;
            public Color initialColor;
        }

        // GC 방지를 위해 리스트 대신 고정 길이 배열로 필드 보관
        private FragmentData[] fragments;
        private Vector3[] velocities;
        private bool[] isGrounded;
        
        private Action onCompleteCallback;
        private Coroutine shatterCoroutine;

        private void Awake()
        {
            // 하위의 모든 MeshRenderer를 탐색하여 초기 상태 캐싱
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            List<FragmentData> fragmentList = new List<FragmentData>(renderers.Length);

            foreach (var r in renderers)
            {
                // 바닥 표시 마크나 UI 등 파편화되지 않아야 하는 예외 처리
                if (r.gameObject.name.Contains("BottomMark") || r.gameObject.name.Contains("UI"))
                    continue;

                // 머티리얼 인스턴스화 방지 및 런타임 투명도 조절을 위해 캐싱
                Material mat = r.material; 
                Color initColor = Color.white;

                if (mat.HasProperty("_BaseColor"))
                {
                    initColor = mat.GetColor("_BaseColor");
                }
                else if (mat.HasProperty("_Color"))
                {
                    initColor = mat.GetColor("_Color");
                }

                fragmentList.Add(new FragmentData
                {
                    transform = r.transform,
                    originalParent = r.transform.parent, // 본래의 부모 계층을 정확히 기억
                    renderer = r,
                    material = mat,
                    initialLocalPosition = r.transform.localPosition,
                    initialLocalRotation = r.transform.localRotation,
                    initialColor = initColor
                });
            }

            fragments = fragmentList.ToArray();
            int count = fragments.Length;
            
            // 런타임 배열 할당을 피하기 위해 Awake에서 미리 초기화
            velocities = new Vector3[count];
            isGrounded = new bool[count];
        }

        /// <summary>
        /// 사망 연출을 시작합니다. 연출이 모두 끝나면 onComplete 콜백을 호출합니다.
        /// </summary>
        public void ExecuteShatter(Action onComplete)
        {
            this.onCompleteCallback = onComplete;
            
            // 파편화할 메쉬가 없는 경우 즉시 콜백 실행 후 종료
            if (fragments == null || fragments.Length == 0)
            {
                onCompleteCallback?.Invoke();
                return;
            }

            // 기존 코루틴이 돌고 있다면 정지 (방어적 코드)
            if (shatterCoroutine != null)
            {
                StopCoroutine(shatterCoroutine);
            }

            // 각 파편의 초기 속도 설정 및 부모 분리
            for (int i = 0; i < fragments.Length; i++)
            {
                // 월드 좌표계로 분리하여 본체의 이동/회전에 영향받지 않게 함
                fragments[i].transform.parent = null;
                
                // 랜덤 벡터 생성 (중심점을 기준으로 수평 무작위 분산)
                float randomAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float randomForce = UnityEngine.Random.Range(explosionForce * 0.5f, explosionForce);
                
                Vector3 dir = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
                // 위로 솟구치는 초기 힘 보정
                dir.y = UnityEngine.Random.Range(1f, 2f); 
                
                velocities[i] = dir.normalized * randomForce;
                isGrounded[i] = false;
            }

            shatterCoroutine = StartCoroutine(ShatterRoutine());
        }

        /// <summary>
        /// GC 발생 없이 매 프레임 파편들의 포물선 운동을 처리하는 수학 연산 코루틴
        /// </summary>
        private IEnumerator ShatterRoutine()
        {
            float elapsed = 0f;
            int count = fragments.Length;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // 모든 파편 위치 및 회전 갱신
                for (int i = 0; i < count; i++)
                {
                    if (isGrounded[i]) continue;

                    // 가상 중력을 적용하여 수직(Y축) 속도 감소
                    velocities[i].y -= gravity * Time.deltaTime;
                    
                    Vector3 pos = fragments[i].transform.position;
                    pos += velocities[i] * Time.deltaTime;
                    
                    // 날아가는 동안의 무작위 회전 효과 (속도 벡터 기반)
                    fragments[i].transform.Rotate(new Vector3(velocities[i].z, velocities[i].y, velocities[i].x) * 50f * Time.deltaTime);

                    // Y좌표가 0(바닥) 이하가 되면 충돌로 간주하고 이동 정지
                    if (pos.y <= 0f)
                    {
                        pos.y = 0f;
                        isGrounded[i] = true;
                    }

                    fragments[i].transform.position = pos;
                }

                // 지정된 시간이 지나면 서서히 투명해지는 Fade-out 처리
                if (elapsed > duration - fadeDuration)
                {
                    float fadeRatio = (duration - elapsed) / fadeDuration; // 1.0에서 0.0으로 감소
                    for (int i = 0; i < count; i++)
                    {
                        Color c = fragments[i].initialColor;
                        c.a *= fadeRatio;
                        ApplyColor(fragments[i].material, c);
                    }
                }

                // 메모리 할당 방지를 위해 new WaitForSeconds 없이 null 반환
                yield return null; 
            }

            // 시간이 다 되면 무조건 복구 절차 진행
            ResetFragments();
        }

        /// <summary>
        /// 오브젝트 풀링 반환을 위해 파편들을 원래 계층 구조와 위치로 완벽히 되돌립니다.
        /// </summary>
        private void ResetFragments()
        {
            for (int i = 0; i < fragments.Length; i++)
            {
                var frag = fragments[i];
                
                // 본래의 부모로 재설정 (다중 계층 뼈대 완벽 복구)
                frag.transform.parent = frag.originalParent;
                
                // 로컬 캐시 데이터 덮어쓰기
                frag.transform.localPosition = frag.initialLocalPosition;
                frag.transform.localRotation = frag.initialLocalRotation;
                
                // 알파값(색상) 원상 복구
                ApplyColor(frag.material, frag.initialColor);
            }

            // 모든 처리가 끝난 뒤 풀 반환 콜백 실행 (ex: UnitManager.Instance.DespawnUnit)
            onCompleteCallback?.Invoke();
            shatterCoroutine = null;
        }

        private void ApplyColor(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);
        }
    }
}
