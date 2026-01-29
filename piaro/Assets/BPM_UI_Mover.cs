using UnityEngine;

public class BPM_UI_PatternMover : MonoBehaviour
{
    public enum BeatAction { A, B, Rest, RandomAB, Double }

    [Header("Tempo")]
    public float bpm = 120f;
    public int beatsPerBar = 4; // 4/4
    public bool alignToBar = true; // reinicia patrón al inicio de cada compás

    [Header("Pattern (se repite en loop)")]
    public BeatAction[] pattern = new BeatAction[]
    {
        BeatAction.A, BeatAction.Rest, BeatAction.B, BeatAction.Rest,
        BeatAction.A, BeatAction.B,   BeatAction.Rest, BeatAction.Rest
    };

    [Header("Random Mode")]
    public bool useRandomMode = false;
    [Range(0f, 1f)] public float restChance = 0.25f;
    [Range(0f, 1f)] public float doubleChance = 0.10f;
    [Range(0f, 1f)] public float forceAccentOnBeat1Chance = 0.60f; // beat 1 del compás

    [Header("Movement")]
    public float distancePerBeat = 100f; // px por beat
    public Vector2 moveDirection = Vector2.left;

    [Header("Prefabs")]
    public RectTransform prefabA;
    public RectTransform prefabB;

    [Header("Spawn Area")]
    public RectTransform parent;
    public float spawnX = 600f;
    public float despawnX = -600f;
    public float laneY = 0f; // si quieres moverlo en Y

    float secondsPerBeat;
    float speed;

    float beatTimer = 0f;
    int beatCountGlobal = 0;    // cuenta beats desde start
    int patternIndex = 0;

    void Start()
    {
        Recalculate();

        // Pre-fill visual (opcional): spawnea 1 compás para que se vea de inmediato
        for (int i = 0; i < beatsPerBar; i++)
        {
            DoBeatAction(GetActionForBeat(i), spawnOffsetBeats: i);
        }
    }

    void Update()
    {
        // 1) mover todos los hijos a velocidad constante
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            RectTransform child = (RectTransform)parent.GetChild(i);
            child.anchoredPosition += moveDirection * speed * Time.deltaTime;

            if (child.anchoredPosition.x <= despawnX)
            {
                Destroy(child.gameObject);
            }
        }

        // 2) reloj por BPM: ejecuta acciones exacto cada beat
        beatTimer += Time.deltaTime;
        while (beatTimer >= secondsPerBeat)
        {
            beatTimer -= secondsPerBeat;
            OnBeat();
        }
    }

    void OnBeat()
    {
        int beatInBar = beatCountGlobal % beatsPerBar; // 0..3 en 4/4

        // Si quieres que cada compás reinicie el patrón
        if (alignToBar && beatInBar == 0)
            patternIndex = 0;

        BeatAction action = GetActionForBeat(beatInBar);
        DoBeatAction(action, spawnOffsetBeats: (beatsPerBar - 1)); 
        // spawnOffsetBeats = "más adelante" para mantener el flujo visual continuo

        beatCountGlobal++;
    }

    BeatAction GetActionForBeat(int beatInBar)
    {
        if (useRandomMode)
        {
            // Opcional: acento en beat 1 del compás
            if (beatInBar == 0 && Random.value < forceAccentOnBeat1Chance)
                return (Random.value < 0.5f) ? BeatAction.A : BeatAction.B;

            // Silencio
            if (Random.value < restChance) return BeatAction.Rest;

            // Doble (fill)
            if (Random.value < doubleChance) return BeatAction.Double;

            // Normal random A/B
            return BeatAction.RandomAB;
        }

        // Pattern loop
        if (pattern == null || pattern.Length == 0) return BeatAction.RandomAB;

        BeatAction a = pattern[patternIndex % pattern.Length];
        patternIndex++;
        return a;
    }

    void DoBeatAction(BeatAction action, int spawnOffsetBeats)
    {
        switch (action)
        {
            case BeatAction.Rest:
                return;

            case BeatAction.A:
                Spawn(prefabA, spawnOffsetBeats);
                return;

            case BeatAction.B:
                Spawn(prefabB, spawnOffsetBeats);
                return;

            case BeatAction.RandomAB:
                Spawn((Random.value < 0.5f) ? prefabA : prefabB, spawnOffsetBeats);
                return;

            case BeatAction.Double:
                Spawn((Random.value < 0.5f) ? prefabA : prefabB, spawnOffsetBeats);
                // segundo hit un poquito después (sin romper el BPM, solo “decoración”)
                Spawn((Random.value < 0.5f) ? prefabA : prefabB, spawnOffsetBeats, extraX: distancePerBeat * 0.25f);
                return;
        }
    }

    void Spawn(RectTransform prefab, int offsetBeats, float extraX = 0f)
    {
        if (!prefab || !parent) return;

        RectTransform instance = Instantiate(prefab, parent);
        instance.anchoredPosition = new Vector2(spawnX + offsetBeats * distancePerBeat + extraX, laneY);
    }

    void Recalculate()
    {
        secondsPerBeat = 60f / Mathf.Max(1f, bpm);
        speed = distancePerBeat / secondsPerBeat;
    }

    public void SetBPM(float newBpm)
    {
        bpm = newBpm;
        Recalculate();
    }
}
