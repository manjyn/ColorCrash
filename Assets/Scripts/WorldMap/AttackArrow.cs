using UnityEngine;

namespace ColorCrash.WorldMap
{
    public class AttackArrow : MonoBehaviour
    {
        private WorldMapTile ownerTile;

        private void Awake()
        {
            // 화살표 오브젝트의 부모 또는 조상에서 WorldMapTile 컴포넌트를 획득
            ownerTile = GetComponentInParent<WorldMapTile>();
        }

        public void OnArrowClicked()
        {
            if (ownerTile != null && WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.ExecuteAttackFromAlly(ownerTile.GridX, ownerTile.GridY);
            }
        }

        public void OnArrowClickVictory()
        {
            // 전투 승리 처리 -> 해당 타일을 획득
            if (ownerTile != null && WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.ExecuteImmediateBattleResult(ownerTile.GridX, ownerTile.GridY, true);
            }
        }

        public void OnArrowClickDefeat()
        {
            // 전투 패배 처리 -> 해당 타일 획득 실패
            if (ownerTile != null && WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.ExecuteImmediateBattleResult(ownerTile.GridX, ownerTile.GridY, false);
            }
        }
    }
}
