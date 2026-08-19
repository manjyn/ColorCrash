using UnityEngine;
using UnityEditor;
using ColorCrash.Tower;

namespace ColorCrash.EditorScripts
{
    [CustomPropertyDrawer(typeof(TowerDataRecord))]
    public class TowerDataRecordDrawer : PropertyDrawer
    {
        private const float CUSTOM_LABEL_WIDTH = 150f;

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
            return totalHeight + 4f; 
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            
            string displayName = label.text;
            
            var idProp = property.FindPropertyRelative("towerId");
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
            {
                displayName = idProp.stringValue;
            }

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayName, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = CUSTOM_LABEL_WIDTH;

                float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var iterator = property.Copy();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) 
                        break;

                    float fieldHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect fieldRect = new Rect(position.x, currentY, position.width, fieldHeight);
                    
                    EditorGUI.PropertyField(fieldRect, iterator, true);
                    
                    currentY += fieldHeight + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false; 
                }

                EditorGUIUtility.labelWidth = originalLabelWidth;
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}
