using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script for behavior of gas meter (UI) in bottom left of the screen
/// - controls the pointer showing how much gas we have left
/// </summary>
public class GasMeter : MonoBehaviour
{
    /// <summary>
    /// Base balloon class (main game logic class)
    /// </summary>
    public Balloon balloon;

    /// <summary>
    /// Number of uses left on gas
    /// </summary>
    int currentUses = 6;

    /// <summary>
    /// Angle change per use (270 degrees in total - 6 uses -> 45)
    /// </summary>
    float angleMod = -45.0f;

    /// <summary>
    /// Current target (-45 -> default)
    /// </summary>
    float angleTarget = -45.0f;

    void Start ()
    {
        currentUses = balloon.GasUses;
    }
	
    /// <summary>
    /// Simply interpolate over time towards target value
    /// </summary>
	void Update ()
    {
        /// If gas is used (read from balloon class) - change target value
        if (balloon.GasUses < currentUses)
        {
            angleTarget = -45.0f + (float)(currentUses - balloon.GasUses) * 45.0f;
        }
        
        if (angleMod < angleTarget)
        {
            angleMod += 15.0f * Time.deltaTime;
        }
        else if (angleMod > angleTarget)
        {
            angleMod -= 15.0f * Time.deltaTime;
        }

        transform.rotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, angleMod));
	}
}
