using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Assertions;


public enum BeaverState
{
	None,
	Idle,
	Introduction,
	Instruction,
	Success
}

[Serializable]
public struct BeaverStep
{
	public AudioClip clip;
	public BeaverState state;

	public bool IsNull()
	{
		return state.Equals(default(BeaverState));
	}
}

public class BeaverController : MonoBehaviour
{
	[SerializeField] private List<BeaverStep> steps = new List<BeaverStep>();
	[SerializeField] Animator animator;

	private BeaverState currentState = BeaverState.None;

	private void Awake()
	{
		Assert.IsNotNull(animator);
	}

	private void Start()
	{
		PlayStep(BeaverState.Introduction);
	}

	public void PlayStep(BeaverState state)
	{
		// get the step from list
		var step = steps.FirstOrDefault(s => s.state == state);
		if (step.IsNull())
		{
			Debug.LogError("Beaver: No step found for state " + state);
			return;
		}

		// play the step
		animator.SetTrigger(state.ToString());
		AudioSource.PlayClipAtPoint(step.clip, transform.position);
		currentState = step.state;
	}

	private void Update()
	{
		//Check the animation progress
		if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
		{
			animator.StopPlayback();
			// play the next step
			 var next = currentState + 1;
			if (next < (BeaverState)steps.Count)
			{
				Debug.Log("Next step is triggered: " + next);
				PlayStep(next);
			}
		}
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
	public void ToInstruction()
	{
		PlayStep(BeaverState.Instruction);
	}
	

	[Button]
	public void ToSuccess()
	{
		PlayStep(BeaverState.Success);
	}
}