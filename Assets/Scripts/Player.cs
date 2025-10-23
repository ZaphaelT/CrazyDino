using Fusion;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private NetworkCharacterController _cc;

    // Upewniamy siê, ¿e komponent jest pobrany przy spawn (bezpieczniej ni¿ Awake jeœli coœ modyfikujesz)
    public override void Spawned()
    {
        _cc = GetComponent<NetworkCharacterController>();

        Debug.Log(
            $"Player Spawned: ObjectId={Object.Id} HasStateAuthority={Object.HasStateAuthority} " +
            $"HasInputAuthority={Object.HasInputAuthority} InputAuthority={Object.InputAuthority} " +
            $"RunnerLocalPlayer={Runner.LocalPlayer}"
        );
    }

    public override void FixedUpdateNetwork()
    {
        // Ruch powinien aplikowaæ tylko instancja z state authority
        if (!Object.HasStateAuthority)
            return;

        if (GetInput(out NetworkInputData data))
        {
            // zabezpieczenie przed normalizacj¹ wektora zero
            if (data.direction.sqrMagnitude > 0f)
            {
                data.direction.Normalize();
                _cc.Move(5f * data.direction * Runner.DeltaTime);
            }
        }
    }
}