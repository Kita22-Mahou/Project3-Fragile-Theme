using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private enum JumpState
    {
        OnGround,
        Aiming,
        InAir,
        AirAiming
    }

    private JumpState jumpState = JumpState.OnGround;

    void Start()
    {
        
    }


    void Update()
    {
        
    }
}
