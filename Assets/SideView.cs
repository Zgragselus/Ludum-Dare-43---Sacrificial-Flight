using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SideView : MonoBehaviour
{
    /// <summary>
    /// Origin object
    /// </summary>
    public Transform reference;

    /// <summary>
    /// Calculate position that is to the right of the object
    /// - camera shouldn't be centerd to the object, but to the side of it, this gives player 
    ///   better view of the world/scene
    /// </summary>
	void Update()
    {
        this.transform.localPosition = reference.right * 17.0f + new Vector3(0.0f, 8.0f, 0.0f);
	}
}
