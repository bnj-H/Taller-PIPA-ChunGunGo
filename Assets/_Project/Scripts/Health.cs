using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private float killY = -6.5f;
    [SerializeField] private int current;

    public int Current => current;
    public int Max => maxHealth;
    public bool FellOff { get; private set; }
    public event Action<Health> Died;

    private bool dead;

    private void Awake()
    {
        current = maxHealth;
    }

    private void Update()
    {
        if (!dead && transform.position.y < killY)
        {
            FellOff = true;
            Die();
        }
    }

    public bool TakeDamage(int amount)
    {
        if (dead) return false;

        current -= amount;
        if (current <= 0)
        {
            Die();
            return false;
        }
        return true;
    }

    private void Die()
    {
        if (dead) return;

        dead = true;
        Died?.Invoke(this);
        Destroy(gameObject);
    }
}