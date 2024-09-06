using System;
using UnityEngine;
using UnityEngine.UI;

namespace TraceCurve
{
	[Serializable]
	public class TraceBrush
	{
		public Material Brush;
		public Material Mask;

		private const string MaskTexProperty = "_MaskTex";

		public Material BrushMaterial
		{
			get { return Brush; }
		}

		public void Init(Component traceObject, Shader brushShader, Shader maskShader)
		{
			Brush = new Material(brushShader);
			Mask = new Material(maskShader);
			Renderer renderer = traceObject.GetComponent<Renderer>();
			Image image = traceObject.GetComponent<Image>();
			if (renderer != null)
			{
				SpriteRenderer spriteRenderer = traceObject.GetComponent<SpriteRenderer>();
				if (spriteRenderer != null)
				{
					Mask.mainTexture = spriteRenderer.sprite.texture;
				}
				else
				{
					Mask.mainTexture = renderer.sharedMaterial.mainTexture;
				}

				renderer.material = Mask;
			}
			else if (image != null)
			{
				image.material = Mask;
			}
			else
			{
				Debug.LogError("Can't find Render or Image Component on TraceObject!");
			}
		}

		public void SetBrushTexture(Texture texture)
		{
			Brush.mainTexture = texture;
		}

		public void SetMaskTexture(RenderTexture renderTexture)
		{
			Mask.SetTexture(MaskTexProperty, renderTexture);
		}
	}
}