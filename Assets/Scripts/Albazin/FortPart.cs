using UnityEngine;

public sealed class FortPart : MonoBehaviour
{
    public int capacity = 200;
    [HideInInspector] public float health;
    public bool assaultTarget;
    SpriteRenderer art;
    Color original;
    Collider2D[] barriers;
    void Awake()
    {
        art = GetComponent<SpriteRenderer>();
        original = art.color;
        barriers = GetComponents<Collider2D>();
        SetHealth(capacity);
    }
    public void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0, capacity);
        art.enabled = health > 0;
        art.color = Color.Lerp(new Color(.56f,.37f,.26f), original, health / capacity);
        foreach (var box in barriers) box.enabled = health > 0;
    }
}
