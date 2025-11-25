using Fusion;
using UnityEngine;

public class ExplosionCleanup : NetworkBehaviour
{
    [SerializeField] private float lifeTime = 2.0f;

    [Networked] private TickTimer LifeTimer { get; set; }

    public override void Spawned()
    {
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
    }

    public override void FixedUpdateNetwork()
    {
        if (LifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }
}
