using System;
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

	public static event Action<BeaverState> OnBeaverStateChange;
	public BeaverState currentState = BeaverState.None;
	private bool wasAudioPlaying = false;
	private int level = 0;

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

		// play the step
		animator.StopPlayback();
		animator.SetTrigger(state.ToString());

		// play the audio
		if (state == BeaverState.Introduction)
		{
			audioSource.clip = GetAudioForLevel(level);
			audioSource.Play();
		}

		currentState = state;
		OnBeaverStateChange?.Invoke(state);
	}

	private void Update()
	{
		// check the progress of audio
		if (audioSource.isPlaying)
		{
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