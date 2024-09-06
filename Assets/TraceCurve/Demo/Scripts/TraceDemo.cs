using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TraceCurve.Demo
{
	public class TraceDemo : MonoBehaviour
	{
		[Serializable]
		public class DemoTraceComponents
		{
			public enum ComponentsType
			{
				Canvas,
				SpriteRenderer,
				MeshRenderer
			}

			public ComponentsType Type;
			public GameObject GameObject;
		}

		#region Variables

		public DemoTraceComponents[] DemoComponents;
		public DemoTraceComponents.ComponentsType SelectedComponent;
		public TraceFiller TraceFiller;
		public Button HideTutorial;
		public Button Restart;
		public Slider Fill;
		public Toggle AutoFill;
		public Toggle Cursor;
		public Toggle Background;
		public Toggle Follow;
		public Text SliderText;
		public GameObject Tutorial;
		public Transform TutorialContainer;

		private GameObject demoObject;
		private TraceInput input;
		private Background background;
		private float progress;
		private int runningCount;
		private bool updateFill;
		private bool updateProgress;
		private bool moveForward = true;
		private int geometryIndex = 1;
		private readonly int[] geometrySteps = {
			0, 2, 5, 9, 11, 14, 17, 20, 23, 29, 32, 33
		};
			
		private const float DivideRatio = 20f;
		private const float TutorialDuration = 5f;
		private const int TutorialMaxShows = 3;
		private const string RunningCount = "RunningCount";

		#endregion

		#region Unity

		void Awake()
		{
#if !UNITY_WEBGL
			Application.targetFrameRate = Mathf.Clamp(Screen.currentResolution.refreshRate, 30, Screen.currentResolution.refreshRate);
#endif
		}

		void Start()
		{
			runningCount = PlayerPrefs.GetInt(RunningCount, 0);
			PlayerPrefs.SetInt(RunningCount, runningCount + 1);
			demoObject = Instantiate(DemoComponents.First(x => x.Type == SelectedComponent).GameObject);
			demoObject.SetActive(true);
			TraceFiller.TracePainter = demoObject.GetComponentInChildren<TracePainter>();
			TraceFiller.TracePainter.SetBrushAngleFollowPath = Follow.isOn;
			TraceFiller.GeometryContainer = demoObject.GetComponentInChildren<GeometryContainer>();
			input = demoObject.GetComponentInChildren<TraceInput>();
			input.OnProgress += inputProgress =>
			{
				Fill.value = inputProgress;
				updateProgress = false;
			};
			background = demoObject.GetComponent<Background>();
			var canvasScaler = demoObject.GetComponentInChildren<TraceCanvasScaler>();
			if (canvasScaler != null)
			{
				canvasScaler.Filler = TraceFiller;
			}
			HideTutorial.onClick.AddListener(OnHideTutorial);
			Restart.onClick.AddListener(OnRestart);
			Fill.onValueChanged.AddListener(OnFillSlider);
			AutoFill.onValueChanged.AddListener(OnAutoFillToggle);
			Cursor.onValueChanged.AddListener(OnCursorToggle);
			Background.onValueChanged.AddListener(OnBackgroundToggle);
			Follow.onValueChanged.AddListener(OnFollowToggle);
			
			if (runningCount < TutorialMaxShows)
			{
				StartCoroutine(ShowTutorial());
			}
		}

		void Update()
		{
			if (updateFill || updateProgress)
			{
				progress += moveForward ? Time.deltaTime / DivideRatio : -Time.deltaTime / DivideRatio;
				if (progress >= 1f || progress <= 0f)
				{
					moveForward = !moveForward;
				}

				int geometry;
				int point;
				TraceFiller.UpdateProgress(progress, out geometry, out point);
				input.SetProgress(geometry, point);
				Fill.value = progress;
				updateProgress = false;
			}

#if ENABLE_INPUT_SYSTEM
			if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
#elif ENABLE_LEGACY_INPUT_MANAGER
			if (Input.GetKeyDown(KeyCode.Space))
#endif
			{
				if (geometryIndex + 1 > geometrySteps.Length)
				{
					geometryIndex = 0;
				}
				else if (geometryIndex + 1 == geometrySteps.Length)
				{
					int outGeometry;
					int outPoint;
					TraceFiller.UpdateProgress(1f, out outGeometry, out outPoint);
					input.SetProgress(outGeometry, outPoint);
					geometryIndex++;
					return;
				}
				var geometry = geometrySteps[geometryIndex];
				TraceFiller.UpdateGeometryProgress(geometry);
				input.SetProgress(geometry, 0);
				geometryIndex++;
			}
		}

		#endregion

		#region UserMethods

		private void OnHideTutorial()
		{
			StopCoroutine(ShowTutorial());
			Tutorial.SetActive(false);
		}

		private void OnAutoFillToggle(bool check)
		{
			updateFill = check;
			input.enabled = !check;
			Fill.interactable = !check;
		}

		private void OnCursorToggle(bool check)
		{
			TraceFiller.TracePainter.BrushObject.gameObject.SetActive(check);
		}

		private void OnBackgroundToggle(bool check)
		{
			background.BackgroundObject.SetActive(check);
		}

		private void OnFollowToggle(bool check)
		{
			TraceFiller.TracePainter.SetBrushAngleFollowPath = check;
			if (!check)
			{
				TraceFiller.TracePainter.BrushObject.localEulerAngles = Vector3.zero;
			}
		}

		private void OnRestart()
		{
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		}

		private void OnFillSlider(float value)
		{
			updateProgress = true;
			progress = value;
			SliderText.text = ((int) (progress * 100f)).ToString();
		}

		#endregion

		#region Coroutines

		private IEnumerator ShowTutorial()
		{
			yield return new WaitForEndOfFrame();
			Tutorial.SetActive(true);
			if (SelectedComponent == DemoTraceComponents.ComponentsType.Canvas &&
			    demoObject.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay)
			{
				var point = Camera.main.ScreenToWorldPoint(TraceFiller.TracePainter.BrushObject.position);
				TutorialContainer.transform.position = new Vector3(point.x, point.y, TutorialContainer.transform.position.z);
			}
			else
			{
				var brushPosition = TraceFiller.TracePainter.BrushObject.position;
				TutorialContainer.transform.position = new Vector3(brushPosition.x, brushPosition.y, TutorialContainer.transform.position.z);
			}
			yield return new WaitForSeconds(TutorialDuration);
			Tutorial.SetActive(false);
		}

		#endregion
	}
}