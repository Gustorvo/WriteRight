using System;
using System.Collections;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace _Project.Scripts
{
	/// <summary>
	/// Will erase the mesh texture when the brush collides with it
	/// Relies on the SphereToMeshCollisionDetector to detect collisions
	/// To paint, use if (useNonAllocated) modifier.ModifyPixelsAtUVNonAlloc(uv, brushSize, (Color32)paintColor) or modifier.ModifyPixelsAtUV(uv, brushSize, (Color32)paintColor);
	/// </summary>
	public class TextureEraser : MonoBehaviour
	{
		//[SerializeField] float successThreshold = 
		[SerializeField] private Color32 paintColor;
		[SerializeField] private VrStylusHandler stylusHandler;
		[SerializeField] int tipSize = 5;


		[SerializeField] private int minPixelPerGroup = 100;
		[SerializeField] private bool saveTextureOnExit;

		[SerializeField, ShowIf("saveTextureOnExit")]
		string savePath = "Assets/_Project/Paint/SavedTextures/ModifiedTexture.png";

		[SerializeField] Texture2D[] levelTextures;

		private MeshRenderer meshRenderer;

		//private BackMesh backMesh = null;
		private TextureModifier modifier = null;
		private int groupsTotal = 0;
		private Coroutine checkGroupsCoroutine;
		private bool initialized;
		public event Action OnAllGroupsErased;

		private TipPosition tip;
		private int currentLevel = 0;


		private void Awake()
		{
			tip = GetComponent<TipPosition>();
			tip.OnTipCollisionEnd += OnTipCollisionEnd;
			//BeaverController.OnBeaverStateChange += LevelUp;
		}

		public void LevelUp()
		{
			//if (state != BeaverState.Idle) return;
			currentLevel += 1;
			if (currentLevel == 4)
			{
				Debug.LogError("Max level reached");
				ResetTexture();
				//Application.Quit();
				return;
			}

			SetTargetTextureForLevel(currentLevel);
			InitMeshRenderer(meshRenderer.transform);
		}

		private void OnTipCollisionEnd(Vector3 obj)
		{
			float transparent = 0;
			float opaque = 0;

			modifier.AnalyzeTransparency(out transparent, out opaque);
			Debug.Log("TraErrornsparent: " + transparent + " Opaque: " + opaque);
		}

		public void Write(Vector2 uv)
		{
			if (!initialized)
			{
				Debug.LogError("Texture erazer not initialized");
				return;
			}

			modifier.ModifyPixelsAtUVNonAlloc(uv, tipSize, paintColor);
		}

		public void SetTargetTextureForLevel(int level)
		{
			if (level > levelTextures.Length)
			{
				Debug.LogError("There are only " + levelTextures.Length + " levels");
				return;
			}

			Texture2D texture2D = levelTextures[level - 1];
			InitModifier(texture2D);
		}

		public void InitMeshRenderer(Transform meshTransform)
		{
			if (!meshTransform.TryGetComponent(out MeshRenderer mr))
			{
				Debug.LogError("Smth is wrong! Mesh renderer should be present");
				return;
			}

			meshRenderer = mr;
			if (modifier != null && modifier.texture != null)
			{
				// mesh texture has changed, we need to re-initialized the modifier
				// reassign mesh renderer's texture
				Debug.Log("Setting new texture");
				meshRenderer.material.mainTexture = modifier.texture;
				initialized = true;
			}
			else
			{
				meshRenderer.material.mainTexture = null;
				Debug.LogError("modifier is not ready");
			}
		}

		[Button]
		private void InitWithDefaultTexture()
		{
			InitModifier(levelTextures[0]);
		}

		[Button]
		public void ResetTexture() => modifier.ResetTexture();

		[Button]
		private void EraseAllGroups()
		{
			var ids = modifier.GetAllGroupStartsIds();
			modifier.SetGroupTransparencyAndRemove(ids.FirstOrDefault());
		}

		private void InitModifier(Texture2D texture)
		{
			if (modifier != null)
			{
				modifier.OnGroupErased -= CheckGroupsErased;
				modifier.Dispose();
			}

			modifier = new TextureModifier(texture);
			groupsTotal = modifier.DivideTextureIntoGroups(minPixelPerGroup, new Vector2(30f, 70f));
			if (groupsTotal > 0)
			{
				modifier.OnGroupErased += CheckGroupsErased;
			}
			else
			{
				Debug.LogError("Texture is possibly empty or is a solid color");
			}
		}

		private void OnDestroy()
		{
			modifier.OnGroupErased -= CheckGroupsErased;
			tip.OnTipCollisionEnd -= OnTipCollisionEnd;
			//BeaverController.OnBeaverStateChange -= LevelUp;
		}

		private IEnumerator CheckGroupsErasedCoroutine()
		{
			while (isActiveAndEnabled)
			{
				yield return new WaitForSeconds(1);
				modifier.AnalyzeTransparency(out float _, out float _);
			}
		}

		private void CheckGroupsErased(int groupId)
		{
			groupsTotal--;

			if (groupsTotal <= 0)
			{
				StopCoroutine(checkGroupsCoroutine);
				checkGroupsCoroutine = null;
				modifier.Dispose();
				initialized = false;
				OnAllGroupsErased?.Invoke();
			}
			else
			{
				Debug.Log("Group erazed");
				modifier.SetGroupTransparencyAndRemove(groupId);
			}
		}

		private void Erase(TipCollision tipCollision)
		{
			if (!initialized) return;
			int size = Mathf.RoundToInt(Mathf.Lerp(7, 25, tipCollision.penetrationValue));
			modifier.DecreaseAlphaAtUVNonAlloc(tipCollision.uv, size);
		}


		[Button]
		public async void RunAsyncSaveTextureInBackground() => await modifier.SaveTextureAsync(savePath);


		void OnApplicationQuit()
		{
			if (saveTextureOnExit)
			{
				SaveTextureAndDispose();
			}
			else
			{
				modifier.Dispose();
			}
		}

		private async void SaveTextureAndDispose()
		{
			await modifier.SaveTextureAsync(savePath);
			modifier.Dispose();
		}
	}
}