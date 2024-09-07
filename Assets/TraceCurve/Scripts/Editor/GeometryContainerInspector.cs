using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TraceCurve.Editor
{
	[CustomEditor(typeof(GeometryContainer))]
	public class GeometryContainerInspector : UnityEditor.Editor
	{
		private int focusedControlID = -1;
		private bool expandArray = true;
		private bool shouldFocusOnControl;
		private readonly Color HandlesColor = Color.green;
		private readonly Color HandlesCurveStartColor = new Color(68 / 255f, 197 / 255f, 203 / 255f);
		private readonly Color HandlesCurveEndColor = new Color(255 / 255f, 146 / 255f, 0);
		private const int MinWidth = 2;
		private const int MaxWidth = 8;
		private const float CubeRadius = 0.1f;
		private const float CircleRadius = 0.06f;
		private const float ScreenSpaceSize = 3f;
		private const float ExtraHandlesScale = 2f;
		
		void OnEnable()
		{
#if UNITY_2019_1_OR_NEWER
			SceneView.duringSceneGui += OnSceneView;
#else
			SceneView.onSceneGUIDelegate += OnSceneView;
#endif
		}

		void OnDisable()
		{
#if UNITY_2019_1_OR_NEWER
			SceneView.duringSceneGui -= OnSceneView;
#else
			SceneView.onSceneGUIDelegate -= OnSceneView;
#endif
		}
		
		public override bool RequiresConstantRepaint()
		{
			return true;
		}

		public override void OnInspectorGUI()
{
    serializedObject.Update();
    expandArray = EditorGUILayout.Foldout(expandArray, new GUIContent("Geometry"));
    if (expandArray)
    {
        var list = serializedObject.FindProperty("Objects");
        for (var i = 0; i < list.arraySize; i++)
        {
            var item = list.GetArrayElementAtIndex(i);
            GUI.SetNextControlName(i.ToString());
            EditorGUILayout.ObjectField(item);
        }
    }

    if (!shouldFocusOnControl)
    {
        var focusedControlName = GUI.GetNameOfFocusedControl();
        if (!string.IsNullOrEmpty(focusedControlName))
        {
            int.TryParse(focusedControlName, out focusedControlID);
        }
        else
        {
            focusedControlID = -1;
        }
    }
    else
    {
        shouldFocusOnControl = false;
        GUI.FocusControl(focusedControlID.ToString());
    }

    var container = (GeometryContainer)target;
    if (container != null)
    {
        var specialKey = (Event.current.alt || Event.current.shift) && focusedControlID != -1;
        GUILayout.BeginHorizontal();
        Geometry geometry = null;
        if (GUILayout.Button("Connect New Line", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            geometry = AddGameObjectWithGeometry<Line>(container.transform, true);
        }
        if (GUILayout.Button("Connect New Curve", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            geometry = AddGameObjectWithGeometry<Curve>(container.transform, true);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add New Line", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            geometry = AddGameObjectWithGeometry<Line>(container.transform, false);
        }

        if (GUILayout.Button("Add New Curve", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            geometry = AddGameObjectWithGeometry<Curve>(container.transform, false);
        }
        if (GUILayout.Button("Add New Dot", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            geometry = AddGameObjectWithGeometry<Dot>(container.transform, false);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(focusedControlID == -1 || container.Objects.Count == 0);
        if (GUILayout.Button("Clone Item", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            geometry = Instantiate(container.Objects[focusedControlID]);
            geometry.transform.parent = container.transform;
            specialKey = true;
        }
        if (GUILayout.Button("Remove Item", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            var item = container.Objects.ElementAt(focusedControlID);
            container.Objects.RemoveAt(focusedControlID);
            if (item != null)
            {
                DestroyImmediate(item.gameObject);
            }
            shouldFocusOnControl = true;
            focusedControlID--;
        }
        EditorGUI.EndDisabledGroup();
        GUILayout.EndHorizontal();

        // New Button to Remove All Items
        if (GUILayout.Button("Remove All Items", GUILayout.ExpandWidth(true)))
        {
            EditorUtility.SetDirty(target);
            foreach (var item in container.Objects)
            {
                if (item != null)
                {
                    DestroyImmediate(item.gameObject);
                }
            }
            container.Objects.Clear();
        }

        if (geometry != null)
        {
            if (specialKey)
            {
                container.Objects.Insert(focusedControlID + 1, geometry);
                focusedControlID++;
            }
            else
            {
                container.Objects.Add(geometry);
                focusedControlID = container.Objects.Count - 1;
            }
            shouldFocusOnControl = true;
        }

        Undo.RecordObject(target, "Change Target");
    }
    serializedObject.ApplyModifiedProperties();
}


		private static T AddGameObjectWithGeometry<T>(Transform container, bool shouldContinue) where T : Geometry
		{
			string gameObjectName;
			if (typeof(T) == typeof(Line))
			{
				gameObjectName = "Line";
			}
			else if (typeof(T) == typeof(Curve))
			{
				gameObjectName = "Curve";
			}
			else
			{
				gameObjectName = "Dot";
			}
			var gameObject = new GameObject(gameObjectName);
			gameObject.transform.parent = container;
			gameObject.transform.position = Vector3.zero;
			T geometry = gameObject.AddComponent<T>();
			geometry.ShouldContinue = shouldContinue;
			return geometry;
		}

		private void OnSceneView(SceneView sv)
		{
			var geometryContainer = target as GeometryContainer;
			if (geometryContainer == null || geometryContainer.Objects.Count <= 0) return;
			var rotation = geometryContainer.transform.rotation;
			var scale = MathHelper.GetNonZeroVector(geometryContainer.transform.localScale);
			var scaleInversed = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
			var position = geometryContainer.transform.position;
			var positionInversed = Vector3.Scale(scaleInversed, position);
			var segmentsData = new List<GeometryData>();
			var specialKey = (Event.current.alt || Event.current.shift || Event.current.control) && focusedControlID != -1;
			var previousColor = Handles.color;
			Handles.color = HandlesColor;
			Geometry previousGeometry = null;
			for (var i = 0; i < geometryContainer.Objects.Count; i++)
			{
				var connector = geometryContainer.Objects[i];
				if (connector == null)
					continue;

				var width = focusedControlID == i ? MaxWidth : MinWidth;
				if (previousGeometry != null && connector.ShouldContinue)
				{
					connector.Start = Vector3.Scale(scaleInversed, Vector3.Scale(previousGeometry.End, scale));
					if (focusedControlID == i && !specialKey)
					{
						previousGeometry.End = DrawHandle(connector.Start, scale, scaleInversed, position, positionInversed);
					}
				}
				else if (focusedControlID == i && !specialKey)
				{
					connector.Start = DrawHandle(connector.Start, scale, scaleInversed, position, positionInversed);
				}

				if (connector.CurveType == Geometry.Type.Curve)
				{
					var curve = connector as Curve;
					if (focusedControlID == i)
					{
						if (!specialKey)
						{
							curve.End = DrawHandle(curve.End, scale, scaleInversed, position, positionInversed);
							curve.StartTangent = DrawHandle(curve.StartTangent, scale, scaleInversed, position, positionInversed);
							curve.EndTangent = DrawHandle(curve.EndTangent, scale, scaleInversed, position, positionInversed);
						}
						else
						{
							var center = (curve.Start + curve.End + curve.StartTangent + curve.EndTangent) / 4f;
							var dragPosition = DrawHandle(center, scale, scaleInversed, position, positionInversed);
							var moveOffset = dragPosition - center;
							curve.Start += moveOffset;
							curve.StartTangent += moveOffset;
							curve.EndTangent += moveOffset;
							curve.End += moveOffset;
							if (previousGeometry != null && curve.ShouldContinue)
							{
								previousGeometry.End = curve.Start;
							}
						}
					}
					var startTransformed = MathHelper.TransformPoint(curve.Start, rotation, scale, position);
					var endTransformed = MathHelper.TransformPoint(curve.End, rotation, scale, position);
					var startTangentTransformed = MathHelper.TransformPoint(curve.StartTangent, rotation, scale, position);
					var endTangentTransformed = MathHelper.TransformPoint(curve.EndTangent, rotation, scale, position);
					var linesCount = MathHelper.GetCurveSegmentsCount(curve);
					var points = new Vector3[linesCount + 1];
					for (var j = 0; j <= linesCount; j++)
					{
						var t = j / (float)linesCount;
						var point = MathHelper.CalculateCubicBezierPoint(t, startTransformed, startTangentTransformed, endTangentTransformed, endTransformed);
						points[j] = point;
					}
					segmentsData.Add(new GeometryData {Object = curve, Points = points});
					Handles.DrawAAPolyLine(width, points);
					Handles.color = Color.white;
					Handles.DrawDottedLine(startTransformed, startTangentTransformed, ScreenSpaceSize);
					Handles.DrawDottedLine(endTransformed, endTangentTransformed, ScreenSpaceSize);

					var scaleValue = HandleUtility.GetHandleSize(startTransformed) * ExtraHandlesScale;
					Handles.color = HandlesCurveStartColor;
					Handles.DrawWireCube(startTransformed, Vector3.one * CubeRadius * scaleValue);
					Handles.DrawWireDisc(startTangentTransformed, Vector3.forward, CircleRadius * scaleValue);
					Handles.color = HandlesCurveEndColor;
					Handles.DrawWireCube(endTransformed, Vector3.one * CubeRadius * scaleValue);
					Handles.DrawWireDisc(endTangentTransformed, Vector3.forward, CircleRadius * scaleValue);
				}
				else if (connector.CurveType == Geometry.Type.Line)
				{
					var line = connector as Line;
					if (focusedControlID == i)
					{
						if (!specialKey)
						{
							line.End = DrawHandle(line.End, scale, scaleInversed, position, positionInversed);
						}
						else
						{
							var center = (line.Start + line.End) / 2f;
							var dragPosition = DrawHandle(center, scale, scaleInversed, position, positionInversed);
							var moveOffset = dragPosition - center;
							line.Start += moveOffset;
							line.End += moveOffset;
							if (previousGeometry != null && line.ShouldContinue)
							{
								previousGeometry.End = line.Start;
							}
						}
					}
					var startTransformed = MathHelper.TransformPoint(line.Start, rotation, scale, position);
					var endTransformed = MathHelper.TransformPoint(line.End, rotation, scale, position);
					var points = new[] {startTransformed, endTransformed};
					segmentsData.Add(new GeometryData {Object = line, Points = points});
					Handles.DrawAAPolyLine(width, points);
				}
				else
				{
					var dot = connector as Dot;
					if (focusedControlID == i)
					{
						if (specialKey)
						{
							var center = (dot.Start + dot.End) / 2f;
							var dragPosition = DrawHandle(center, scale, scaleInversed, position, positionInversed);
							var moveOffset = dragPosition - center;
							dot.Start += moveOffset;
							dot.End += moveOffset;
							if (previousGeometry != null && dot.ShouldContinue)
							{
								previousGeometry.End = dot.Start;
							}
						}
					}
					var startTransformed = MathHelper.TransformPoint(dot.Start, rotation, scale, position);
					var points = new[] {startTransformed, startTransformed};
					segmentsData.Add(new GeometryData {Object = dot, Points = points});
					var scaleValue = HandleUtility.GetHandleSize(startTransformed) * ExtraHandlesScale;
					Handles.DrawWireCube(startTransformed, Vector2.one * CubeRadius * scaleValue);
				}
				Undo.RecordObject(connector, "Connector Change");
				previousGeometry = connector;
			}
			geometryContainer.SegmentsData = segmentsData;
			Handles.color = previousColor;
		}
		
		private Vector3 DrawHandle(Vector3 point, Vector3 scale, Vector3 scaleInversed, Vector3 position, Vector3 positionInversed)
		{
			return Vector3.Scale(scaleInversed, Handles.PositionHandle(Vector3.Scale(point, scale) + position, Quaternion.identity)) - positionInversed;
		}
	}
}