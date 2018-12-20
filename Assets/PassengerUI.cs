using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// User interface for each passenger (which generates the text in label and toggle button)
/// </summary>
public class PassengerUI : MonoBehaviour
{
    /// <summary>
    /// Ref. to label (passenger name + score)
    /// </summary>
    public Text PassengerText;

    /// <summary>
    /// Ref. to toggle (throw out + weight)
    /// </summary>
    public Text ToggleText;

    /// <summary>
    /// Toggle button (needs to be disabled once passenger is thrown out)
    /// </summary>
    public Toggle PassengerToggle;

    /// <summary>
    /// Global passengers data
    /// </summary>
    public Passengers Data;

    /// <summary>
    /// Passenger ID
    /// </summary>
    public int ID;

    /// <summary>
    /// Initialization - brute force loop over passengers to find the one with matching ID
    /// - could be improved with associative container/tree, but no need (6 passengers only now!)
    /// - sets texts with name, score, weight
    /// </summary>
	void Start ()
    {
        for (int i = 0; i < Passengers.kMaxPassengers; i++)
        {
            if (Data.passengers[i].Id == ID)
            {
                PassengerText.text = Data.passengers[i].Name + " ($" + Data.passengers[i].NetWorth + ")";
                ToggleText.text = "Throw Out! " + "(" + (50 + Data.passengers[i].Weight).ToString() + "kg)";
                break;
            }
        }
    }

    /// <summary>
    /// Each frame brute force over passengers to find matching ID
    /// - again ineffecient, but not big problem due to low number of passengers
    /// - if passenger isn't alive - disallow interaction with toggle button
    /// </summary>
    void Update()
    {
        for (int i = 0; i < Passengers.kMaxPassengers; i++)
        {
            if (Data.passengers[i].Id == ID)
            {
                if (Data.passengers[i].Alive == false)
                {
                    PassengerToggle.interactable = false;
                }
            }
        }
    }
}
