using UnityEngine;

public class Proyectil : MonoBehaviour
{
    [Header("Stats")]
    public float damage = 20f;
    public float speed = 25f;
    public float lifetime = 6f;
    public bool instantKill = true; // si true, destruye enemigos al impactar
    public bool destroyOnHit = true;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        // si no hay velocidad ya asignada, aplicar velocidad adelante
        if (rb != null && rb.velocity.sqrMagnitude < 0.01f)
        {
            rb.velocity = transform.forward * speed;
        }
        Destroy(gameObject, lifetime);
    }

    // API para asignar daño desde otros scripts
    public void SetDamage(float d)
    {
        damage = d;
    }

    void OnCollisionEnter(Collision col)
    {
        HandleHit(col.collider);
    }

    void OnTriggerEnter(Collider col)
    {
        HandleHit(col);
    }

    void HandleHit(Collider col)
    {
        if (col == null) return;

        // Intentar pasar daño por SendMessage a varias convenciones
        col.gameObject.SendMessage("ApplyDamage", damage, SendMessageOptions.DontRequireReceiver);
        col.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        col.gameObject.SendMessage("ReceiveDamage", damage, SendMessageOptions.DontRequireReceiver);

        // Si el objeto está taggeado como Enemy o instantKill está activado, destruirlo
        if (instantKill || col.CompareTag("Enemy"))
        {
            // Intentamos una destrucción segura: si existe un componente Health, preferimos llamarlo
            var health = col.GetComponent<MonoBehaviour>();
            if (col.CompareTag("Enemy"))
            {
                Destroy(col.gameObject);
            }
        }

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }
}
