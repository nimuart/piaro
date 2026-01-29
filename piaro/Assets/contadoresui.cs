using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class contadoresui : MonoBehaviour
{
    [Header("Combo Counter (TextMeshPro)")]
    public TextMeshProUGUI comboText;

    [Header("Tecla Register Slots (4 slots) - can be UI Image or GameObject with SpriteRenderer")]
    public GameObject[] teclaSlots = new GameObject[4];

    [Header("Sprites por tecla")]
    public Sprite spriteA;
    public Sprite spriteS;
    public Sprite spriteD;
    public Sprite spriteW;
    public Sprite spriteSpace;

    [Header("Colores")]
    public Color colorPerfect = Color.green;
    public Color colorRegular = Color.yellow;
    public Color colorGoofy = new Color(1f, 0.6f, 0f);
    public Color colorMiss = Color.gray;
    public Color colorIdle = Color.white;

    struct HitInfo { public RitmoManager.TeclaRitmo tecla; public RitmoManager.HitAccuracy acc; }

    private Queue<HitInfo> recent = new Queue<HitInfo>(4);

    void OnEnable()
    {
        RitmoManager.OnComboUpdated += HandleComboUpdated;
        RitmoManager.OnHit += HandleHit;
        RitmoManager.OnComboDetectado += HandleComboDetected;
        RitmoManager.OnComandoDetectado += HandleComandoLegacy;
    }

    void OnDisable()
    {
        RitmoManager.OnComboUpdated -= HandleComboUpdated;
        RitmoManager.OnHit -= HandleHit;
        RitmoManager.OnComboDetectado -= HandleComboDetected;
        RitmoManager.OnComandoDetectado -= HandleComandoLegacy;
    }

    void Start()
    {
        UpdateComboText();
        ClearImages();
    }

    void HandleComboUpdated(int comboCount, float totalMultiplier)
    {
        UpdateComboText(comboCount);
    }

    void UpdateComboText(int value = -1)
    {
        int c = value;
        if (c < 0 && RitmoManager.Instance != null) c = RitmoManager.Instance.comboCount;
        if (comboText != null) comboText.text = c.ToString();
    }

    void HandleHit(RitmoManager.HitAccuracy acc, int beatIndex, RitmoManager.TeclaRitmo tecla)
    {
        // En caso de MISS reseteamos el registro
        if (acc == RitmoManager.HitAccuracy.Miss)
        {
            recent.Clear();
            ClearImages();
            return;
        }

        // Push into recent queue (max 4)
        if (recent.Count >= 4) recent.Dequeue();
        recent.Enqueue(new HitInfo { tecla = tecla, acc = acc });
        RefreshImagesFromQueue();
    }

    void HandleComboDetected(RitmoManager.ComboDef combo)
    {
        // Flash the 4 images to show success
        StartCoroutine(FlashSuccess());
    }

    void HandleComandoLegacy(string cmd)
    {
        if (cmd == "FALLO")
        {
            recent.Clear();
            ClearImages();
            UpdateComboText(0);
        }
    }

    System.Collections.IEnumerator FlashSuccess()
    {
        foreach (var slot in teclaSlots) if (slot != null) SetSlotColor(slot, Color.white);
        yield return new WaitForSeconds(0.35f);
        RefreshImagesFromQueue();
    }

    void RefreshImagesFromQueue()
    {
        HitInfo[] arr = recent.ToArray();
        int start = Mathf.Max(0, 4 - arr.Length);
        // clear all
        for (int i = 0; i < teclaSlots.Length; i++)
        {
            if (teclaSlots[i] != null)
            {
                SetSlotSprite(teclaSlots[i], null, colorIdle);
            }
        }

        for (int i = 0; i < arr.Length; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= teclaSlots.Length) continue;
            var hi = arr[i];
            var slot = teclaSlots[idx];
            if (slot == null) continue;
            SetSlotSprite(slot, SpriteForTecla(hi.tecla), ColorForAcc(hi.acc));
        }
    }

    Color ColorForAcc(RitmoManager.HitAccuracy acc)
    {
        switch (acc)
        {
            case RitmoManager.HitAccuracy.Perfect: return colorPerfect;
            case RitmoManager.HitAccuracy.Regular: return colorRegular;
            case RitmoManager.HitAccuracy.Goofy: return colorGoofy;
            default: return colorMiss;
        }
    }

    Sprite SpriteForTecla(RitmoManager.TeclaRitmo tecla)
    {
        switch (tecla)
        {
            case RitmoManager.TeclaRitmo.A: return spriteA;
            case RitmoManager.TeclaRitmo.S: return spriteS;
            case RitmoManager.TeclaRitmo.D: return spriteD;
            case RitmoManager.TeclaRitmo.W: return spriteW;
            case RitmoManager.TeclaRitmo.Space: return spriteSpace;
            default: return null;
        }
    }

    void ClearImages()
    {
        foreach (var slot in teclaSlots)
        {
            if (slot == null) continue;
            SetSlotSprite(slot, null, colorIdle);
        }
    }

    void SetSlotSprite(GameObject slot, Sprite sprite, Color color)
    {
        if (slot == null) return;
        var ui = slot.GetComponent<UnityEngine.UI.Image>();
        if (ui != null)
        {
            ui.sprite = sprite;
            ui.color = color;
            return;
        }

        var sr = slot.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.color = color;
            return;
        }
    }

    void SetSlotColor(GameObject slot, Color color)
    {
        if (slot == null) return;
        var ui = slot.GetComponent<UnityEngine.UI.Image>();
        if (ui != null) { ui.color = color; return; }
        var sr = slot.GetComponent<SpriteRenderer>();
        if (sr != null) { sr.color = color; return; }
    }
}
