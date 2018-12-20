using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fire effect source
/// </summary>
public class FireFX : MonoBehaviour
{
    /// <summary>
    /// Basic fire (blaze) particle system game object
    /// </summary>
    public GameObject fireFlame;

    /// <summary>
    /// Smoke particle system game object
    /// </summary>
    public GameObject fireSmoke;

    /// <summary>
    /// Fireflies particle system game object
    /// </summary>
    public GameObject fireFireflies;

    /// <summary>
    /// Basic fire (blaze) particle system
    /// </summary>
    private ParticleSystem flame;

    /// <summary>
    /// Smoke particle system
    /// </summary>
    private ParticleSystem smoke;

    /// <summary>
    /// Fireflies particle system
    /// </summary>
    private ParticleSystem fireflies;

    /// <summary>
    /// Fire timeout (burn just for few seconds)
    /// </summary>
    public float timeout = 3.4f;
    private float timer = 0.0f;

    /// <summary>
    /// Sound of fire
    /// </summary>
    private AudioSource source;

    /// <summary>
    /// How many times can we fire torch?
    /// </summary>
    public int GasUses = 6;

    /// <summary>
    /// Ref to balloon
    /// </summary>
    public Balloon balloon;

    /// <summary>
    /// Initialize
    /// - read particle systems
    /// - disable particle emission for all
    /// </summary>
    void Start ()
    {
        flame = fireFlame.GetComponent<ParticleSystem>();
        smoke = fireSmoke.GetComponent<ParticleSystem>();
        fireflies = fireFireflies.GetComponent<ParticleSystem>();

        source = GetComponent<AudioSource>();

        ParticleSystem.EmissionModule em = flame.emission;
        em.rateOverTime = 0.0f;
        em = smoke.emission;
        em.rateOverTime = 0.0f;
        em = fireflies.emission;
        em.rateOverTime = 0.0f;
    }
	
	/// <summary>
    /// During update we have to handle if user fires torch
    /// </summary>
	void Update ()
    {
		if (timer <= 0.0f)
        {
            // In case of firing torch, set emission rate and play sound
            if (Input.GetKey(KeyCode.Space) && GasUses > 0 && balloon.end == false)
            {
                timer = timeout;

                ParticleSystem.EmissionModule em = flame.emission;
                em.rateOverTime = 20.0f;
                em = smoke.emission;
                em.rateOverTime = 20.0f;
                em = fireflies.emission;
                em.rateOverTime = 20.0f;
                source.PlayOneShot(source.clip);

                GasUses--;
            }
            // Otherwise (once we're finished), disable emission
            else
            {
                ParticleSystem.EmissionModule em = flame.emission;
                em.rateOverTime = 0.0f;
                em = smoke.emission;
                em.rateOverTime = 0.0f;
                em = fireflies.emission;
                em.rateOverTime = 0.0f;
            }
        }

        timer -= Time.deltaTime;
    }
}
