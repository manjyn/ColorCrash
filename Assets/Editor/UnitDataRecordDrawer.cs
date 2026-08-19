using UnityEngine;
using UnityEditor;
using ColorCrash.Units;

namespace ColorCrash.EditorScripts
{
    /// <summary>
    /// UnitDataRecord 구조체의 리스트 내부 요소를 직접 커스텀하여 그리는 클래스입니다.
    /// 리스트(배열) 내부의 아이템들은 CustomEditor가 아닌 PropertyDrawer로 접근해야 확실하게 너비가 적용됩니다.
    /// </summary>
    [CustomPropertyDrawer(typeof(UnitDataRecord))]
    public class UnitDataRecordDrawer : PropertyDrawer
    {
        // ★ 변수명(Label) 너비를 강제 고정하는 핵심 변수 (원하시는 대로 수정 가능)
        private const float CUSTOM_LABEL_WIDTH = 150f;

        // 리스트 아이템의 전체 높이를 내부 필드 개수에 맞게 동적으로 계산
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) 
                return EditorGUIUtility.singleLineHeight;

            float totalHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var iterator = property.Copy();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) 
                    break;
                
                totalHeight += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }
            return totalHeight + 4f; // 하단 여백
        }

        // 실제 GUI를 화면에 그리는 로직
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 1. 접기/펴기(Foldout) 헤더 그리기
            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            
            string displayName = label.text;
            
            // 편의성 보너스: 'Element 0' 대신 'UnitId' 값을 제목으로 표시해 줍니다.
            var idProp = property.FindPropertyRelative("unitId");
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
            {
                displayName = idProp.stringValue;
            }

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayName, true);

            // 2. 내부 필드 그리기 (펼쳐졌을 때만)
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                // ★ 핵심: 유니티의 기본 너비를 무시하고 우리가 원하는 너비로 덮어씌움
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = CUSTOM_LABEL_WIDTH;

                float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var iterator = property.Copy();
                bool enterChildren = true;

                // 구조체 내부의 모든 변수(maxHp, attackDamage 등)를 순회하며 그리기
                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) 
                        break;

                    float fieldHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect fieldRect = new Rect(position.x, currentY, position.width, fieldHeight);
                    
                    EditorGUI.PropertyField(fieldRect, iterator, true);
                    
                    currentY += fieldHeight + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false; // 첫 번째 깊이의 자식들만 탐색
                }

                // 렌더링이 끝나면 다른 UI에 영향을 주지 않도록 원래 너비로 원상 복구
                EditorGUIUtility.labelWidth = originalLabelWidth;
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}
