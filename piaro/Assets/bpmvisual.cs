using UnityEngine;
using System.Collections.Generic;

public class bpmvisual : MonoBehaviour
{
    [Header("Prefab (imagen) a usar")]
    public GameObject imagePrefab;

    [Header("Timing")]
    public float bpm = 120f;
    public float beatsBetween = 1f; // cuántos beats entre cada imagen

    [Header("Layout")]
    public int poolSize = 8;
    public float spacing = 2f; // distancia entre imágenes (unidades world)
    public Vector3 startPosition = Vector3.zero; // posición del primero (más a la derecha)
    public float leftWrapOffset = -10f; // límite izquierdo relativo a startPosition.x donde reaparecen

    private List<GameObject> pool = new List<GameObject>();
    private float speed = 0f; // velocidad hacia la izquierda (unidades/s)

    void Start()
    {
        if (imagePrefab == null)
        {
            Debug.LogWarning("bpmvisual: imagePrefab no asignado.");
            return;
        }

        if (bpm <= 0f) bpm = 120f;
        if (beatsBetween <= 0f) beatsBetween = 1f;

        float spawnInterval = beatsBetween * (60f / bpm); // segundos entre imágenes
        if (spawnInterval <= 0f) spawnInterval = 0.5f;
        // speed tal que en spawnInterval las imágenes se separen spacing unidades
        speed = spacing / spawnInterval;

        // Crear pool inicial alineada horizontalmente a la derecha
        for (int i = 0; i < poolSize; i++)
        {
            Vector3 pos = startPosition + Vector3.right * (i * spacing);
            GameObject go = Instantiate(imagePrefab, pos, Quaternion.identity, transform);
            pool.Add(go);
        }
    }

    void Update()
    {
        if (pool.Count == 0) return;

        // Mover todas hacia la izquierda
        for (int i = 0; i < pool.Count; i++)
        {
            var g = pool[i];
            if (g == null) continue;
            g.transform.position += Vector3.left * speed * Time.deltaTime;
        }

        // Envolver las que pasen el límite izquierdo hacia la derecha del grupo
        float leftBound = startPosition.x + leftWrapOffset;
        float rightmost = float.MinValue;
        foreach (var g in pool)
        {
            if (g == null) continue;
            if (g.transform.position.x > rightmost) rightmost = g.transform.position.x;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            var g = pool[i];
            if (g == null) continue;
            if (g.transform.position.x < leftBound)
            {
                // Reposicionar a la derecha del más derecho
                g.transform.position = new Vector3(rightmost + spacing, startPosition.y, startPosition.z);
                rightmost = g.transform.position.x;
            }
        }
    }

    void OnValidate()
    {
        if (bpm <= 0f) bpm = 120f;
        if (beatsBetween <= 0f) beatsBetween = 1f;
        float spawnInterval = beatsBetween * (60f / bpm);
        if (spawnInterval > 0f) speed = spacing / spawnInterval;
    }
}
