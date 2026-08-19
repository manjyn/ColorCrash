using UnityEngine;

namespace ColorCrash.Units
{
    /// <summary>
    /// 자식 모델의 Animator에서 발생하는 Animation Event(OnHit)를 부모 UnitBase로 전달하는 심플 릴레이 스크립트
    /// </summary>
    public class UnitAnimationRelay : MonoBehaviour
    {
        private UnitBase unitBase;

        private void Awake()
        {
            unitBase = GetComponentInParent<UnitBase>();
        }

        /// <summary>
        /// FBX 애니메이션 이벤트에서 호출되는 OnHit 메소드
        /// </summary>
        public void OnHit()
        {
            if (unitBase != null)
            {
                unitBase.OnHit();
            }
        }
    }
}
