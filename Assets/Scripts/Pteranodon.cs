using Fusion;
using UnityEngine;

public class Pteranodon : NetworkBehaviour
{
    /*
    private Animator _animator;

    [Networked, OnChangedRender(nameof(OnIsWalkingChanged))]
    private bool IsWalking { get; set; }

    [Networked, OnChangedRender(nameof(OnIsDeadChanged))]
    private bool IsDead { get; set; }

    [Networked] 
    private int Hp { get; set; }

    [SerializeField]
    private int maxHp = 100;

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        _animator = GetComponent<Animator>();
        if (Object.HasStateAuthority)
            Hp = maxHp;
    }

    void Update()
    {
        // bool shouldWalk = ...;
        // if (Object.HasStateAuthority && IsWalking != shouldWalk)
        //     IsWalking = shouldWalk;
    }

    public void TakeDamage(int amount)
    {
        if (!Object.HasStateAuthority || IsDead)
            return;

        Hp -= amount;
        RPC_PlayTakeDamage();

        if (Hp <= 0)
        {
            Hp = 0;
            IsDead = true;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTakeDamage()
    {
        if (_animator != null)
            _animator.SetTrigger("TakeDamage");
    }

    private void OnIsWalkingChanged()
    {
        if (_animator != null)
            _animator.SetBool("isWalking", IsWalking);
    }

    private void OnIsDeadChanged()
    {
        if (_animator != null)
            _animator.SetTrigger("Death");
    }
    */
}
