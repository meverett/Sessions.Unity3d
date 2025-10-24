using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DelayedDisable : MonoBehaviour
{
    /// <summary>
    /// The delay in seconds to wait before disabling the game object.
    /// </summary>
    public float Delay = 1f;

    private void Start()
    {
        StartCoroutine(DoDelay(Delay));
    }

    private void OnEnable()
    {
        StartCoroutine(DoDelay(Delay));
    }

    IEnumerator DoDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false); // disable self
    }

    public void ResetDelay()
    {
        StopAllCoroutines();
        StartCoroutine(DoDelay(Delay));
    }
}
