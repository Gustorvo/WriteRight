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
		return clip == null && state.Equals(default(BeaverState));
	}
}

public class BeaverController : MonoBehaviour
{
	[SerializeField] private List<BeaverStep> steps = new List<BeaverStep>();
	[SerializeField] Animator animator;

	private void Awake()
	{
		Assert.IsNotNull(animator);
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