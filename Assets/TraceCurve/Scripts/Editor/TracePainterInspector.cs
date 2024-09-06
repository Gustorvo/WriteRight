using UnityEngine;
using UnityEditor;

namespace TraceCurve.Editor
{
	[CustomEditor(typeof(TracePainter))]
	public class TracePainterInspector : UnityEditor.Editor
	{
		private SerializedProperty renderTextureQuality;
		private SerializedProperty renderTexture;
		private SerializedProperty traceObject;
		private SerializedProperty brushObject;
		private SerializedProperty brushTexture;
		private SerializedProperty brushScale;
		private SerializedProperty brushExtraBounds;
		private SerializedProperty brushShader;
		private SerializedProperty maskShader;
		private SerializedProperty canDraw;
		private SerializedProperty canFill;
		private SerializedProperty setBrushAngleFollowPath;
		private SerializedProperty brushAngleOffset;

		void OnEnable()
		{
			renderTextureQuality = serializedObject.FindProperty("RenderTextureQuality");
			renderTexture = serializedObject.FindProperty("RenderTexture");
			traceObject = serializedObject.FindProperty("TraceObject");
			brushObject = serializedObject.FindProperty("BrushObject");
			brushTexture = serializedObject.FindProperty("BrushTexture");
			brushScale = serializedObject.FindProperty("BrushScale");
			brushExtraBounds = serializedObject.FindProperty("BrushExtraBounds");
			brushShader = serializedObject.FindProperty("BrushShader");
			maskShader = serializedObject.FindProperty("MaskShader");
			canDraw = serializedObject.FindProperty("canDraw");
			canFill = serializedObject.FindProperty("canFillByPositions");
			setBrushAngleFollowPath = serializedObject.FindProperty("setBrushAngleFollowPath");
			brushAngleOffset = serializedObject.FindProperty("brushAngleOffset");
		}

		public override bool RequiresConstantRepaint()
		{
			return true;
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			EditorGUILayout.PropertyField(renderTextureQuality, new GUIContent("Quality"));
			EditorGUI.EndDisabledGroup();
			if (renderTexture.objectReferenceValue != null)
			{
				EditorGUILayout.TextArea(string.Empty, GUI.skin.horizontalSlider, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				var rect = GUILayoutUtility.GetRect(160, 120, GUILayout.ExpandWidth(true));
				GUI.DrawTexture(rect, (Texture) renderTexture.objectReferenceValue, ScaleMode.ScaleToFit);
				EditorGUILayout.TextArea(string.Empty, GUI.skin.horizontalSlider, GUILayout.Height(EditorGUIUtility.singleLineHeight));
			}

			EditorGUILayout.PropertyField(traceObject, new GUIContent("Trace Object"));
			EditorGUILayout.PropertyField(brushObject, new GUIContent("Brush Object"));
			EditorGUILayout.PropertyField(brushTexture, new GUIContent("Brush Texture"));
			EditorGUILayout.PropertyField(brushScale, new GUIContent("Brush Scale"));
			EditorGUILayout.PropertyField(brushExtraBounds, new GUIContent("Brush Extra Bounds"));

			if (brushShader.objectReferenceValue == null)
			{
				EditorGUILayout.PropertyField(brushShader, new GUIContent("Brush Shader"));
			}

			if (maskShader.objectReferenceValue == null)
			{
				EditorGUILayout.PropertyField(maskShader, new GUIContent("Mask Shader"));
			}
			
			EditorGUILayout.PropertyField(canDraw, new GUIContent("Can Draw"));
			EditorGUILayout.PropertyField(canFill, new GUIContent("Can Fill By Positions"));
			EditorGUILayout.PropertyField(setBrushAngleFollowPath, new GUIContent("Set Brush Angle Follow Path"));
			EditorGUILayout.Slider(brushAngleOffset, -180f, 180f, new GUIContent("Brush Angle Offset"));

			if (Application.isPlaying && renderTexture.objectReferenceValue != null)
			{			
				var painter = (TracePainter) target;
				if (GUILayout.Button("Clear"))
				{
					painter.Clear();
				}

				if (GUILayout.Button("Fill"))
				{
					painter.Fill();
				}
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
}