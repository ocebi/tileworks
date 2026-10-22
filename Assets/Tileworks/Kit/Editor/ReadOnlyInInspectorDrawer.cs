using Tileworks.Kit.Unity;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Kit.Editor
{
    [CustomPropertyDrawer(typeof(ReadOnlyInInspectorAttribute))]
    public sealed class ReadOnlyInInspectorDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true)) EditorGUI.PropertyField(position, property, label, true);
        }
    }
}
