using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TempoUI : MonoBehaviour
{
    [Header("Frames de 4 pasos (asignar en inspector)")]
    public Image[] beatFrames = new Image[4];
    public float pulseScale = 1.4f;
    public float pulseTime = 0.12f;

    [Header("Colores por precisión")]
    public Color perfectColor = Color.green;
    public Color regularColor = Color.yellow;
    public Color goofyColor = new Color(1f, 0.6f, 0f);
    public Color missColor = Color.red;
    public Color idleColor = Color.white;

    void OnEnable()
    {
        RitmoManager.OnBeat += HandleBeat;
        RitmoManager.OnHit += HandleHit;
    }

    void OnDisable()
    {
        RitmoManager.OnBeat -= HandleBeat;
        RitmoManager.OnHit -= HandleHit;
    }

    void Start()
    {
        foreach (var img in beatFrames)
            if (img != null) img.color = idleColor;
    }

    void HandleBeat(int beatIndex)
    {
        if (beatIndex < 0 || beatIndex >= beatFrames.Length) return;
        if (beatFrames[beatIndex] == null) return;
        StopCoroutine("PulseCoroutine");
        StartCoroutine(PulseCoroutine(beatFrames[beatIndex]));
    }

    IEnumerator PulseCoroutine(Image img)
    {
        Transform t = img.transform;
        Vector3 baseScale = t.localScale;
        Vector3 target = baseScale * pulseScale;
        float t0 = 0f;
        while (t0 < pulseTime)
        {
            t.localScale = Vector3.Lerp(baseScale, target, t0 / pulseTime);
            t0 += Time.deltaTime;
            yield return null;
        }
        t.localScale = baseScale;
    }

    void HandleHit(RitmoManager.HitAccuracy acc, int beatIndex)
    {
        if (beatIndex < 0 || beatIndex >= beatFrames.Length) return;
        var img = beatFrames[beatIndex];
        if (img == null) return;

        Color c = idleColor;
        switch (acc)
        {
            case RitmoManager.HitAccuracy.Perfect: c = perfectColor; break;
            case RitmoManager.HitAccuracy.Regular: c = regularColor; break;
            case RitmoManager.HitAccuracy.Goofy: c = goofyColor; break;
            case RitmoManager.HitAccuracy.Miss: c = missColor; break;
        }

        StopCoroutine("FlashColor");
        StartCoroutine(FlashColor(img, c, 0.45f));
    }

    IEnumerator FlashColor(Image img, Color c, float duration)
    {
        Color orig = img.color;
        img.color = c;
        float t = 0f;
        while (t < duration)
        {
            img.color = Color.Lerp(c, orig, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        img.color = orig;
    }
}
