using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace _Project.Scripts
{
	public class TextureModifier
	{
		private NativeArray<Vector2Int> brushOffsets;
		private int textureWidth, textureHeight;
		public Texture2D texture;
		private NativeArray<Color32> pixels;
		private NativeArray<Color32> pixelsNonAllocated;
		private NativeList<int> groupStarts;
		private NativeParallelMultiHashMap<int, int> groupResults;
		private Color32[] modifiedPixels;
		public event Action<int> OnGroupErased;
		private int brushSize;
		private bool initialized;

		#region Initialize

		public TextureModifier(Texture2D inputTexture)
		{
			textureWidth = inputTexture.width;
			textureHeight = inputTexture.height;
			texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
			texture.LoadRawTextureData(inputTexture.GetRawTextureData());
			texture.Apply();
			if (pixels.IsCreated) pixels.Dispose();
			pixels = new NativeArray<Color32>(texture.GetPixels32(), Allocator.Persistent);
			pixelsNonAllocated = new NativeArray<Color32>(textureWidth * textureHeight, Allocator.Persistent);
			initialized = true;
		}

		public void Dispose()
		{
			if (pixelsNonAllocated.IsCreated) pixelsNonAllocated.Dispose();
			if (pixels.IsCreated) pixels.Dispose();
			if (brushOffsets.IsCreated) brushOffsets.Dispose();
			if (groupStarts.IsCreated) groupStarts.Dispose();
			if (groupResults.IsCreated) groupResults.Dispose();
			texture = null;
			OnGroupErased = null;
			initialized = false;
		}

		#endregion

		#region Debug

		public void SetAlphaForEachGroup(byte alphaValue)
		{
			if (!initialized) return;
			if (groupStarts.Length == 0)
			{
				Debug.LogWarning("No groups available for processing.");
				return;
			}

			foreach (var groupStart in groupStarts)
			{
				var groupEntries = groupResults.GetValuesForKey(groupStart);

				while (groupEntries.MoveNext())
				{
					int pixelIndex = groupEntries.Current;

					Color32 color = pixels[pixelIndex];
					color.a = alphaValue; // Set the alpha value
					var nativeArray = pixels;
					nativeArray[pixelIndex] = color;
				}
			}

			// Apply the modified pixels back to the texture
			texture.SetPixels32(pixels.ToArray());
			texture.Apply();

			Debug.Log($"Set alpha value of {alphaValue} for all pixels in each group.");
		}

		#endregion


		public int DivideTextureIntoGroups(int minPixelPerGroup = 100, Vector2 threshold = default)
		{
			if (!initialized) return -1;
			int width = texture.width;
			int height = texture.height;

			NativeArray<bool> visited = new NativeArray<bool>(pixels.Length, Allocator.TempJob);
			groupResults = new NativeParallelMultiHashMap<int, int>(pixels.Length, Allocator.Persistent);
			groupStarts = new NativeList<int>(Allocator.Persistent);

			// Run the job to mark all groups sequentially
			var markJob = new MarkPixelsSequentialJob
			{
				width = width,
				height = height,
				pixelArray = pixels,
				visited = visited,
				groupResults = groupResults,
				groupStarts = groupStarts,
				colorThreshold =  threshold
			};

			JobHandle markJobHandle = markJob.Schedule();
			markJobHandle.Complete();

			// Run the job to filter groups based on the minimum size
			var groupSizes = new NativeArray<int>(groupStarts.Length, Allocator.TempJob);

			var countGroupSizesJob = new CountGroupSizesJob
			{
				groupResults = groupResults,
				groupStarts = groupStarts,
				groupSizes = groupSizes
			};

			JobHandle countJobHandle = countGroupSizesJob.Schedule(groupStarts.Length, 64);
			countJobHandle.Complete();

			var filteredGroupResults =
				new NativeParallelMultiHashMap<int, int>(groupResults.Capacity, Allocator.Persistent);
			var validGroupStarts = new NativeList<int>(Allocator.Persistent);

			var filterGroupsJob = new FilterGroupsJob
			{
				groupResults = groupResults,
				groupSizes = groupSizes,
				minPixelPerGroup = minPixelPerGroup,
				filteredGroupResults = filteredGroupResults.AsParallelWriter(),
				validGroupStarts = validGroupStarts.AsParallelWriter()
			};

			JobHandle filterJobHandle = filterGroupsJob.Schedule(groupStarts.Length, 64);
			filterJobHandle.Complete();

			// Replace the old groupResults and groupStarts with the filtered results
			groupResults.Dispose();
			groupStarts.Dispose();
			groupResults = filteredGroupResults;
			groupStarts = validGroupStarts;

			// Cleanup
			visited.Dispose();
			groupSizes.Dispose();

			Debug.Log($"Total number of groups after filtering: {groupStarts.Length}");
			return groupStarts.Length;
		}

		public NativeArray<int> GetBrushPixelPositions(Vector2 uv, int brushSize)
		{
			if (!initialized) return default;
			int centerX = Mathf.FloorToInt(uv.x * textureWidth);
			int centerY = Mathf.FloorToInt(uv.y * textureHeight);

			centerX = Mathf.Clamp(centerX, 0, textureWidth - 1);
			centerY = Mathf.Clamp(centerY, 0, textureHeight - 1);

			if (!brushOffsets.IsCreated)
			{
				PrecomputeBrush(brushSize);
			}

			NativeArray<int> pixelPositions = new NativeArray<int>(brushOffsets.Length, Allocator.TempJob);

			for (int i = 0; i < brushOffsets.Length; i++)
			{
				Vector2Int offset = brushOffsets[i];

				int actualX = centerX + offset.x;
				int actualY = centerY + offset.y;

				actualY = math.clamp(actualY, 0, textureHeight - 1);
				actualX = math.clamp(actualX, 0, textureWidth - 1);

				int pixelIndex = actualY * textureWidth + actualX;

				pixelPositions[i] = pixelIndex;
			}

			return pixelPositions;
		}

		public void ModifyPixelsAtUVNonAlloc(Vector2 uv, int brushSize, Color32 color)
		{
			if (!initialized) return;
			pixelsNonAllocated = texture.GetRawTextureData<Color32>();

			int centerX = Mathf.FloorToInt(uv.x * textureWidth);
			int centerY = Mathf.FloorToInt(uv.y * textureHeight);

			centerX = Mathf.Clamp(centerX, 0, textureWidth - 1);
			centerY = Mathf.Clamp(centerY, 0, textureHeight - 1);

			if (!brushOffsets.IsCreated)
			{
				PrecomputeBrush(brushSize);
			}

			var job = new ModifyColorJob
			{
				TextureData = pixelsNonAllocated,
				Width = textureWidth,
				Height = textureHeight,
				CenterX = centerX,
				CenterY = centerY,
				BrushOffsets = brushOffsets,
				TargetColor = color
			};

			JobHandle jobHandle = job.Schedule(brushOffsets.Length, 64);
			jobHandle.Complete();
			pixels = pixelsNonAllocated;
			texture.Apply();
		}

		public void DecreaseAlphaAtUVNonAlloc(Vector2 uv, int brushSize)
		{
			if (!initialized) return;
			int centerX = Mathf.FloorToInt(uv.x * textureWidth);
			int centerY = Mathf.FloorToInt(uv.y * textureHeight);

			centerX = Mathf.Clamp(centerX, 0, textureWidth - 1);
			centerY = Mathf.Clamp(centerY, 0, textureHeight - 1);

			if (!brushOffsets.IsCreated || brushSize != this.brushSize)
			{
				PrecomputeBrush(brushSize);
				this.brushSize = brushSize;
			}

			pixelsNonAllocated = texture.GetRawTextureData<Color32>();

			var job = new DecreaseAlphaJob
			{
				TextureData = pixelsNonAllocated,
				Width = textureWidth,
				Height = textureHeight,
				CenterX = centerX,
				CenterY = centerY,
				BrushOffsets = brushOffsets,
				MaxDistance = brushSize
			};

			JobHandle jobHandle = job.Schedule(brushOffsets.Length, 64);
			jobHandle.Complete();
			texture.Apply();
			pixels = pixelsNonAllocated;
		}


		public void ModifyPixelsAtUV(Vector2 uv, int brushSize, Color32 color)
		{
			if (!initialized) return;
			int centerX = Mathf.FloorToInt(uv.x * textureWidth);
			int centerY = Mathf.FloorToInt(uv.y * textureHeight);

			centerX = Mathf.Clamp(centerX, 0, textureWidth - 1);
			centerY = Mathf.Clamp(centerY, 0, textureHeight - 1);

			if (!brushOffsets.IsCreated)
			{
				PrecomputeBrush(brushSize);
			}

			var job = new ModifyColorJob
			{
				TextureData = pixels,
				Width = textureWidth,
				Height = textureHeight,
				CenterX = centerX,
				CenterY = centerY,
				BrushOffsets = brushOffsets,
				TargetColor = color
			};

			JobHandle jobHandle = job.Schedule(brushOffsets.Length, 64);
			jobHandle.Complete();

			// Apply only the modified pixels to the texture
			texture.SetPixels32(centerX - brushSize, centerY - brushSize, 2 * brushSize + 1, 2 * brushSize + 1,
				ExtractModifiedPixels(centerX, centerY, brushSize));

			texture.Apply();
		}

		[BurstCompile]
		private struct ModifyColorJob : IJobParallelFor
		{
			[NativeDisableParallelForRestriction] public NativeArray<Color32> TextureData;
			public NativeArray<Vector2Int> BrushOffsets;
			public Color32 TargetColor;

			public int Width;
			public int Height;
			public int CenterX;
			public int CenterY;

			public void Execute(int index)
			{
				Vector2Int offset = BrushOffsets[index];

				int actualX = CenterX + offset.x;
				int actualY = CenterY + offset.y;

				actualY = math.clamp(actualY, 0, Height - 1);
				actualX = math.clamp(actualX, 0, Width - 1);

				int pixelIndex = actualY * Width + actualX;

				if (pixelIndex < 0 || pixelIndex >= TextureData.Length)
				{
					return;
				}

				TextureData[pixelIndex] = TargetColor;
			}
		}

		public int GetGroupStartIdByUV(Vector2 uv)
		{
			if (!initialized) return -1;
			// Convert UV coordinates to pixel coordinates
			int x = Mathf.FloorToInt(uv.x * textureWidth);
			int y = Mathf.FloorToInt(uv.y * textureHeight);

			// Clamp to texture boundaries
			x = Mathf.Clamp(x, 0, textureWidth - 1);
			y = Mathf.Clamp(y, 0, textureHeight - 1);

			// Calculate the pixel index
			int pixelIndex = y * textureWidth + x;

			// Iterate through each group to find if this pixel index belongs to any group
			for (int i = 0; i < groupStarts.Length; i++)
			{
				int groupStart = groupStarts[i];
				var groupEntries = groupResults.GetValuesForKey(groupStart);

				while (groupEntries.MoveNext())
				{
					if (groupEntries.Current == pixelIndex)
					{
						// Return the group start ID if the pixel index is found in the group
						return groupStart;
					}
				}
			}

			// Return -1 or any other indicator if no group is found for this UV position
			return -1;
		}


		public List<int> GetAllGroupStartsIds()
		{
			if (!initialized) return default;
			List<int> list = new List<int>(groupStarts.Length);

			for (int i = 0; i < groupStarts.Length; i++)
			{
				list.Add(groupStarts[i]);
			}

			return list;
		}

		public int GetPixelCountInGroup(int groupStartID)
		{
			if (!initialized) return -1;
			var groupEntries = groupResults.GetValuesForKey(groupStartID);

			int count = 0;
			// Iterate over the group entries and collect the pixels
			while (groupEntries.MoveNext())
			{
				count++;
			}

			return count;
		}

		public void SetPixelColorInGroupNonAlloc(int groupStartID, List<Color32> groupPixels)
		{
			if (!initialized) return;
			if (groupStartID < 0 || !groupStarts.Contains(groupStartID))
			{
				Debug.LogError($"Invalid groupStartID: {groupStartID}.");
				return;
			}

			pixelsNonAllocated = texture.GetRawTextureData<Color32>();

			// Find the group based on the provided groupStartID
			var groupEntries = groupResults.GetValuesForKey(groupStartID);

			// Iterate over the group entries (pixel indices)
			int i = 0;
			while (groupEntries.MoveNext())
			{
				int pixelIndex = groupEntries.Current;
				pixelsNonAllocated[pixelIndex] = groupPixels[i];
				i++;
			}

			texture.Apply();
		}

		public List<Color32> GetPixelsByGroupStart(int groupStartID)
		{
			if (!initialized) return default;
			if (groupStartID < 0 || !groupStarts.Contains(groupStartID))
			{
				Debug.LogError($"Invalid groupStartID: {groupStartID}.");
				return null;
			}

			// Find the group based on the provided groupStartID
			var groupEntries = groupResults.GetValuesForKey(groupStartID);

			// Create a list to hold the pixels in this group
			List<Color32> groupPixels = new List<Color32>();

			// Iterate over the group entries and collect the pixels
			while (groupEntries.MoveNext())
			{
				int pixelIndex = groupEntries.Current;
				groupPixels.Add(pixels[pixelIndex]);
			}

			// Return the collected pixels as an array
			return groupPixels;
		}

		public void SetPixelsColorInGroupNonAlloc(int groupStartID, Color32 color)
		{
			if (!initialized) return;
			if (groupStartID < 0 || !groupStarts.Contains(groupStartID))
			{
				Debug.LogError($"Invalid groupStartID: {groupStartID}.");
				return;
			}

			pixelsNonAllocated = texture.GetRawTextureData<Color32>();

			// Find the group based on the provided groupStartID
			var groupEntries = groupResults.GetValuesForKey(groupStartID);

			// Iterate over the group entries (pixel indices)
			while (groupEntries.MoveNext())
			{
				int pixelIndex = groupEntries.Current;
				pixelsNonAllocated[pixelIndex] = color;
			}

			texture.Apply();
		}

		public void SetGroupTransparencyAndRemove(int groupStartID)
		{
			if (!initialized) return;
			Color32 transparencyColor = new Color32(0, 0, 0, 0);
			SetPixelsColorInGroupNonAlloc(groupStartID, transparencyColor);
			int groupIndex = groupStarts.IndexOf(groupStartID);
			groupStarts.RemoveAt(groupIndex);
		}

		/// <summary>
		/// Get the last element's transparency percentage
		/// </summary>
		/// <param name="transparencyPercentage"></param>
		/// <param name="nonOpaquePercentage"></param>
		public void AnalyzeTransparency(out float transparencyPercentage, out float nonOpaquePercentage)
		{
			transparencyPercentage = 0;
			nonOpaquePercentage = 0;
			
			if (!initialized) return;
			if (groupStarts.Length == 0)
			{
				Debug.LogWarning("No groups available for analysis.");
				return;
			}

			Debug.Log("Analizing group transparency");

			NativeArray<float> transparencyPercentages = new NativeArray<float>(groupStarts.Length, Allocator.TempJob);
			NativeArray<float> nonOpaquePercentages = new NativeArray<float>(groupStarts.Length, Allocator.TempJob);

			var analyzeTransparencyJob = new AnalyzeTransparencyJob
			{
				groupResults = groupResults,
				groupStarts = groupStarts,
				pixels = pixels,
				transparencyPercentages = transparencyPercentages,
				nonOpaquePercentages = nonOpaquePercentages
			};

			JobHandle analyzeJobHandle = analyzeTransparencyJob.Schedule(groupStarts.Length, 64);
			analyzeJobHandle.Complete();
			List<int> erazedGroups = new List<int>(groupStarts.Length);

			for (int i = 0; i < groupStarts.Length; i++)
			{
				transparencyPercentage = transparencyPercentages[i];
				nonOpaquePercentage = nonOpaquePercentages[i];
				
				if (transparencyPercentages[i] > 15 && nonOpaquePercentages[i] > 98)
				{
					erazedGroups.Add(groupStarts[i]);
					// Debug.Log(
					// 	$"Group {i}: {nonOpaquePercentages[i]:F2}% non-opaque pixels and {transparencyPercentages[i]:F2}% transparent pixels");
				}
			}

			transparencyPercentages.Dispose();
			nonOpaquePercentages.Dispose();
			if (erazedGroups.Count > 0)
			{
				Debug.Log("Erased groups: " + string.Join(", ", erazedGroups));
				erazedGroups.ForEach(g => OnGroupErased?.Invoke(g));
			}
		}

		public void DecreaseAlphaAtUV(Vector2 uv, int brushSize)
		{
			if (!initialized) return;
			int centerX = Mathf.FloorToInt(uv.x * textureWidth);
			int centerY = Mathf.FloorToInt(uv.y * textureHeight);

			centerX = Mathf.Clamp(centerX, 0, textureWidth - 1);
			centerY = Mathf.Clamp(centerY, 0, textureHeight - 1);

			if (!brushOffsets.IsCreated)
			{
				PrecomputeBrush(brushSize);
			}

			var job = new DecreaseAlphaJob
			{
				TextureData = pixels,
				Width = textureWidth,
				Height = textureHeight,
				CenterX = centerX,
				CenterY = centerY,
				BrushOffsets = brushOffsets,
				MaxDistance = brushSize
			};

			JobHandle jobHandle = job.Schedule(brushOffsets.Length, 64);
			jobHandle.Complete();


			// Apply only the modified pixels to the texture
			texture.SetPixels32(centerX - brushSize, centerY - brushSize, 2 * brushSize + 1, 2 * brushSize + 1,
				ExtractModifiedPixels(centerX, centerY, brushSize));

			texture.Apply();
		}

		private Color32[] ExtractModifiedPixels(int centerX, int centerY, int brushSize)
		{
			if (!initialized) return default;
			int blockWidth = 2 * brushSize + 1;
			int blockHeight = 2 * brushSize + 1;

			if (modifiedPixels == null || modifiedPixels.Length != blockWidth * blockHeight)
				// this should be cashed to avoid re-allocations
				modifiedPixels = new Color32[blockWidth * blockHeight];

			for (int y = 0; y < blockHeight; y++)
			{
				for (int x = 0; x < blockWidth; x++)
				{
					int texX = centerX - brushSize + x;
					int texY = centerY - brushSize + y;

					if (texX >= 0 && texX < textureWidth && texY >= 0 && texY < textureHeight)
					{
						int textureIndex = texY * textureWidth + texX;
						int arrayIndex = y * blockWidth + x;
						modifiedPixels[arrayIndex] = pixels[textureIndex];
					}
				}
			}

			return modifiedPixels;
		}

		private void PrecomputeBrush(int brushSize)
		{
			if (!initialized) return;
			if (brushOffsets.IsCreated)
			{
				brushOffsets.Dispose();
			}

			int brushArea = (2 * brushSize + 1) * (2 * brushSize + 1);
			brushOffsets = new NativeArray<Vector2Int>(brushArea, Allocator.Persistent);

			int index = 0;
			for (int y = -brushSize; y <= brushSize; y++)
			{
				for (int x = -brushSize; x <= brushSize; x++)
				{
					if (x * x + y * y <= brushSize * brushSize)
					{
						brushOffsets[index++] = new Vector2Int(x, y);
					}
				}
			}
		}

		public async Task SaveTextureAsync(string savePath)
		{
			if (!initialized) return;
			string directory = Path.GetDirectoryName(savePath);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
				Debug.Log("Directory created at: " + directory);
			}

			byte[] bytes = texture.EncodeToPNG();

			// Using WriteAllBytesAsync for asynchronous file write
			await File.WriteAllBytesAsync(savePath, bytes);
			Debug.Log("Texture saved to: " + savePath);
		}

		[BurstCompile]
		private struct DecreaseAlphaJob : IJobParallelFor
		{
			[NativeDisableParallelForRestriction] public NativeArray<Color32> TextureData;
			public NativeArray<Vector2Int> BrushOffsets;

			public int Width;
			public int Height;
			public int CenterX;
			public int CenterY;
			public float MaxDistance;

			public void Execute(int index)
			{
				Vector2Int offset = BrushOffsets[index];

				int actualX = CenterX + offset.x;
				int actualY = CenterY + offset.y;

				actualY = math.clamp(actualY, 0, Height - 1);
				actualX = math.clamp(actualX, 0, Width - 1);

				int pixelIndex = actualY * Width + actualX;

				if (pixelIndex < 0 || pixelIndex >= TextureData.Length)
				{
					return;
				}

				Color32 color = TextureData[pixelIndex];

				if (color.a == 0) return;

				// Calculate distance from the center
				float distance = math.sqrt(offset.x * offset.x + offset.y * offset.y);

				// Calculate falloff factor: 1.0 at center, 0.95 at edges
				float falloff = math.lerp(0, 1.0f, distance / MaxDistance);

				// Blend the new alpha value with the existing one
				byte newAlpha = (byte)(color.a * falloff);

				// Ensure the alpha value decreases
				if (newAlpha < color.a)
				{
					color.a = newAlpha;
				}

				// Force full transparency if close to the edge or center
				if (falloff < 0.1f || distance <= MaxDistance * 0.1f)
				{
					color.a = 0;
				}

				TextureData[pixelIndex] = color;
			}
		}

		[BurstCompile]
		private struct CountGroupSizesJob : IJobParallelFor
		{
			[ReadOnly] public NativeParallelMultiHashMap<int, int> groupResults;
			[ReadOnly] public NativeList<int> groupStarts;
			public NativeArray<int> groupSizes;

			public void Execute(int index)
			{
				int groupStart = groupStarts[index];
				int groupSize = 0;

				var groupEntries = groupResults.GetValuesForKey(groupStart);
				while (groupEntries.MoveNext())
				{
					groupSize++;
				}

				groupSizes[index] = groupSize;
			}
		}

		[BurstCompile]
		private struct FilterGroupsJob : IJobParallelFor
		{
			[ReadOnly] public NativeParallelMultiHashMap<int, int> groupResults;
			[ReadOnly] public NativeArray<int> groupSizes;
			public int minPixelPerGroup;

			public NativeParallelMultiHashMap<int, int>.ParallelWriter filteredGroupResults;
			public NativeList<int>.ParallelWriter validGroupStarts;

			public void Execute(int index)
			{
				int groupStart = index;

				if (groupSizes[index] >= minPixelPerGroup)
				{
					validGroupStarts.AddNoResize(groupStart);

					var groupEntries = groupResults.GetValuesForKey(groupStart);
					while (groupEntries.MoveNext())
					{
						filteredGroupResults.Add(groupStart, groupEntries.Current);
					}
				}
			}
		}

		[BurstCompile]
		private struct MarkPixelsSequentialJob : IJob
		{
			public int width;
			public int height;
			public Vector2 colorThreshold;
			public NativeArray<Color32> pixelArray;
			public NativeArray<bool> visited;
			public NativeParallelMultiHashMap<int, int> groupResults;
			public NativeList<int> groupStarts;

			private static readonly int[] dx = { 0, 1, 0, -1 };
			private static readonly int[] dy = { 1, 0, -1, 0 };

			public void Execute()
			{
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = y * width + x;

						if (visited[index] || !IsNonTransparent(pixelArray[index]))
							continue;

						int groupID = groupStarts.Length; // Use groupStarts.Length as the group ID

						var queue = new NativeQueue<int>(Allocator.Temp);
						queue.Enqueue(index);

						bool groupFormed = false;

						while (queue.Count > 0)
						{
							int currentIndex = queue.Dequeue();
							if (visited[currentIndex]) continue;

							visited[currentIndex] = true;
							groupResults.Add(groupID, currentIndex);
							groupFormed = true;

							int cx = currentIndex % width;
							int cy = currentIndex / width;

							for (int i = 0; i < 4; i++)
							{
								int newX = cx + dx[i];
								int newY = cy + dy[i];

								if (newX >= 0 && newX < width && newY >= 0 && newY < height)
								{
									int neighborIndex = newY * width + newX;
									if (colorThreshold != default)

										if (!visited[neighborIndex] && IsNonTransparent(pixelArray[neighborIndex], threshold: colorThreshold))
										{
											queue.Enqueue(neighborIndex);
										}
								}
							}
						}

						if (groupFormed)
						{
							groupStarts.Add(
								groupID); // Only add groupID after confirming that the group has been formed
						}

						queue.Dispose();
					}
				}
			}

			private bool IsNonTransparent(Color32 pixelColor, Vector2 threshold = default)
			{
				
				if (threshold != default)
				{
					return
						pixelColor.a > threshold.x && pixelColor.a < threshold.y;
				}
				else
				{
					return pixelColor.a > 0; // Consider non-transparent if alpha is greater than 0
				}
			}
		}

		[BurstCompile]
		private struct AnalyzeTransparencyJob : IJobParallelFor
		{
			[ReadOnly] public NativeParallelMultiHashMap<int, int> groupResults;
			[ReadOnly] public NativeList<int> groupStarts;
			[ReadOnly] public NativeArray<Color32> pixels;

			public NativeArray<float> transparencyPercentages;
			public NativeArray<float> nonOpaquePercentages;

			public void Execute(int index)
			{
				int groupStart = groupStarts[index];
				int totalPixels = 0;
				int transparentPixels = 0;
				int nonOpaquePixels = 0;

				var groupEntries = groupResults.GetValuesForKey(groupStart);
				while (groupEntries.MoveNext())
				{
					int pixelIndex = groupEntries.Current;

					totalPixels++;
					byte a = pixels[pixelIndex].a;
					// if (a < (1 - eraseThresholdPercentage) * 255)
					if (a == 0)
					{
						transparentPixels++;
					}

					if (a < 255)
					{
						nonOpaquePixels++;
					}
				}

				// Calculate the percentage of transparent pixels
				if (totalPixels > 0)
				{
					transparencyPercentages[index] = ((float)transparentPixels / totalPixels) * 100f;

					// Calculate the percentage of non-opaque pixels
					nonOpaquePercentages[index] = ((float)nonOpaquePixels / totalPixels) * 100f;
				}
				else
				{
					transparencyPercentages[index] = 0f;
					nonOpaquePercentages[index] = 100f; // Set the non-opaque percentage to 100
				}
			}
		}
	}
}