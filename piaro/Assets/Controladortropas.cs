using UnityEngine;
using System.Collections;

public class ControladorTropas : MonoBehaviour
{
    [Header("Movimiento")]
    public float distanciaPaso = 2f;
    public float distanciaFast = 4f;
    public float velocidadMovimiento = 5f;

    [Header("Movimiento contínuo")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4f;
    public float acceleration = 6f;
    public float deceleration = 8f;

    private Vector3 moveDirection = Vector3.zero;
    private float currentSpeed = 0f;
    private float targetSpeed = 0f;

    [Header("Combate")]
    public float baseAttackDamage = 10f;
    public GameObject projectilePrefab; // prefabricado por defecto para ataques
    public Vector3 projectileSpawnOffset = Vector3.zero;
    public float projectileSpawnHeight = 1.0f;
    public float projectileSpeedFallback = 8f;
    [Header("Armas")]
    public GameObject[] weaponProjectiles = new GameObject[3];
    public Color[] weaponTint = new Color[3];
    public int currentWeapon = 0;
    public float fireRate = 6f; // shots per second
    private float fireCooldown = 0f;

    // legacy positional movement removed in favor of continuous stacked movement

    void Start()
    {
        currentSpeed = 0f;
    }

    void OnEnable()
    {
        RitmoManager.OnComboDetectado += ManejarCombo;
        RitmoManager.OnComandoDetectado += ManejarComandoLegacy; // opcional
    }

    void OnDisable()
    {
        RitmoManager.OnComboDetectado -= ManejarCombo;
        RitmoManager.OnComandoDetectado -= ManejarComandoLegacy;
    }

    // Nuevo: recibe el combo completo con su "accion"
    void ManejarCombo(RitmoManager.ComboDef combo)
    {
        // Weapon change via combo
        if (combo.weaponIndex >= 0)
        {
            int idx = Mathf.Clamp(combo.weaponIndex, 0, weaponProjectiles.Length - 1);
            currentWeapon = idx;
            Debug.Log($"Weapon switched to slot {currentWeapon}");
            return;
        }

        switch (combo.accion)
        {
            case RitmoManager.TipoAccion.MoverAdelante:
                Mover(Vector3.right, distanciaPaso);
                break;

            case RitmoManager.TipoAccion.MoverAtras:
                Mover(Vector3.left, distanciaPaso);
                break;

            case RitmoManager.TipoAccion.MoverFast:
                Mover(Vector3.right, distanciaFast);
                break;

            case RitmoManager.TipoAccion.Atacar:
                Debug.Log("Tropas: ATAQUE!");
                // Animación / hitbox / daño -> spawn 1 proyectil o aplicar daño cuerpo a cuerpo
                {
                    // Determinar prefab a usar: primero el del combo, luego el default del RitmoManager, luego este componente
                    GameObject prefabToUse = combo.projectilePrefab != null ? combo.projectilePrefab :
                        (RitmoManager.Instance != null && RitmoManager.Instance.defaultProjectilePrefab != null ? RitmoManager.Instance.defaultProjectilePrefab : projectilePrefab);

                    // Calcular multiplicador: toma el multipler actual + bonus del combo
                    float multiplier = 1f;
                    if (RitmoManager.Instance != null)
                        multiplier = RitmoManager.Instance.GetCurrentDamageMultiplier();
                    multiplier += combo.bonusDamage;

                    float finalDamage = baseAttackDamage * multiplier;

                    if (prefabToUse != null)
                    {
                        // spawn in front
                        Vector3 spawnPos = transform.position + transform.forward + Vector3.up * projectileSpawnHeight + projectileSpawnOffset;
                        GameObject p = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
                        Rigidbody rb = p.GetComponent<Rigidbody>();
                        float speed = combo.projectileSpeed > 0f ? combo.projectileSpeed : projectileSpeedFallback;
                        if (rb != null) rb.linearVelocity = transform.forward * speed;

                        // Tint projectile if possible
                        var sr = p.GetComponentInChildren<SpriteRenderer>();
                        if (sr != null && weaponTint.Length > 0) sr.color = weaponTint[Mathf.Clamp(currentWeapon,0,weaponTint.Length-1)];

                        // Intentar pasar el daño al proyectil: función SetDamage(float) si existe
                        p.SendMessage("SetDamage", finalDamage, SendMessageOptions.DontRequireReceiver);
                    }
                    else
                    {
                        // Sin prefab: registro y/o daño cuerpo a cuerpo (implementar según tu sistema de objetivo)
                        Debug.Log($"Aplicando ataque cuerpo a cuerpo con daño: {finalDamage}");
                    }
                }
                break;

            case RitmoManager.TipoAccion.AtacarAereo:
                Debug.Log("Tropas: ATAQUE AÉREO!");
                break;

            case RitmoManager.TipoAccion.Defensa:
                Debug.Log("Tropas: DEFENSA!");
                break;

            case RitmoManager.TipoAccion.Salto:
                Debug.Log("Tropas: SALTO!");
                Saltar();
                break;

            case RitmoManager.TipoAccion.SaltoAtaque:
                Debug.Log("Tropas: SALTO + ATAQUE!");
                break;

            case RitmoManager.TipoAccion.Especial:
                Debug.Log("Tropas: ESPECIAL!");
                break;

            default:
                Debug.Log($"Combo detectado ({combo.id}) pero sin acción asignada.");
                break;
        }
    }

    // Legacy: por si aún disparas strings tipo "FALLO"
    void ManejarComandoLegacy(string comando)
    {
        if (comando == "FALLO")
        {
            Debug.Log("Las tropas se tambalean por perder el ritmo...");
        }
    }

    void Mover(Vector3 dir, float distancia)
    {
        moveDirection = dir.normalized;
        // decidir velocidad objetivo según distancia pedido
        if (Mathf.Approximately(distancia, distanciaFast)) targetSpeed = runSpeed;
        else targetSpeed = walkSpeed;
    }

    void Update()
    {
        // cooldown for firing
        if (fireCooldown > 0f) fireCooldown -= Time.deltaTime;

        // ajustar velocidad hacia target
        float accel = targetSpeed > currentSpeed ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        // aplicar movimiento continuo
        if (moveDirection.sqrMagnitude > 0.001f && currentSpeed > 0f)
        {
            transform.position += moveDirection * currentSpeed * Time.deltaTime;
        }

        // Mouse aim + fire
        if (Input.GetMouseButton(0))
        {
            TryFireAtMouse();
        }
    }

    public void Frenar()
    {
        targetSpeed = 0f;
    }

    void Saltar()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (Mathf.Abs(rb.linearVelocity.y) < 0.1f) // simple grounded check
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 6f, rb.linearVelocity.z);
            }
        }
    }

    void TryFireAtMouse()
    {
        if (fireCooldown > 0f) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        // Raycast to plane at unit's y
        Plane plane = new Plane(Vector3.up, transform.position);
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(r, out float enter))
        {
            Vector3 hit = r.GetPoint(enter);
            Vector3 dir = (hit - transform.position).normalized;

            // choose projectile from current weapon, fallback to component prefab
            GameObject prefab = (weaponProjectiles != null && currentWeapon >= 0 && currentWeapon < weaponProjectiles.Length && weaponProjectiles[currentWeapon] != null)
                ? weaponProjectiles[currentWeapon]
                : projectilePrefab;

            if (prefab != null)
            {
                Vector3 spawnPos = transform.position + dir + Vector3.up * projectileSpawnHeight + projectileSpawnOffset;
                GameObject p = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir));
                Rigidbody rb = p.GetComponent<Rigidbody>();
                float speed = projectileSpeedFallback;
                if (rb != null) rb.linearVelocity = dir * speed;
                p.SendMessage("SetDamage", baseAttackDamage * RitmoManager.Instance.GetCurrentDamageMultiplier(), SendMessageOptions.DontRequireReceiver);
                var sr = p.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && weaponTint.Length > 0) sr.color = weaponTint[Mathf.Clamp(currentWeapon,0,weaponTint.Length-1)];
            }

            fireCooldown = 1f / Mathf.Max(0.0001f, fireRate);
        }
    }
}
