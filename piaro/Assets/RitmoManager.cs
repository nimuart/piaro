using UnityEngine;
using FMODUnity;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;

public class RitmoManager : MonoBehaviour
{
    public static RitmoManager Instance;
    public enum TeclaRitmo { A, S, D, W, Space }

    public enum TipoAccion
    {
        Ninguna,
        MoverAdelante,
        MoverAtras,
        MoverFast,
        Atacar,
        AtacarAereo,
        Defensa,
        Salto,
        SaltoAtaque,
        Especial,
        CambiarArma
    }

    [Serializable]
    public class ComboDef
    {
        [Header("Identidad")]
        public string id;                // "MARCHA", "ATAQUE", etc
        public string displayName;       // texto bonito opcional

        [Header("Secuencia (4 pasos; el 4to debe ser Space)")]
        public TeclaRitmo k1;
        public TeclaRitmo k2;
        public TeclaRitmo k3;
        public TeclaRitmo k4 = TeclaRitmo.Space;

        [Header("Qué hace")]
        public TipoAccion accion = TipoAccion.Ninguna;

        [Header("Opcional (para futuro: anim/prefab)")]
        public string animacion;         // nombre de trigger o state
        public GameObject prefabCambio;  // si luego quieres swap/spawn
        [Header("Efectos del combo")]
        public int comboGain = 1;            // cuánto incrementa el contador de combos
        public float bonusDamage = 0f;       // daño extra directo (suma al multiplicador si quieres)
        public bool spawnProjectile = false; // si debe spawnear un proyectil al ejecutarse
        public GameObject projectilePrefab;  // opcional: prefab del proyectil para este combo
        public float projectileSpeed = 8f;   // velocidad con la que lanzar el proyectil
        [Header("Cambio de arma")]
        public int weaponIndex = -1; // -1 no cambia arma
    }

    [Header("FMOD Setup")]
    public EventReference musicaBase;
    [Header("Sonidos de hit por precisión")]
    public EventReference hitGreen;  // perfecto
    public EventReference hitYellow; // regular
    public EventReference hitRed;    // goofy / miss
    private FMOD.Studio.EventInstance instanciaMusica;

    [Header("Tolerancias (Segundos)")]
    public float tolPerfect = 0.05f;
    public float tolRegular = 0.12f;
    public float tolGoofy = 0.20f;

    [Header("Combos (Editables en Inspector)")]
    public List<ComboDef> combos = new List<ComboDef>();

    [Header("Sistema de Combo")]
    public int comboCount = 0;
    public int comboMax = 99;
    public float damagePerCombo = 0.1f; // cada punto de combo añade este multiplicador
    public GameObject defaultProjectilePrefab; // prefab global si el combo no define uno

    // Notifica (comboCount, totalMultiplier)
    public static event Action<int, float> OnComboUpdated;

    [Header("Estado del Combo (debug)")]
    public List<TeclaRitmo> secuenciaActual = new List<TeclaRitmo>();

    // Evento nuevo (recomendado)
    public static event Action<ComboDef> OnComboDetectado;

    // Evento viejo (por si aún lo usas en otros scripts)
    public static event Action<string> OnComandoDetectado;

    public enum HitAccuracy { Perfect, Regular, Goofy, Miss }

    // Beat tick: pasa el índice del beat (0..3)
    public static event Action<int> OnBeat;

    // Notifica resultado de cada pulsación: accuracy + beatIndex + tecla
    public static event Action<HitAccuracy, int, TeclaRitmo> OnHit;

    private double tiempoUltimoPulso;
    private int pulsoActual = 0;
    private bool yaPresionoEnEstePulso = false;

    class TimelineInfo { public int beat = 0; }
    TimelineInfo timelineInfo;
    FMOD.Studio.EVENT_CALLBACK beatCallback;
    GCHandle timelineHandle;

    void Awake()
    {
        Instance = this;
        // Si está vacío, lo prellenamos una sola vez.
        // (Así no te lo dejo “vacío” y puedes editarlo en inspector después.)
        if (combos == null) combos = new List<ComboDef>();
        if (combos.Count == 0)
            CargarCombosDefault();
    }

    void Start()
    {
        timelineInfo = new TimelineInfo();
        beatCallback = new FMOD.Studio.EVENT_CALLBACK(BeatEventCallback);

        instanciaMusica = RuntimeManager.CreateInstance(musicaBase);
        timelineHandle = GCHandle.Alloc(timelineInfo);
        instanciaMusica.setUserData(GCHandle.ToIntPtr(timelineHandle));
        instanciaMusica.setCallback(beatCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
        instanciaMusica.start();
    }

    void Update()
    {
        if (pulsoActual != timelineInfo.beat)
        {
            pulsoActual = timelineInfo.beat;
            tiempoUltimoPulso = Time.timeAsDouble;
            yaPresionoEnEstePulso = false;
            OnBeat?.Invoke(pulsoActual % 4);
        }

        // 3 teclas + remate Space/click
        if (secuenciaActual.Count < 3)
        {
            if (Input.GetKeyDown(KeyCode.A)) ProcesarTambor(TeclaRitmo.A);
            else if (Input.GetKeyDown(KeyCode.S)) ProcesarTambor(TeclaRitmo.S);
            else if (Input.GetKeyDown(KeyCode.D)) ProcesarTambor(TeclaRitmo.D);
            else if (Input.GetKeyDown(KeyCode.W)) ProcesarTambor(TeclaRitmo.W);
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) FallarCombo("Remate antes de tiempo");
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) ProcesarTambor(TeclaRitmo.Space);
            else if (Input.anyKeyDown) FallarCombo("Debías usar Espacio");
        }
    }

    void ProcesarTambor(TeclaRitmo tecla)
    {
        if (yaPresionoEnEstePulso) return;

        double tiempoAhora = Time.timeAsDouble;
        double diferencia = Math.Abs(tiempoAhora - tiempoUltimoPulso);
        yaPresionoEnEstePulso = true;

        HitAccuracy acc;
        if (diferencia <= tolPerfect) acc = HitAccuracy.Perfect;
        else if (diferencia <= tolRegular) acc = HitAccuracy.Regular;
        else if (diferencia <= tolGoofy) acc = HitAccuracy.Goofy;
        else acc = HitAccuracy.Miss;

        int beatIndex = pulsoActual % 4;
        OnHit?.Invoke(acc, beatIndex, tecla);

        // Reproducir sonido por precisión (si hay asignado)
        PlayHitSound(acc);

        if (acc != HitAccuracy.Miss)
        {
            secuenciaActual.Add(tecla);
            Debug.Log($"<color=cyan>{tecla}</color> ({secuenciaActual.Count}/4) [{acc}]");

            if (secuenciaActual.Count == 4)
                VerificarCombo();
        }
        else
        {
            FallarCombo("Fuera de tiempo");
        }
    }

    void PlayHitSound(HitAccuracy acc)
    {
        try
        {
            if (acc == HitAccuracy.Perfect)
            {
                RuntimeManager.PlayOneShot(hitGreen);
            }
            else if (acc == HitAccuracy.Regular)
            {
                RuntimeManager.PlayOneShot(hitYellow);
            }
            else // Goofy or Miss
            {
                RuntimeManager.PlayOneShot(hitRed);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("FMOD PlayHitSound error: " + e.Message);
        }
    }

    void FallarCombo(string razon)
    {
        Debug.Log($"<color=red>MISS: {razon}</color>");
        secuenciaActual.Clear();
        comboCount = 0;
        OnComboUpdated?.Invoke(comboCount, 1f);
        OnComandoDetectado?.Invoke("FALLO");

        // Notify a miss hit on current beat as well (no tecla)
        OnHit?.Invoke(HitAccuracy.Miss, pulsoActual % 4, TeclaRitmo.Space);
    }

    void VerificarCombo()
    {
        ComboDef encontrado = BuscarCombo(secuenciaActual);

        if (encontrado != null)
        {
            Debug.Log($"<color=magenta>COMBO: {encontrado.id} ({encontrado.accion})</color>");
            OnComboDetectado?.Invoke(encontrado);
            OnComandoDetectado?.Invoke(encontrado.id); // compat

            // Aplicar efectos de combo: incrementar contador y notificar multiplier
            comboCount = Mathf.Clamp(comboCount + encontrado.comboGain, 0, comboMax);
            float totalMultiplier = 1f + comboCount * damagePerCombo + encontrado.bonusDamage;
            OnComboUpdated?.Invoke(comboCount, totalMultiplier);

            // Si el combo implica disparar, instanciamos 1 proyectil (si hay prefab)
            if (encontrado.spawnProjectile || encontrado.projectilePrefab != null)
            {
                GameObject prefabToUse = encontrado.projectilePrefab != null ? encontrado.projectilePrefab : defaultProjectilePrefab;
                if (prefabToUse != null)
                {
                    Vector3 spawnPos = transform.position + transform.forward + Vector3.up * 1.0f;
                    GameObject p = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
                    Rigidbody rb = p.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = transform.forward * encontrado.projectileSpeed;
                    }
                }
                else
                {
                    Debug.LogWarning("No projectile prefab assigned (combo nor default).");
                }
            }
        }
        else
        {
            Debug.Log("<color=gray>Esa combinación no existe.</color>");
            OnComandoDetectado?.Invoke("FALLO");
        }

        secuenciaActual.Clear();
    }

    ComboDef BuscarCombo(List<TeclaRitmo> seq)
    {
        if (seq == null || seq.Count != 4) return null;

        for (int i = 0; i < combos.Count; i++)
        {
            var c = combos[i];
            if (c.k1 == seq[0] && c.k2 == seq[1] && c.k3 == seq[2] && c.k4 == seq[3])
                return c;
        }
        return null;
    }

    // Acceso conveniente para otros scripts: multiplicador de daño actual
    public float GetCurrentDamageMultiplier()
    {
        return 1f + comboCount * damagePerCombo;
    }

    void OnDestroy()
    {
        instanciaMusica.setCallback(null);
        instanciaMusica.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        instanciaMusica.release();
        if (timelineHandle.IsAllocated) timelineHandle.Free();
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    static FMOD.RESULT BeatEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        IntPtr timelineInfoPtr;
        FMOD.Studio.EventInstance instance = new FMOD.Studio.EventInstance(instancePtr);
        instance.getUserData(out timelineInfoPtr);

        if (timelineInfoPtr != IntPtr.Zero)
        {
            GCHandle handle = GCHandle.FromIntPtr(timelineInfoPtr);
            TimelineInfo info = (TimelineInfo)handle.Target;
            info.beat++;
        }
        return FMOD.RESULT.OK;
    }

    // --- Defaults: usa tus ideas + deja el resto editable ---
    void CargarCombosDefault()
    {
        combos.Clear();

        // Movimientos base (ejemplos que pediste)
        Add("MOVE_WWW", "Mover (WWW)", TeclaRitmo.W, TeclaRitmo.W, TeclaRitmo.W, TipoAccion.MoverAdelante);
        // Weapon changes via A/A/A, S/S/S, D/D/D
        AddWeapon("WEAP_AAA", "Arma A (AAA)", TeclaRitmo.A, TeclaRitmo.A, TeclaRitmo.A, 0);
        AddWeapon("WEAP_SSS", "Arma S (SSS)", TeclaRitmo.S, TeclaRitmo.S, TeclaRitmo.S, 1);
        AddWeapon("WEAP_DDD", "Arma D (DDD)", TeclaRitmo.D, TeclaRitmo.D, TeclaRitmo.D, 2);
        // Keep a DDD legacy move? removed to prioritize weapon selection

        // Ataques / variaciones (tú mencionaste estos patrones)
        Add("ATK_DSA", "Ataque (DSA)", TeclaRitmo.D, TeclaRitmo.S, TeclaRitmo.A, TipoAccion.Atacar);
        Add("ATK_DWA", "Ataque Aéreo (DWA)", TeclaRitmo.D, TeclaRitmo.W, TeclaRitmo.A, TipoAccion.AtacarAereo);

        // Defensas (ejemplos)
        Add("DEF_SWD", "Defensa (SWD)", TeclaRitmo.S, TeclaRitmo.W, TeclaRitmo.D, TipoAccion.Defensa);
        Add("DEF_SAD", "Defensa (SAD)", TeclaRitmo.S, TeclaRitmo.A, TeclaRitmo.D, TipoAccion.Defensa);
        Add("DEF_SDA", "Defensa (SDA)", TeclaRitmo.S, TeclaRitmo.D, TeclaRitmo.A, TipoAccion.Defensa);

        // Movilidad fast
        Add("FAST_WDD", "Mover Fast (WDD)", TeclaRitmo.W, TeclaRitmo.D, TeclaRitmo.D, TipoAccion.MoverFast);
        Add("FAST_WSS", "Mover Fast (WSS)", TeclaRitmo.W, TeclaRitmo.S, TeclaRitmo.S, TipoAccion.MoverFast);

        // Saltos / salto+ataque / especial (ejemplos base, cámbialos si quieres)
        Add("JUMP_WWA", "Salto (WWA)", TeclaRitmo.W, TeclaRitmo.W, TeclaRitmo.A, TipoAccion.Salto);
        Add("JUMPATK_WDA", "Salto+Ataque (WDA)", TeclaRitmo.W, TeclaRitmo.D, TeclaRitmo.A, TipoAccion.SaltoAtaque);
        Add("SPEC_ADW", "Especial (ADW)", TeclaRitmo.A, TeclaRitmo.D, TeclaRitmo.W, TipoAccion.Especial);

        // Si quieres también mantener tus combos antiguos estilo Patapon, agrégalos aquí:
        // Add("MARCHA_PataPataPataPon", "MARCHA", A A A Space, etc...)
    }

    void Add(string id, string displayName, TeclaRitmo a, TeclaRitmo b, TeclaRitmo c, TipoAccion accion)
    {
        combos.Add(new ComboDef
        {
            id = id,
            displayName = displayName,
            k1 = a,
            k2 = b,
            k3 = c,
            k4 = TeclaRitmo.Space,
            accion = accion
        });
    }

    void AddWeapon(string id, string displayName, TeclaRitmo a, TeclaRitmo b, TeclaRitmo c, int weaponIndex)
    {
        combos.Add(new ComboDef
        {
            id = id,
            displayName = displayName,
            k1 = a,
            k2 = b,
            k3 = c,
            k4 = TeclaRitmo.Space,
            accion = TipoAccion.CambiarArma,
            weaponIndex = weaponIndex
        });
    }
}
