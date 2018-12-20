using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main class containing player-controlled balloon, and also determining whether game has ended or not
/// 
/// </summary>
public class Balloon : MonoBehaviour
{
    /// <summary>
    /// Physics constants for physically-based behavior of balloon
    /// </summary>
    
    public const float kDryAirJoules = 287;                                 // Dry air energy
    public const float kAtmoPressure = 101300;                              // Atmospheric pressure (unused!)
    public const float kOutsideTemparetureInKelvin = 293;                   // Outside temperature (const. ~20C)
    public const float kThickness = 0.01f;                                  // Thickness of enevelope (in meters)
    public const float kNylonLambda = 0.24f;                                // Heat conduction constant for envelope
    public const float kEnveloperRadius = 5;                                // Radius of balloon (in meters)
    public const float kHeatPropertyOfSubstanceJoulesToChange = 1010;       // Heat energy required to change temperature of 1kg of air by 1 kelvin
    public const float kDragCoefficient = 0.5f;                             // Newton's Air drag coefficient
    public float PropaneBurnerPower = 5000000.0f;                           // Energy of burner

    public float Mass;
    public float AirTemperatureInKelvins;
    public float HotAirVolume;

    public float OutsideDensity;
    public float InsideDensity;
    public float Buoynancy;
    public float BuoynancyForce;

    private Rigidbody _rb;
    
    public float timeout = 3.4f;
    private float timer = 0.0f;

    public float Altitude = 0.0f;
    public float WindScale = 0.0f;
    private float Phase = 0.0f;

    public int GasUses = 6;

    public Passengers People;

    public float MassTmp;

    public bool end = false;

    public Text StartText;
    public Text LoseText;
    public Text WinText;

    private float startAlpha = 1.0f;
    private float endAlpha = 0.0f;
    private float startCooldown = 10.0f;

    public Passengers gameData;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Get density of air at given altitude
    /// - uses linear interpolation based on constants defined by measurement
    /// </summary>
    /// <param name="altitude">Altitude</param>
    /// <returns>Density of air at altitude</returns>
    private float GetOutsideDensity(float altitude)
    {
        if (altitude < 1000.0f)
        {
            float t = altitude / 1000.0f;
            t = 1.0f - t;
            return t * 1.225f + (1.0f - t) * 1.112f;
        }
        else if (altitude < 2000.0f)
        {
            float t = (altitude - 1000.0f) / 1000.0f;
            t = 1.0f - t;
            return t * 1.112f + (1.0f - t) * 1.007f;
        }
        else if (altitude < 3000.0f)
        {
            float t = (altitude - 2000.0f) / 1000.0f;
            t = 1.0f - t;
            return t * 1.007f + (1.0f - t) * 0.9093f;
        }
        else if (altitude < 4000.0f)
        {
            float t = (altitude - 3000.0f) / 1000.0f;
            t = 1.0f - t;
            return t * 0.9093f + (1.0f - t) * 0.8194f;
        }
        else if (altitude < 5000.0f)
        {
            float t = (altitude - 4000.0f) / 1000.0f;
            t = 1.0f - t;
            return t * 0.8194f + (1.0f - t) * 0.7364f;
        }
        else if (altitude < 6000.0f)
        {
            float t = (altitude - 5000.0f) / 1000.0f;
            t = 1.0f - t;
            return t * 0.7364f + (1.0f - t) * 0.6601f;
        }
        else
        {
            return 0.6f;
        }
    }

    /// <summary>
    /// Get wind at altitude for given time
    /// - Randomized
    /// - Grows with altitude
    /// </summary>
    /// <param name="altitude">Altitude of object</param>
    /// <param name="phase">Time constant</param>
    /// <returns></returns>
    private float GetWind(float altitude, float phase)
    {
        float perlin = Mathf.PerlinNoise(phase - Mathf.Floor(phase), 0.1f);
        float scale = Mathf.Min(3.0f, altitude / 500.0f);
        return Mathf.Clamp(perlin * scale, 0.0f, 5.0f);
    }

    /// <summary>
    /// In update function we need to resolve blending for intro/outro text (hacky, but works)
    /// </summary>
    void Update()
    {
        /// Start cooldown is alpha blend for intro text (hacky, but works)
        startCooldown -= Time.deltaTime;
        if (startCooldown < 0.0f)
        {
            startAlpha -= 0.5f * Time.deltaTime;
            startAlpha = Mathf.Clamp(startAlpha, 0.0f, 1.0f);
            StartText.color = new Color(1.0f, 1.0f, 1.0f, startAlpha);
        }

        /// In case game has ended
        if (end == true)
        {
            /// Ballon landed too low (on water), then we lost -> blend in lose color based on time
            if (Altitude < 10.0f)
            {
                LoseText.color = new Color(1.0f, 1.0f, 1.0f, endAlpha);
            }
            /// Otherwise we won, calculate score of the player and blend in some funny text
            else
            {
                WinText.color = new Color(1.0f, 1.0f, 1.0f, endAlpha);

                int score = 0;
                int aliveCount = 0;
                string quote = "";
                for (int i = 0; i < 6; i++)
                {
                    if (gameData.passengers[i].Alive)
                    {
                        score += gameData.passengers[i].NetWorth;
                        aliveCount++;
                    }
                }

                if (aliveCount == 0)
                {
                    quote = "Who drove the air ballon when everyone is dead?!";
                }
                else if (aliveCount == 1)
                {
                    if (score == 16000)
                    {
                        quote = "Long live capitalism!";
                    }
                    else if (score == 1000)
                    {
                        quote = "I have a secret for you. Socialism doesn't work!";
                    }
                    else
                    {
                        quote = "Can one man repair a hot air balloon?";
                    }
                }
                else if (aliveCount == 2)
                {
                    quote = "Two people on lonely island... hopefully they are a man and a woman, otherwise humanity is doomed!";
                }
                else if (aliveCount == 3)
                {
                    quote = "There, unless one of you is hot air balloon engineer, nobody is going to leave.";
                }
                else if (aliveCount == 4)
                {
                    quote = "What is the chance of hot air balloon engineer among 4 random people?";
                }
                else if (aliveCount == 5)
                {
                    if (score <= 16000)
                    {
                        quote = "You killed the fat rich one! Well... Communism doesn't work!";
                    }
                    else
                    {
                        quote = "Sometimes, sacrifices are necessary.";
                    }
                }
                else if (aliveCount == 6)
                {
                    quote = "You're a hero ... pffft! ... Why do you have to save everyone?! That's too boring!";
                }

                WinText.text = "You've landed on island, congratulations!\nYour Score: $" + score + "\n" + quote;
            }

            endAlpha += 0.5f * Time.deltaTime;
            endAlpha = Mathf.Clamp(endAlpha, 0.0f, 1.0f);
        }
    }

    /// <summary>
    /// Trigger collider to end game -> which is basically water plane
    /// </summary>
    /// <param name="other">Trigger collider</param>
    private void OnTriggerEnter(Collider other)
    {
        end = true;
    }

    /// <summary>
    /// Main game logic goes here
    /// </summary>
    void FixedUpdate()
    { 
        /// If cooldown for propane torch is out, player can use space to fire it
        if (timer <= 0.0f)
        {
            if (Input.GetKey(KeyCode.Space) && GasUses > 0 && end == false)
            {
                timer = timeout;
                GasUses--;
            }
        }
        /// Otherwise heat up the balloon (isobaric heat equation)
        else
        {
            AirTemperatureInKelvins += (PropaneBurnerPower * Time.fixedDeltaTime) / (HotAirVolume * InsideDensity * kHeatPropertyOfSubstanceJoulesToChange);

            if (AirTemperatureInKelvins > 390.0f)
            {
                AirTemperatureInKelvins = 390.0f;
            }
        }

        // Update timers
        timer -= Time.fixedDeltaTime;
        Phase += Time.fixedDeltaTime;

        /// Update altitude (Altitude - is just scaled altitude - I used this to balance game), and obtain density of outside air
        float altitude = transform.position.y * 25.0f;
        OutsideDensity = GetOutsideDensity(altitude);
        Altitude = (altitude - 122.5f) * 4.0f;

        // Calculate density of inside air in envelope, and buoynant force (upwards)
        InsideDensity = kAtmoPressure / (kDryAirJoules * AirTemperatureInKelvins);
        Buoynancy = (OutsideDensity - InsideDensity) * HotAirVolume;
        
        // Update temperature inside, due to energy losses
        float area = 4 * Mathf.PI * Mathf.Pow(7, 2);
        float energyLoss = kNylonLambda * area * (kOutsideTemparetureInKelvin - AirTemperatureInKelvins) * Time.fixedDeltaTime / kThickness;
        float temperatureChange = energyLoss / (HotAirVolume * InsideDensity * kHeatPropertyOfSubstanceJoulesToChange);
        AirTemperatureInKelvins += temperatureChange;

        // Calculate total mass of balloon. 
        float totalMass = Mass + (float)People.CalculateWeight();
        MassTmp = totalMass;

        // Calculate aerodynamic drag in vertical direction
        float drag = 0.5f * kDragCoefficient * OutsideDensity * area * Mathf.Pow(_rb.velocity.y, 2);

        // Calculate total upwards force (Buoynant force - Gravity force)
        Vector3 buoynancyForce = -(Buoynancy - totalMass) * Physics.gravity * Time.fixedDeltaTime;

        if (_rb.velocity.y > 0.0f)
        {
            buoynancyForce.y -= drag;
        }
        else
        {
            buoynancyForce.y += drag;
        }

        BuoynancyForce = buoynancyForce.y;

        // Calculate wind force
        float windSpeed = (GetWind(Altitude, Phase) + 0.01f) * 20.0f;
        float windForce = windSpeed;

        // Calculate aerodynamic drag in horizontal direction
        WindScale = 0.05f * kDragCoefficient * OutsideDensity * area * Mathf.Pow(_rb.velocity.x, 2);

        // Total movement force (is sum of all forces), clamp forces due to errors
        Vector3 movementForce = new Vector3(Mathf.Clamp(windForce - WindScale, -100.0f, 100.0f), 0.0f, 0.0f);
        if (movementForce.magnitude > 100.0f)
        {
            movementForce = new Vector3(0.0f, 0.0f, 0.0f);
        }

        if (buoynancyForce.magnitude > 100.0f)
        {
            buoynancyForce = new Vector3(0.0f, 0.0f, 0.0f);
        }

        // In case of end of the game - don't use physics to move object anymore, keep it where it was
        if (end)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
        // Otherwise apply all the forces
        else
        {
            _rb.mass = totalMass;
            _rb.AddForce(buoynancyForce, ForceMode.Acceleration);
            _rb.AddForce(movementForce, ForceMode.Acceleration);
        }
    }
}
