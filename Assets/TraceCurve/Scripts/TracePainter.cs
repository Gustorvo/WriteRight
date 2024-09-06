using System.Collections.Generic;
using UnityEngine;

namespace TraceCurve
{
	public class TracePainter : MonoBehaviour
	{
		public Quality RenderTextureQuality = Quality.High;
		public RenderTexture RenderTexture;
		public Transform TraceObject;
		public Transform BrushObject;
		public Texture BrushTexture;
		public Vector2 BrushScale = Vector2.one;
		public Vector2 BrushExtraBounds = Vector2.zero;
		public Shader BrushShader;
		public Shader MaskShader;

		public enum Quality
		{
			Low = 4,
			Medium = 2,
			High = 1
		}

		[SerializeField] private bool canDraw = true;
		public bool CanDraw
		{
			get { return canDraw; }
			set
			{
				if (value == false)
				{
					isStartPosition = true;
					drawEndPosition = null;
				}
				canDraw = value;
			}
		}
		
		[SerializeField] private bool canFillByPositions;
		public bool CanFillByPositions
		{
			get { return canFillByPositions; }
			set
			{
				if (value == false)
				{
					isStartPosition = true;
					drawEndPosition = null;
				}
				canFillByPositions = value;
			}
		}
				
		[SerializeField] private bool setBrushAngleFollowPath;
		public bool SetBrushAngleFollowPath
		{
			get { return setBrushAngleFollowPath; }
			set { setBrushAngleFollowPath = value; }
		}
		
		[SerializeField] private float brushAngleOffset;
		public float BrushAngleOffset
		{
			get { return brushAngleOffset; }
			set { brushAngleOffset = value; }
		}

		private RectTransform brushRectTransform;
		public RectTransform BrushRectTransform
		{
			get { return brushRectTransform; }
		}

		private Vector2 brushBoundsSize;
		public Vector2 BrushBoundsSize
		{
			get { return brushBoundsSize; }
		}
		
		private bool isCanvasOverlay;
		public bool IsCanvasOverlay
		{
			get { return isCanvasOverlay; }
		}

		public bool UpdateOnce;

		private TraceBrush traceBrush;
		private TraceBrushRenderer traceBrushRenderer;
		private Renderer traceRenderer;
		private RectTransform rectTransform;
		private Texture previousBrushTexture;
		private List<Vector2[]> renderPositionsQueue;
		private Vector2 boundsSize;
		private Vector2 halfBoundsSize;
		private Vector3 prevBrushObjectPosition;
		private Vector2 previousBrushScale;
		private Vector2 imageSize;
		private Vector2 drawStartPosition;
		private Vector2? drawEndPosition;
		private Vector2 drawPosition;
		private bool shouldUpdate;
		private bool isStartPosition = true;

		void Awake()
		{
			renderPositionsQueue = new List<Vector2[]>();
			traceBrush = new TraceBrush();
			traceBrushRenderer = new TraceBrushRenderer();
			prevBrushObjectPosition = BrushObject.position;
			traceBrush.Init(TraceObject, BrushShader, MaskShader);
			traceBrush.SetBrushTexture(BrushTexture);
			previousBrushTexture = BrushTexture;
			traceBrushRenderer.Init((int) RenderTextureQuality, traceBrush.BrushMaterial);
			traceBrushRenderer.SetBrushScale(BrushScale);
			previousBrushScale = BrushScale;
			traceBrushRenderer.OnUpdate = UpdatePositions;
		}

		void Start()
		{
			GetBounds();
			CreateRenderTexture();
		}

		void LateUpdate()
		{
			if (BrushScale != previousBrushScale)
			{
				traceBrushRenderer.SetBrushScale(BrushScale);
				previousBrushScale = BrushScale;
			}
			if (BrushTexture != previousBrushTexture)
			{
				traceBrush.SetBrushTexture(BrushTexture);
				previousBrushTexture = BrushTexture;
			}
			traceBrushRenderer.Update();
		}

		public void Fill()
		{
			traceBrushRenderer.Fill();
		}

		public void Clear()
		{
			traceBrushRenderer.Clear();
		}

		public void AddToRenderQueue(Vector2[] positions)
		{
			if (positions != null && positions.Length > 1)
			{
				for (var i = 0; i < positions.Length; i++)
				{
					positions[i] = GetDrawPosition(positions[i]);
				}
				renderPositionsQueue.Add(positions);
			}
		}

		private void UpdatePositions()
		{
			if (CanDraw)
			{
				shouldUpdate = prevBrushObjectPosition != BrushObject.position || UpdateOnce;
				if (shouldUpdate)
				{
					OnDraw(BrushObject.position);
				}
				prevBrushObjectPosition = BrushObject.position;
				if (renderPositionsQueue != null && renderPositionsQueue.Count > 0)
				{
					foreach (var renderPositions in renderPositionsQueue)
					{
						traceBrushRenderer.DrawLines(renderPositions);
					}
					renderPositionsQueue.Clear();
					if (drawEndPosition != null)
					{
						drawStartPosition = drawEndPosition.Value;
					}
				}
				else if (shouldUpdate && CanFillByPositions || UpdateOnce)
				{
					if (drawStartPosition == drawEndPosition)
					{
						traceBrushRenderer.DrawHole(drawPosition);
					}
					else
					{
						traceBrushRenderer.DrawLine(drawStartPosition, drawEndPosition.Value);
					}
					UpdateOnce = false;
				}
			}
		}

		private void CreateRenderTexture()
		{
			var renderTextureSize = new Vector2(imageSize.x / (float) RenderTextureQuality, imageSize.y / (float) RenderTextureQuality);
			RenderTexture = new RenderTexture((int) renderTextureSize.x, (int) renderTextureSize.y, 0, RenderTextureFormat.R8);
			RenderTexture.useMipMap = false;
			RenderTexture.autoGenerateMips = false;
			RenderTexture.Create();
			traceBrush.SetMaskTexture(RenderTexture);
			traceBrushRenderer.SetTexture(RenderTexture);
		}

		private void GetBounds()
		{
			traceRenderer = TraceObject.GetComponent<Renderer>();
			rectTransform = TraceObject.GetComponent<RectTransform>();
			if (traceRenderer != null)
			{
				imageSize = new Vector2(traceRenderer.sharedMaterial.mainTexture.width, traceRenderer.sharedMaterial.mainTexture.height);
				boundsSize = traceRenderer.bounds.size;
				halfBoundsSize = boundsSize / 2f;
			}
			else if (rectTransform != null)
			{
				imageSize = new Vector2(rectTransform.rect.width, rectTransform.rect.height);
				boundsSize = Vector2.Scale(rectTransform.rect.size, rectTransform.lossyScale);
				halfBoundsSize = boundsSize / 2f;
				var canvas = TraceObject.transform.GetComponentInParent<Canvas>();
				if (canvas != null)
				{
					isCanvasOverlay = canvas.renderMode == RenderMode.ScreenSpaceOverlay;
				}
			}
			else
			{
				Debug.LogError("Can't find Renderer or RectTransform Component!");
			}
			traceBrushRenderer.SetImage(imageSize);
			
			var brushRenderer = BrushObject.GetComponent<Renderer>();
			brushRectTransform = BrushObject.GetComponent<RectTransform>();
			if (brushRenderer != null)
			{
				brushBoundsSize = brushRenderer.bounds.size;
			}
			else if (brushRectTransform != null)
			{
				brushBoundsSize = Vector2.Scale(brushRectTransform.rect.size, brushRectTransform.lossyScale);
			}
			else
			{
				var pixelsPerInch = new Vector2(imageSize.x  / boundsSize.x / BrushObject.lossyScale.x, imageSize.y / boundsSize.y / BrushObject.lossyScale.y);
				brushBoundsSize = new Vector2(BrushTexture.width / pixelsPerInch.x, BrushTexture.height / pixelsPerInch.y);
				Debug.LogWarning("Can't find Renderer or RectTransform Component!");
			}
		}
		
		private void OnDraw(Vector2 position)
		{
			drawPosition = GetDrawPosition(position);
			if (isStartPosition)
			{
				if (drawEndPosition == null)
				{
					drawEndPosition = drawPosition;
				}
				else
				{
					drawEndPosition = drawStartPosition;
				}
				drawStartPosition = drawPosition;
			}
			else
			{
				drawEndPosition = drawPosition;
			}
			isStartPosition = !isStartPosition;
		}

		private Vector2 GetDrawPosition(Vector2 position)
		{
			var clickLocalPosition = Vector2.Scale(TraceObject.InverseTransformPoint(position), TraceObject.lossyScale) + halfBoundsSize;
			var pixelsPerInch = new Vector2(
				imageSize.x / boundsSize.x / TraceObject.lossyScale.x, 
				imageSize.y / boundsSize.y / TraceObject.lossyScale.y);
			return Vector2.Scale(Vector2.Scale(clickLocalPosition, TraceObject.lossyScale), pixelsPerInch);
		}
	}
}