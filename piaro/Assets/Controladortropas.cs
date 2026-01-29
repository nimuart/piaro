using UnityEngine;
using System.Collections;

public class ControladorTropas : MonoBehaviour
{
    [Header("Movimiento")]
    public float distanciaPaso = 2f;
    public float distanciaFast = 4f;
    public float velocidadMovimiento = 5f;

    private Vector3 posicionObjetivo;
    private bool estaMoviendose = false;

    void Start()
    {
        posicionObjetivo = transform.position;
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
                // Aquí luego: animación / hitbox / daño
                break;

            case RitmoManager.TipoAccion.AtacarAereo:
                Debug.Log("Tropas: ATAQUE AÉREO!");
                break;

            case RitmoManager.TipoAccion.Defensa:
                Debug.Log("Tropas: DEFENSA!");
                break;

            case RitmoManager.TipoAccion.Salto:
                Debug.Log("Tropas: SALTO!");
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
        posicionObjetivo += dir * distancia;
        StartCoroutine(SuavizarMovimiento());
    }

    IEnumerator SuavizarMovimiento()
    {
        if (estaMoviendose) yield break;
        estaMoviendose = true;

        while (Vector3.Distance(transform.position, posicionObjetivo) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                posicionObjetivo,
                velocidadMovimiento * Time.deltaTime
            );
            yield return null;
        }

        transform.position = posicionObjetivo;
        estaMoviendose = false;
    }
}
