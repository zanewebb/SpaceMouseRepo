// Compile-time stubs only — see SpaceMouseRepo.Stubs.csproj for why.
// At runtime these are replaced by the real types in R.E.P.O.'s Assembly-CSharp.dll.

using UnityEngine;

public class PhysGrabber : MonoBehaviour
{
    public bool isLocal;
    public bool grabbed;
    public Quaternion physRotation;
    public Quaternion nextPhysRotation;
    public PhysGrabObject grabbedPhysGrabObject;
    public Rigidbody grabbedObject;
    public Camera playerCamera;
    public Transform physGrabPoint;

    public void Update() { }
    public void FixedUpdate() { }
}

public class PhysGrabObject : MonoBehaviour
{
}
