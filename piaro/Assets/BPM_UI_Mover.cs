using UnityEngine;
using UnityEngine.UI;

public class BPM_UI_Mover : MonoBehaviour
{
    [Header("BPM Settings")]
    public float bpm = 120f;
    public int beatsPerBar = 4; // 4/4

    [Header("Movement")]
    public float distancePerBeat = 100f; // px por beat
    public Vector2 moveDirection = Vector2.left;

    [Header("Prefabs")]
    public RectTransform prefabA;
    public RectTransform prefabB;

    [Header("Spawn")]
    public RectTransform parent;
    public float spawnX = 600f;
    public float despawnX = -600f;

    private float secondsPerBeat;
    private float speed;
    private bool spawnToggle = false;

    void Start()
    {
        Recalculate();

        // Spawnea los primeros 4 beats (1 compás)
        for (int i = 0; i < beatsPerBar; i++)
        {
            SpawnElement(i * distancePerBeat);
        }
    }

    void Update()
    {
        foreach (RectTransform child in parent)
        {
            child.anchoredPosition += moveDirection * speed * Time.deltaTime;

            if (child.anchoredPosition.x <= despawnX)
            {
                Destroy(child.gameObject);
                SpawnElement((beatsPerBar - 1) * distancePerBeat);
            }
        }
    }

    void Recalculate()
    {
        secondsPerBeat = 60f / bpm;
        speed = distancePerBeat / secondsPerBeat;
    }

    void SpawnElement(float offset)
    {
        RectTransform prefab = spawnToggle ? prefabA : prefabB;
        spawnToggle = !spawnToggle;

        RectTransform instance = Instantiate(prefab, parent);
        instance.anchoredPosition = new Vector2(spawnX + offset, 0f);
    }

    // 🔁 Llamar si cambias BPM en runtime
    public void SetBPM(float newBpm)
    {
        bpm = newBpm;
        Recalculate();
    }
}
