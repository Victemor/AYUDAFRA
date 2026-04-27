#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para DraggableItemDefinition.
    /// Mejora validación y automatiza parte del setup visual.
    /// </summary>
    [CustomEditor(typeof(DraggableItemDefinition))]
    public sealed class DraggableItemDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DraggableItemDefinition definition = (DraggableItemDefinition)target;

            SerializedProperty idProp = serializedObject.FindProperty("id");
            SerializedProperty displayNameProp = serializedObject.FindProperty("displayName");
            SerializedProperty inventorySpriteProp = serializedObject.FindProperty("inventorySprite");
            SerializedProperty worldPrefabProp = serializedObject.FindProperty("worldPrefab");

            EditorGUILayout.HelpBox(
                "Cada DraggableItemDefinition representa un objeto único del juego. " +
                "El ID se sincroniza con el nombre del asset.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(idProp);
            }

            EditorGUILayout.PropertyField(displayNameProp);
            EditorGUILayout.PropertyField(inventorySpriteProp);
            EditorGUILayout.PropertyField(worldPrefabProp);

            GUILayout.Space(6f);
            DrawPreview(inventorySpriteProp);

            GUILayout.Space(6f);
            DrawButtons(definition, inventorySpriteProp, worldPrefabProp);

            GUILayout.Space(6f);
            DrawWarnings(inventorySpriteProp, worldPrefabProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPreview(SerializedProperty inventorySpriteProp)
        {
            Sprite sprite = inventorySpriteProp.objectReferenceValue as Sprite;
            if (sprite == null)
            {
                return;
            }

            Rect previewRect = GUILayoutUtility.GetRect(72f, 72f, GUILayout.ExpandWidth(false));
            Texture2D preview = AssetPreview.GetAssetPreview(sprite);

            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(sprite);
            }

            if (preview != null)
            {
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            }
        }

        private void DrawButtons(
            DraggableItemDefinition definition,
            SerializedProperty inventorySpriteProp,
            SerializedProperty worldPrefabProp)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Sincronizar ID con nombre del asset"))
            {
                definition.SyncAutoFields();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button("Tomar sprite desde World Prefab"))
            {
                GameObject prefab = worldPrefabProp.objectReferenceValue as GameObject;
                Sprite detectedSprite = DraggableEditorUtility.FindBestSpriteFromWorldPrefab(prefab);

                if (detectedSprite != null)
                {
                    inventorySpriteProp.objectReferenceValue = detectedSprite;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWarnings(
            SerializedProperty inventorySpriteProp,
            SerializedProperty worldPrefabProp)
        {
            if (inventorySpriteProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Falta asignar Inventory Sprite.", MessageType.Warning);
            }

            GameObject prefab = worldPrefabProp.objectReferenceValue as GameObject;
            if (prefab == null)
            {
                EditorGUILayout.HelpBox("Falta asignar World Prefab.", MessageType.Warning);
                return;
            }

            if (DraggableEditorUtility.FindBestSpriteFromWorldPrefab(prefab) == null)
            {
                EditorGUILayout.HelpBox(
                    "El World Prefab no parece tener SpriteRenderer útil para auto-extraer preview.",
                    MessageType.Info);
            }
        }
    }
}
#endif