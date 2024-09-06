using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace TraceCurve
{
	public class TraceInput : MonoBehaviour
	{
		#region Variables

		public enum Move
		{
			Forward,
			Backward
		}
		
		public Camera Camera;
		public TracePainter TracePainter;
		public GeometryContainer GeometryContainer;
		public TraceBrushMoverBase BrushMover;
		public Move MoveDirection = Move.Forward;
		public float ConnectRadius = 0.1f;
		public float BreakRange = -1;
		public bool UpdateOnDown;

		private List<Vector2> drawPositions = new List<Vector2>();
		private Vector3 mousePosition;
		private Vector2 clickPosition;
		private Vector2 previousMousePosition = -Vector2.one;
		private Vector2 dragOffset = Vector2.zero;
		private Move previousMoveForward;
		private GeometryRange[] geometryRanges;
		private bool rangesInited;
		private bool finished;
		private bool dragStarted;
		private int geometryIndex;
		private int pointIndex;
		private int fingerId = -1;
#if UNITY_WEBGL
		private bool isWebgl = true;
#else
		private bool isWebgl = false;
#endif
		private const float Eps = 0.00001f;
		private const int MaxIterationsCount = 256;
		private const bool UpdateOnePositionPerFrame = false;

		#endregion

		#region Events

		public event Handler OnBrake;
		public event GeometryHandler OnGeometryStart;
		public event GeometryHandler OnGeometryFinish;
		public event GeometryHandler OnGeometryCancel;
		public event GeometryHandler OnAllGeometryFinish;
		public event ProgressHandler OnProgress;

		public delegate void Handler();
		public delegate void ProgressHandler(float progress);
		public delegate void GeometryHandler(Geometry geometry);

		#endregion

		#region Properties

		private Geometry Geometry
		{
			get { return GeometryContainer.SegmentsData[geometryIndex].Object; }
		}

		private Vector3[] Points
		{
			get { return GeometryContainer.SegmentsData[geometryIndex].Points; }
		}

		private Vector3 PreviousPoint
		{
			get
			{
				if (pointIndex > 0)
				{
					return Points[pointIndex - 1];
				}
				if (geometryIndex > 0)
				{
					var points = GeometryContainer.SegmentsData[geometryIndex - 1].Points;
					return points[points.Length - 2];
				}
				return GeometryContainer.SegmentsData[0].Points[0];
			}
		}

		private Vector3 CurrentPoint
		{
			get { return Points[pointIndex]; }
		}

		private Vector3 NextPoint
		{
			get
			{
				if (pointIndex < Points.Length - 1)
				{
					return Points[pointIndex + 1];
				}
				if (geometryIndex < LastGeometryIndex)
				{
					return GeometryContainer.SegmentsData[geometryIndex + 1].Points[1];
				}
				var points = GeometryContainer.SegmentsData[LastGeometryIndex].Points;
				return points[points.Length - 1];
			}
		}

		private int LastGeometryIndex
		{
			get { return GeometryContainer.SegmentsData.Count - 1; }
		}

		private int LastPointIndex
		{
			get { return GeometryContainer.SegmentsData[geometryIndex].Points.Length - 1; }
		}

		private bool IsFigureFinished
		{
			get { return LastPointIndex <= pointIndex; }
		}

		private bool IsAllFiguresFinished
		{
			get { return IsFigureFinished && GeometryContainer.SegmentsData.Count <= geometryIndex + 1; }
		}

		#endregion

		#region Unity

		void Start()
		{
			if (Camera == null)
			{
				Camera = Camera.main;
				if (!Camera.orthographic)
				{
					Debug.LogWarning("Camera should be orthographic! Fixing it...");
					Camera.orthographic = true;
				}
			}
			if (BrushMover != null)
			{
				BrushMover.Init(TracePainter.BrushObject.transform);
				UpdateBrushMoverAngle();
				BrushMover.OnMoveStarted = () =>
				{
					UpdateAngle();
					UpdateBrushMoverAngle();
					TracePainter.CanDraw = false;
				};
				BrushMover.OnMoveFinished = () =>
				{
					UpdateAngle();
					TracePainter.CanDraw = true;
				};
			}
			SetBrushPosition();
#if ENABLE_INPUT_SYSTEM
			if (!EnhancedTouchSupport.enabled)
			{
				EnhancedTouchSupport.Enable();
			}
#endif
		}

		void Update()
		{
			if (BrushMover != null && BrushMover.IsBusy)
			{
				BrushMover.Move();
				return;
			}
			
			if (previousMoveForward != MoveDirection)
			{
				if (finished)
				{
					finished = !finished;
				}
				previousMoveForward = MoveDirection;
			}

			UpdateInput();
		}

		#endregion

		#region UserMethods

		public void SetBrushPosition()
		{
			if (GeometryContainer != null && GeometryContainer.SegmentsData.Count > 0)
			{
				TracePainter.BrushObject.transform.position = new Vector3(CurrentPoint.x, CurrentPoint.y, TracePainter.BrushObject.transform.position.z);
				UpdateAngle();
			}
		}

		public void SetProgress(int geometry, int point)
		{
			geometryIndex = geometry;
			pointIndex = point;
			finished = geometryIndex == LastGeometryIndex && LastPointIndex == pointIndex;
		}

		public void InitRanges()
		{
			rangesInited = true;
			geometryRanges = new GeometryRange[GeometryContainer.SegmentsData.Count];
			for (var i = 0; i < GeometryContainer.SegmentsData.Count; i++)
			{
				var segments = GeometryContainer.SegmentsData[i];
				var lineLength = 0f;
				for (var j = 0; j < segments.Points.Length - 1; j++)
				{
					var p0 = segments.Points[j + 0];
					var p1 = segments.Points[j + 1];
					var distance = Vector2.Distance(p0, p1);
					lineLength += distance;
				}
				var previousRange = i > 0 ? geometryRanges[i - 1].End : 0f;
				var range = new GeometryRange
				{
					Geometry = segments.Object,
					Start = previousRange,
					End = previousRange + lineLength
				};
				geometryRanges[i] = range;
			}
		}

		#endregion

		#region Handles

		private void HandleOnBreak()
		{
			if (OnBrake != null)
			{
				OnBrake();
			}
		}

		private void HandleOnGeometryStart()
		{
			if (OnGeometryStart != null)
			{
				OnGeometryStart(Geometry);
			}
		}

		private void HandleOnGeometryFinish()
		{
			if (OnGeometryFinish != null)
			{
				OnGeometryFinish(Geometry);
			}
		}

		private void HandleOnGeometryCancel()
		{
			if (OnGeometryCancel != null)
			{
				OnGeometryCancel(Geometry);
			}
		}

		private void HandleOnAllGeometryFinish()
		{
			if (OnAllGeometryFinish != null)
			{
				OnAllGeometryFinish(Geometry);
			}
		}
		
		private void HandleOnProgress()
		{
			if (OnProgress != null)
			{
				OnProgress(GetProgress());
			}
		}
		
		private float GetProgress()
		{
			if (!rangesInited)
			{
				InitRanges();
			}
			var progress = geometryRanges[geometryIndex].Start;
			for (var i = 0; i <= pointIndex - 1; i++)
			{
				var p0 = GeometryContainer.SegmentsData[geometryIndex].Points[i];
				var p1 = GeometryContainer.SegmentsData[geometryIndex].Points[i + 1];
				var distance = Vector2.Distance(p0, p1);
				progress += distance;
			}
			
			progress += Vector2.Distance(GeometryContainer.SegmentsData[geometryIndex].Points[pointIndex],
				TracePainter.BrushObject.transform.position);

			var totalProgress = geometryRanges[geometryRanges.Length - 1].End - Eps;
			progress /= totalProgress;
			progress = Mathf.Clamp(progress, 0f, 1f);
			return progress;
		}

		#endregion

		#region Input

		private void UpdateInput()
		{
#if ENABLE_INPUT_SYSTEM
				if (Touchscreen.current != null && Touch.activeTouches.Count > 0 && !isWebgl)
				{
					foreach (var touch in Touch.activeTouches)
					{
						clickPosition = TracePainter.IsCanvasOverlay ? touch.screenPosition : (Vector2)Camera.ScreenToWorldPoint(touch.screenPosition);
						if (touch.phase == TouchPhase.Began && fingerId == -1)
						{
							fingerId = touch.finger.index;
							OnDown();
						}
					
						if (touch.finger.index == fingerId)
						{
							if (dragStarted && touch.phase == TouchPhase.Moved)
							{
								OnPress();
							}

							if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
							{
								fingerId = -1;
								OnUp();
							}
						}
					}
				}
#elif ENABLE_LEGACY_INPUT_MANAGER
			if (Input.touchSupported && Input.touchCount > 0 && !isWebgl)
			{
				#region Input Touch

				foreach (var touch in Input.touches)
				{
					clickPosition = TracePainter.IsCanvasOverlay ? touch.position : (Vector2)Camera.ScreenToWorldPoint(touch.position);
					if (touch.phase == TouchPhase.Began && fingerId == -1)
					{
						fingerId = touch.fingerId;
						OnDown();
					}
					
					if (touch.fingerId == fingerId)
					{
						if (dragStarted && touch.phase == TouchPhase.Moved)
						{
							OnPress();
						}

						if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
						{
							fingerId = -1;
							OnUp();
						}
					}
				}
				
				#endregion
			}
#endif
#if ENABLE_INPUT_SYSTEM
				if (Mouse.current != null)
				{
					var mousePosition = Mouse.current.position.ReadValue();
					clickPosition = TracePainter.IsCanvasOverlay ? mousePosition : (Vector2)Camera.ScreenToWorldPoint(mousePosition);
					if (Mouse.current.leftButton.wasPressedThisFrame)
					{
						OnDown();
					}

					if (Mouse.current.leftButton.isPressed && (Vector2)mousePosition != previousMousePosition)
					{
						if (dragStarted)
						{
							OnPress();
						}
					}
				
					if (Mouse.current.leftButton.wasReleasedThisFrame)
					{
						OnUp();
					}
#elif ENABLE_LEGACY_INPUT_MANAGER
				{
					#region Input Mouse

					mousePosition = Input.mousePosition;
					clickPosition = TracePainter.IsCanvasOverlay ? mousePosition : Camera.ScreenToWorldPoint(mousePosition);
					if (Input.GetMouseButtonDown(0))
					{
						OnDown();
					}

					if (Input.GetMouseButton(0) && (Vector2)mousePosition != previousMousePosition)
					{
						if (dragStarted)
						{
							OnPress();
						}
					}
				
					if (Input.GetMouseButtonUp(0))
					{
						OnUp();
					}
				
					#endregion
#endif
				}
		}
		
		private void OnDown()
		{
			if (TracePainter.BrushRectTransform != null)
			{
				var rect = new Rect(
					(Vector2)TracePainter.BrushRectTransform.position - TracePainter.BrushBoundsSize / 2f - TracePainter.BrushExtraBounds / 2f, 
					TracePainter.BrushBoundsSize + TracePainter.BrushExtraBounds);
				if (rect.Contains(clickPosition))
				{
					OnDownProcess(clickPosition);
				}
			}
			else
			{
				var bounds = new Bounds(
					(Vector2)TracePainter.BrushObject.transform.position, 
					TracePainter.BrushBoundsSize + TracePainter.BrushExtraBounds);
				if (bounds.Contains(clickPosition))
				{
					OnDownProcess(clickPosition);
				}
			}
		}

		private void OnDownProcess(Vector2 clickWorldPosition)
		{
			if (!finished)
			{
				HandleOnGeometryStart();
			}
			dragStarted = true;
			dragOffset = clickWorldPosition - (Vector2)TracePainter.BrushObject.transform.position;

			if (UpdateOnDown)
			{
				previousMousePosition = mousePosition;
				TracePainter.AddToRenderQueue(new []{ clickPosition, clickPosition });
				TracePainter.UpdateOnce = true;
			}

			if (GeometryContainer.SegmentsData[geometryIndex].Object.CurveType == Geometry.Type.Dot)
			{
				SetDot();
			}
		}

		private void SetDot()
		{
			var newPosition = TracePainter.BrushObject.transform.position;
			if (!IsAllFiguresFinished && MoveDirection == Move.Forward)
			{
				TryMoveForward(mousePosition, ref newPosition);
				UpdateOnce(newPosition);
			}
			if (MoveDirection == Move.Backward)
			{
				TryMoveBack(mousePosition, ref newPosition);
				UpdateOnce(newPosition);
			}
		}

		private void UpdateOnce(Vector2 newPosition)
		{
			var brushObjectTransform = TracePainter.BrushObject.transform;
			brushObjectTransform.position = new Vector3(newPosition.x, newPosition.y, brushObjectTransform.position.z);
			HandleOnProgress();
			drawPositions.Add(newPosition);
			TracePainter.AddToRenderQueue(drawPositions.ToArray());
			drawPositions.Clear();
			OnUp();
			TracePainter.UpdateOnce = true;
		}
		
		private void UpdateAngle()
		{
			if (TracePainter.SetBrushAngleFollowPath && Vector3.Distance(CurrentPoint, NextPoint) > 0.0001f)
			{
				var angle = Mathf.Atan2(CurrentPoint.y - NextPoint.y, CurrentPoint.x - NextPoint.x) * Mathf.Rad2Deg + TracePainter.BrushAngleOffset;
				var localEulerAngles = TracePainter.BrushObject.transform.localEulerAngles;
				TracePainter.BrushObject.transform.localRotation = Quaternion.Euler(localEulerAngles.x, localEulerAngles.y, angle);
			}
		}

		private void UpdateBrushMoverAngle()
		{
			if (TracePainter.SetBrushAngleFollowPath)
			{
				var angle = Mathf.Atan2(CurrentPoint.y - NextPoint.y, CurrentPoint.x - NextPoint.x) * Mathf.Rad2Deg + TracePainter.BrushAngleOffset;
				BrushMover.EnableFollowingPath(angle);
			}
			else
			{
				BrushMover.DisableFollowingPath();
			}
		}

		private void OnPress()
		{
			if (GeometryContainer.SegmentsData.Count == 0)
			{
				Debug.LogWarning("Can't find segments data!");
				return;
			}
			
			var newPosition = TracePainter.BrushObject.transform.position;
			if (BreakRange != -1 && Vector2.Distance(clickPosition, newPosition) > BreakRange)
			{
				HandleOnBreak();
				dragStarted = false;
				return;
			}
			
			drawPositions.Add(newPosition);
			var count = 0;
			while (count < MaxIterationsCount && CheckTracePath(clickPosition - dragOffset, ref newPosition) && !UpdateOnePositionPerFrame)
			{
				count++;
			}

			if (BrushMover == null || !BrushMover.IsBusy)
			{
				if (dragStarted && newPosition != TracePainter.BrushObject.transform.position)
				{
					drawPositions.Add(newPosition);
				}

				TracePainter.BrushObject.transform.position = new Vector3(newPosition.x, newPosition.y, TracePainter.BrushObject.transform.position.z);
				UpdateAngle();
				HandleOnProgress();
			}
			TracePainter.AddToRenderQueue(drawPositions.ToArray());
			drawPositions.Clear();
			previousMousePosition = mousePosition;
		}

		private void OnUp()
		{
			if (!finished)
			{
				HandleOnGeometryCancel();
			}
			previousMousePosition = -Vector2.one;
			dragOffset = Vector2.zero;
			dragStarted = false;
		}

		#endregion

		#region Moving

		private bool CheckTracePath(Vector2 position, ref Vector3 newPosition)
		{
			var movingPosition = new Vector3(position.x, position.y, newPosition.z);
			var shouldReturn = false;
			
			if (MoveDirection == Move.Forward)
			{
				var normals = MathHelper.GetPerpendiculars(CurrentPoint, NextPoint);
				shouldReturn = !MathHelper.HalfSpace(normals[0], normals[1], movingPosition);
			}
			else if (MoveDirection == Move.Backward)
			{
				var normals = MathHelper.GetPerpendiculars(CurrentPoint, PreviousPoint);
				shouldReturn = MathHelper.HalfSpace(normals[0], normals[1], movingPosition);
			}
			
			if (!IsAllFiguresFinished && MoveDirection == Move.Forward && !shouldReturn)
			{
				return TryMoveForward(position, ref newPosition);
			}
			if (MoveDirection == Move.Backward && shouldReturn)
			{
				return TryMoveBack(position, ref newPosition);
			}
			return false;
		}

		private bool TryMoveForward(Vector3 movingPosition, ref Vector3 position)
		{
			Vector3 nearestPoint = PreviousPoint;
			if (MathHelper.NearestPointOnLine(CurrentPoint, CurrentPoint - NextPoint, movingPosition) < ConnectRadius)
			{
				nearestPoint = MathHelper.NearestPointOnFiniteLine(CurrentPoint, NextPoint, movingPosition);
				var distanceToNearest = Vector3.Distance(nearestPoint, NextPoint);
				var distanceToCurrent = Vector3.Distance(position, NextPoint);
				if (distanceToNearest < distanceToCurrent)
				{
					position = nearestPoint;
				}
			}

			var distance = Vector2.Distance(nearestPoint, NextPoint);
			if (distance < ConnectRadius)
			{
				pointIndex++;
				if (IsFigureFinished)
				{
					if (IsAllFiguresFinished)
					{
						position = NextPoint;
						drawPositions.Add(NextPoint);
						finished = true;
						dragStarted = false;
						HandleOnGeometryFinish();
						HandleOnAllGeometryFinish();
						return false;
					}

					HandleOnGeometryFinish();
					geometryIndex++;
					pointIndex = 0;
					dragStarted = Geometry.ShouldContinue;
					if (!dragStarted)
					{
						if (BrushMover != null)
						{
							var from = GeometryContainer.SegmentsData[geometryIndex - 1]
								.Points[GeometryContainer.SegmentsData[geometryIndex - 1].Points.Length - 1];
							var fromPosition = new Vector3(from.x, from.y, TracePainter.BrushObject.transform.position.z);
							var to = GeometryContainer.SegmentsData[geometryIndex].Points[0];
							var toPosition = new Vector3(to.x, to.y, TracePainter.BrushObject.transform.position.z);
							BrushMover.StartMove(
								GeometryContainer.SegmentsData[geometryIndex - 1], fromPosition,
								GeometryContainer.SegmentsData[geometryIndex], toPosition);
							fingerId = -1;
						}
						else
						{
							position = CurrentPoint;
						}
						var previousCurvePoints = GeometryContainer.SegmentsData[geometryIndex - 1].Points;
						drawPositions.Add(previousCurvePoints[previousCurvePoints.Length - 1]);
						dragStarted = false;
						return false;
					}
				}

				position = CurrentPoint;
				drawPositions.Add(position);
				return true;
			}
			return false;
		}

		private bool TryMoveBack(Vector3 movingPosition, ref Vector3 position)
		{
			Vector3 nearestPoint = NextPoint;
			if (MathHelper.NearestPointOnLine(CurrentPoint, CurrentPoint - PreviousPoint, movingPosition) < ConnectRadius)
			{
				nearestPoint = MathHelper.NearestPointOnFiniteLine(CurrentPoint, PreviousPoint, movingPosition);
				position = nearestPoint;
			}

			var distance = Vector2.Distance(nearestPoint, PreviousPoint);
			if (distance < ConnectRadius)
			{
				var shouldContinue = Geometry.ShouldContinue;
				pointIndex--;
				if (pointIndex <= 0)
				{
					if (!shouldContinue)
					{
						HandleOnGeometryCancel();
						dragStarted = false;
					}

					HandleOnGeometryFinish();
					geometryIndex--;
					if (geometryIndex < 0)
					{
						geometryIndex = 0;
						pointIndex = 0;
						finished = true;
						position = CurrentPoint;
						HandleOnAllGeometryFinish();
						return false;
					}

					pointIndex = LastPointIndex;
					if (!shouldContinue)
					{
						if (BrushMover != null)
						{
							var from = GeometryContainer.SegmentsData[geometryIndex + 1].Points[0];
							var fromPosition = new Vector3(from.x, from.y, TracePainter.BrushObject.transform.position.z);
							var to = GeometryContainer.SegmentsData[geometryIndex]
								.Points[GeometryContainer.SegmentsData[geometryIndex].Points.Length - 1];
							var toPosition = new Vector3(to.x, to.y, TracePainter.BrushObject.transform.position.z);
							BrushMover.StartMove(
								GeometryContainer.SegmentsData[geometryIndex + 1], fromPosition,
								GeometryContainer.SegmentsData[geometryIndex], toPosition);
							fingerId = -1;
						}
						else
						{
							position = CurrentPoint;
						}
						var previousCurvePoints = GeometryContainer.SegmentsData[geometryIndex + 1].Points;
						drawPositions.Add(previousCurvePoints[0]);
						HandleOnGeometryCancel();
						dragStarted = false;
						return false;
					}
				}

				position = CurrentPoint;
				if (!shouldContinue)
				{
					drawPositions.Add(position);
				}

				return dragStarted;
			}
			return false;
		}

		#endregion
	}
}