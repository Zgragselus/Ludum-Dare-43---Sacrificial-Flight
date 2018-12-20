using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base passenger class
/// Passengers have some weight, and therefore have major part in physics of balloon movement, 
/// main logic for them is implemented here.
/// </summary>
public struct Passenger
{
    /// <summary>
    /// ID of the passenger
    /// </summary>
    public int Id;

    /// <summary>
    /// Name of the passenger (displayed)
    /// </summary>
    public string Name;

    /// <summary>
    /// Total worth of the passenger (for score)
    /// </summary>
    public int NetWorth;

    /// <summary>
    /// Weight of the passenger (impacts altitude change of balloon)
    /// </summary>
    public int Weight;

    /// <summary>
    /// Is passenger alive
    /// Living passenger is in the balloon and increases weight
    /// Dead passenger is out of the balloon (e.g. doesn't increase total weight)
    /// </summary>
    public bool Alive;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="id">Passenger ID</param>
    /// <param name="name">Passenger Name</param>
    /// <param name="netWorth">Total score for the passenger</param>
    /// <param name="weight">Total weight of the passenger</param>
    public Passenger(int id, string name, int netWorth, int weight)
    {
        Id = id;
        Name = name;
        NetWorth = netWorth;
        Weight = weight;
        Alive = true;
    }
}

/// <summary>
/// Holds all the passengers, and allows us to interact with them through this class
/// </summary>
public class Passengers : MonoBehaviour
{
    /// <summary>
    /// Passenger count
    /// </summary>
    public const int kMaxPassengers = 6;

    /// <summary>
    /// List of passengers
    /// </summary>
    public Passenger[] passengers;
    
    /// <summary>
    /// Start with creating passengers, their net worth/weight
    /// While randomization could work well, game wouldn't have same difficulty for everyone
    /// </summary>
    void Awake()
    {
        passengers = new Passenger[kMaxPassengers];
        passengers[0] = new Passenger(0, "Esmae Massey", 8000, 30);
        passengers[1] = new Passenger(1, "Kai Mcnamara", 2000, 20);
        passengers[2] = new Passenger(2, "Glen Delacruz", 1000, 10);
        passengers[3] = new Passenger(3, "Mica Wilkerson", 1000, 10);
        passengers[4] = new Passenger(4, "Ebony Camp", 16000, 60);
        passengers[5] = new Passenger(5, "Abdul Hills", 4000, 20);
    }

    /// <summary>
    /// Debug function to print
    /// </summary>
    public void Print()
    {
        for (int i = 0; i < kMaxPassengers; i++)
        {
            if (passengers[i].Alive)
            {
                print("Passenger " + i + " ALIVE");
            }
            else
            {
                print("Passenger " + i + " DEAD");
            }
        }
    }

    /// <summary>
    /// Calculate total weight of all living passengers
    /// </summary>
    /// <returns>Sum of passengers weight</returns>
    internal int CalculateWeight()
    {
        if (passengers != null)
        {
            int sum = 0;

            for (int i = 0; i < kMaxPassengers; i++)
            {
                if (passengers[i].Alive)
                {
                    sum += passengers[i].Weight;
                }
            }

            return sum;
        }
        else
        {
            return 0;
        }
    }
}
