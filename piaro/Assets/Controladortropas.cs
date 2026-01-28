using UnityEngine;
using System.Collections;

public class ControladorTropas : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float distanciaPaso = 2f;
    public float velocidadMovimiento = 5f;

    private Vector3 posicionObjetivo;
    private bool estaMoviendose = false;

    void Start()
    {
        // La posición inicial es donde lo pongas en el editor
        posicionObjetivo = transform.position;
    }

    void OnEnable()
    {
        // Nos suscribimos al evento del RitmoManager
        RitmoManager.OnComandoDetectado += ManejarComando;
    }

    void OnDisable()
    {
        // Nos desuscribimos para evitar errores de memoria
        RitmoManager.OnComandoDetectado -= ManejarComando;
    }

    // Este método se ejecuta automáticamente cuando el RitmoManager detecta un combo
    void ManejarComando(string comando)
    {
        switch (comando)
        {
            case "MARCHA_PataPataPataPon":
                MoverHaciaAdelante();
                break;
            
            case "HUIDA_DonDonDonPon":
                MoverHaciaAtras();
                break;

            case "FALLO":
                Debug.Log("Las tropas se tambalean por perder el ritmo...");
                // Aquí podrías poner una animación de tropiezo
                break;
        }
    }

    void MoverHaciaAdelante()
    {
        posicionObjetivo += Vector3.right * distanciaPaso;
        StartCoroutine(SuavizarMovimiento());
    }

    void MoverHaciaAtras()
    {
        posicionObjetivo += Vector3.left * distanciaPaso;
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