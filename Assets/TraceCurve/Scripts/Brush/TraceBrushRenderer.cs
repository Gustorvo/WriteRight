using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TraceCurve
{
	public class TraceBrushRenderer
	{
		public Action OnUpdate;

		private Mesh mesh;
		private Mesh quadMesh;
		private Material brush;
		private CommandBuffer commandBuffer;
		private RenderTargetIdentifier rti;
		private Vector2 brushScale;
		private Vector2 imageSize;
		private bool isFirstFrame = true;
		private int lastFrameId;
		private int renderTextureQuality;

		public void Init(int renderTextureQuality, Material brush)
		{
			this.brush = brush;
			this.renderTextureQuality = renderTextureQuality;

			commandBuffer = new CommandBuffer();
			commandBuffer.name = "ObjectRenderer";

			quadMesh = new Mesh();
			quadMesh.vertices = new Vector3[4];
			quadMesh.uv = new[]
			{
				new Vector2(0, 1),
				new Vector2(1, 1),
				new Vector2(1, 0),
				new Vector2(0, 0),
			};
			quadMesh.triangles = new[]
			{
				0, 1, 2,
				2, 3, 0
			};
			quadMesh.colors = new[]
			{
				Color.white,
				Color.white,
				Color.white,
				Color.white
			};
		}

		public void SetBrushScale(Vector2 scale)
		{
			brushScale = scale;
		}

		public void Update()
		{
			if (lastFrameId == Time.frameCount)
			{
				return;
			}

			if (isFirstFrame)
			{
				commandBuffer.SetRenderTarget(rti);
				commandBuffer.ClearRenderTarget(false, true, Color.clear);
				Graphics.ExecuteCommandBuffer(commandBuffer);
				isFirstFrame = false;
			}

			lastFrameId = Time.frameCount;

			if (OnUpdate != null)
				OnUpdate();
		}

		public void SetImage(Vector2 image)
		{
			imageSize = image;
		}

		public void SetTexture(RenderTexture renderTexture)
		{
			rti = new RenderTargetIdentifier(renderTexture);
		}

		public void Fill()
		{
			commandBuffer.SetRenderTarget(rti);
			commandBuffer.ClearRenderTarget(false, true, Color.red);
			Graphics.ExecuteCommandBuffer(commandBuffer);
		}

		public void Clear()
		{
			isFirstFrame = true;
		}

		public void DrawHole(Vector2 drawPosition)
		{
			var positionRect = new Rect(
				(drawPosition.x - 0.5f * brush.mainTexture.width * brushScale.x) / imageSize.x,
				(drawPosition.y - 0.5f * brush.mainTexture.height * brushScale.y) / imageSize.y,
				brush.mainTexture.width * brushScale.x / imageSize.x,
				brush.mainTexture.height * brushScale.y / imageSize.y);

			quadMesh.vertices = new[]
			{
				new Vector3(positionRect.xMin, positionRect.yMax, 0),
				new Vector3(positionRect.xMax, positionRect.yMax, 0),
				new Vector3(positionRect.xMax, positionRect.yMin, 0),
				new Vector3(positionRect.xMin, positionRect.yMin, 0)
			};

			GL.LoadOrtho();
			commandBuffer.Clear();
			commandBuffer.SetRenderTarget(rti);
			commandBuffer.DrawMesh(quadMesh, Matrix4x4.identity, brush);
			Graphics.ExecuteCommandBuffer(commandBuffer);
		}

		public void DrawLine(Vector2 drawStartPosition, Vector2 drawEndPosition)
		{
			var holesCount = (int)(Vector2.Distance(drawStartPosition, drawEndPosition) / renderTextureQuality);
			holesCount = Mathf.Clamp(holesCount, 1, 16384);
			var positions = new Vector3[holesCount * 4];
			var uv = new Vector2[holesCount * 4];
			var colors = new Color[holesCount * 4];
			var indices = new int[holesCount * 6];
			for (var i = 0; i < holesCount; i++)
			{
				var holePosition = drawStartPosition + (drawEndPosition - drawStartPosition) / holesCount * i;
				var positionRect = new Rect(
					(holePosition.x - 0.5f * brush.mainTexture.width * brushScale.x) / imageSize.x,
					(holePosition.y - 0.5f * brush.mainTexture.height * brushScale.y) / imageSize.y,
					brush.mainTexture.width * brushScale.x / imageSize.x,
					brush.mainTexture.height * brushScale.y / imageSize.y);

				var index4 = i * 4;
				var index6 = i * 6;

				positions[index4 + 0] = new Vector3(positionRect.xMin, positionRect.yMax, 0);
				positions[index4 + 1] = new Vector3(positionRect.xMax, positionRect.yMax, 0);
				positions[index4 + 2] = new Vector3(positionRect.xMax, positionRect.yMin, 0);
				positions[index4 + 3] = new Vector3(positionRect.xMin, positionRect.yMin, 0);

				uv[index4 + 0] = Vector2.up;
				uv[index4 + 1] = Vector2.one;
				uv[index4 + 2] = Vector2.right;
				uv[index4 + 3] = Vector2.zero;

				colors[index4 + 0] = Color.white;
				colors[index4 + 1] = Color.white;
				colors[index4 + 2] = Color.white;
				colors[index4 + 3] = Color.white;

				indices[index6 + 0] = index4 + 0;
				indices[index6 + 1] = index4 + 1;
				indices[index6 + 2] = index4 + 2;
				indices[index6 + 3] = index4 + 2;
				indices[index6 + 4] = index4 + 3;
				indices[index6 + 5] = index4 + 0;
			}

			if (positions.Length > 0)
			{
				if (mesh != null)
				{
					mesh.Clear(false);
				}
				else
				{
					mesh = new Mesh();
				}

				mesh.vertices = positions;
				mesh.uv = uv;
				mesh.colors = colors;
				mesh.triangles = indices;
				GL.LoadOrtho();
				commandBuffer.Clear();
				commandBuffer.SetRenderTarget(rti);
				commandBuffer.DrawMesh(mesh, Matrix4x4.identity, brush);
				Graphics.ExecuteCommandBuffer(commandBuffer);
			}
		}

		public void DrawLines(Vector2[] lines)
		{
			var holesArray = new int[lines.Length - 1];
			var totalHolesCount = 0;
			for (var i = 0; i < lines.Length - 1; i++)
			{
				var drawStartPosition = lines[i];
				var drawEndPosition = lines[i + 1];
				holesArray[i] = (int)Mathf.Max(1, Vector2.Distance(drawStartPosition, drawEndPosition) / renderTextureQuality);
				totalHolesCount += holesArray[i];
			}
			totalHolesCount = Mathf.Clamp(totalHolesCount, 1, 16384);
			var positions = new Vector3[totalHolesCount * 4];
			var uv = new Vector2[totalHolesCount * 4];
			var colors = new Color[totalHolesCount * 4];
			var indices = new int[totalHolesCount * 6];
			var count = 0;
			for (var i = 0; i < lines.Length - 1; i++)
			{
				var drawStartPosition = lines[i];
				var drawEndPosition = lines[i + 1];
				var holes = holesArray[i];
				for (var j = 0; j < holes; j++)
				{
					var holePosition = drawStartPosition + (drawEndPosition - drawStartPosition) / holes * j;
					var positionRect = new Rect(
						(holePosition.x - 0.5f * brush.mainTexture.width * brushScale.x) / imageSize.x,
						(holePosition.y - 0.5f * brush.mainTexture.height * brushScale.y) / imageSize.y,
						brush.mainTexture.width * brushScale.x / imageSize.x,
						brush.mainTexture.height * brushScale.y / imageSize.y);

					var index4 = count * 4 + j * 4;
					var index6 = count * 6 + j * 6;

					positions[index4 + 0] = new Vector3(positionRect.xMin, positionRect.yMax, 0);
					positions[index4 + 1] = new Vector3(positionRect.xMax, positionRect.yMax, 0);
					positions[index4 + 2] = new Vector3(positionRect.xMax, positionRect.yMin, 0);
					positions[index4 + 3] = new Vector3(positionRect.xMin, positionRect.yMin, 0);

					uv[index4 + 0] = Vector2.up;
					uv[index4 + 1] = Vector2.one;
					uv[index4 + 2] = Vector2.right;
					uv[index4 + 3] = Vector2.zero;

					colors[index4 + 0] = Color.white;
					colors[index4 + 1] = Color.white;
					colors[index4 + 2] = Color.white;
					colors[index4 + 3] = Color.white;

					indices[index6 + 0] = index4 + 0;
					indices[index6 + 1] = index4 + 1;
					indices[index6 + 2] = index4 + 2;
					indices[index6 + 3] = index4 + 2;
					indices[index6 + 4] = index4 + 3;
					indices[index6 + 5] = index4 + 0;
				}

				count += holes;
			}

			if (positions.Length > 0)
			{
				if (mesh != null)
				{
					mesh.Clear(false);
				}
				else
				{
					mesh = new Mesh();
				}

				mesh.vertices = positions;
				mesh.uv = uv;
				mesh.colors = colors;
				mesh.triangles = indices;
				GL.LoadOrtho();
				commandBuffer.Clear();
				commandBuffer.SetRenderTarget(rti);
				commandBuffer.DrawMesh(mesh, Matrix4x4.identity, brush);
				Graphics.ExecuteCommandBuffer(commandBuffer);
			}
		}
	}
}