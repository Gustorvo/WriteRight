using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Project.Scripts;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Assertions;


public enum BeaverState
{
	None,
	Introduction,
	Idle,
	Success
}


public class BeaverController : MonoBehaviour
{
	[SerializeField] private AudioSource audioSource;
	[SerializeField] Animator animator;

	[SerializeField] List<AudioClip> instrucionClips;
	[SerializeField] private AudioClip yahoo;
	[SerializeField] private AudioClip success;

	public static event Action<BeaverState> OnBeaverStateChange;
	public BeaverState currentState = BeaverState.None;
	private bool wasAudioPlaying = false;
	private int level = 0;
	private Coroutine transitioning;

	private void Awake()
	{
		Assert.IsNotNull(animator);
		A4.OnLetterWritten += LetterWritten;
	}

	private void OnDestroy()
	{
		A4.OnLetterWritten -= LetterWritten;
	}

	private void LetterWritten()
	{
		PlayStep(BeaverState.Success);
	}

	private void Start()
	{
		level = 0;
		PlayStep(BeaverState.Introduction);
	}

	public AudioClip GetAudioForLevel(int level) => instrucionClips[level];

	public void PlayStep(BeaverState state)
	{
		// get the step from list

		if (state == BeaverState.None)
		{
			Debug.LogError("Beaver: No step found for state " + state);
			return;
		}


		// play the audio
		if (state == BeaverState.Introduction)
		{
			audioSource.clip = GetAudioForLevel(level);
			audioSource.Play();
		}

		if (state == BeaverState.Success)
		{
			audioSource.PlayOneShot(success);
			audioSource.clip = yahoo;
			audioSource.PlayDelayed(1f);
			transitioning = StartCoroutine(DelayCoroutine(1f, () => PlayAnimation(state)));
			//audioSource.
		}
		else
		{
			PlayAnimation(state);
		}


		currentState = state;
		OnBeaverStateChange?.Invoke(state);
	}

	IEnumerator DelayCoroutine(float time, Action callback)
	{
		yield return new WaitForSeconds(time);
		callback?.Invoke();
		transitioning = null;
	}

	private void PlayAnimation(BeaverState state, float delay = 0)
	{
		// play the step
		animator.StopPlayback();
		animator.SetTrigger(state.ToString());
	}

	private void Update()
	{
		if (transitioning != null) return;
		// check the progress of audio
		if (audioSource.isPlaying)
		{
			if (currentState == BeaverState.Success) return;
			wasAudioPlaying = true;
			return;
		}

		if (wasAudioPlaying)
		{
			float pr = GetAnimeProgress();
			StopCurrentAndTriggerNext();
			wasAudioPlaying = false;
			return;
		}

		float progress = GetAnimeProgress();
		//Check the animation progress
		if (progress >= .99f)
		{
			StopCurrentAndTriggerNext();
		}
	}

	private float GetAnimeProgress()
	{
		var info = animator.GetCurrentAnimatorStateInfo(0);
		float progress = info.normalizedTime;
		// If the animation is looping, progress can be greater than 1; use modulo to get the fractional part
		progress %= 1f;
		return progress;
	}

	private void StopCurrentAndTriggerNext()
	{
		var next = currentState + 1;

		//return and wait for the letter to be written
		if (next == BeaverState.Success) return;

		animator.ResetTrigger(currentState.ToString());
		animator.StopPlayback();
		// play the next step


		if ((int)next < System.Enum.GetValues(typeof(BeaverState)).Length)
		{
			Debug.Log("Next step is triggered: " + next);
		}
		else
		{
			Debug.Log("All steps are done. Will loop from the beginning");
			next = BeaverState.Introduction;
			// increase the level
			level += 1;
		}

		PlayStep(next);
	}


	[Button]
	public void ToIntro()
	{
		PlayStep(BeaverState.Introduction);
	}

	[Button]
	public void SetToIdle()
	{
		PlayStep(BeaverState.Idle);
	}


	[Button]
	public void ToSuccess()
	{
		PlayStep(BeaverState.Success);
	}
}