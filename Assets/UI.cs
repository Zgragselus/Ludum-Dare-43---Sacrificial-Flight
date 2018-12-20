using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI bound class
/// </summary>
public class UI : MonoBehaviour
{
    /// <summary>
    /// Passengers
    /// </summary>
    public Passengers Data;

    /// <summary>
    /// Button to throw passenger out of balloon
    /// - just needs to set Alive flag to false
    /// - Passenger ID is set statically through editor
    /// </summary>
    /// <param name="id">Passenger ID</param>
    public void ThrowOut(int id)
    {
        Data.passengers[id].Alive = false;
    }
}
