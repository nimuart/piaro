using UnityEngine;
using FMODUnity;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;

public class RitmoManager : MonoBehaviour
{
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
        Especial
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
    }

    [Header("FMOD Setup")]
    public EventReference musicaBase;
    private FMOD.Studio.EventInstance instanciaMusica;

    [Header("Tolerancias (Segundos)")]
    public float tolPerfect = 0.05f;
    public float tolRegular = 0.12f;
    public float tolGoofy = 0.20f;

    [Header("Combos (Editables en Inspector)")]
    public List<ComboDef> combos = new List<ComboDef>();

    [Header("Estado del Combo (debug)")]
    public List<TeclaRitmo> secuenciaActual = new List<TeclaRitmo>();

    // Evento nuevo (recomendado)
    public static event Action<ComboDef> OnComboDetectado;

    // Evento viejo (por si aún lo usas en otros scripts)
    public static event Action<string> OnComandoDetectado;

    private double tiempoUltimoPulso;
    private int pulsoActual = 0;
    private bool yaPresionoEnEstePulso = false;

    class TimelineInfo { public int beat = 0; }
    TimelineInfo timelineInfo;
    FMOD.Studio.EVENT_CALLBACK beatCallback;
    GCHandle timelineHandle;

    void Awake()
    {
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

        if (diferencia <= tolGoofy)
        {
            secuenciaActual.Add(tecla);
            Debug.Log($"<color=cyan>{tecla}</color> ({secuenciaActual.Count}/4)");

            if (secuenciaActual.Count == 4)
                VerificarCombo();
        }
        else
        {
            FallarCombo("Fuera de tiempo");
        }
    }

    void FallarCombo(string razon)
    {
        Debug.Log($"<color=red>MISS: {razon}</color>");
        secuenciaActual.Clear();
        OnComandoDetectado?.Invoke("FALLO");
    }

    void VerificarCombo()
    {
        ComboDef encontrado = BuscarCombo(secuenciaActual);

        if (encontrado != null)
        {
            Debug.Log($"<color=magenta>COMBO: {encontrado.id} ({encontrado.accion})</color>");
            OnComboDetectado?.Invoke(encontrado);
            OnComandoDetectado?.Invoke(encontrado.id); // compat
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
        Add("MOVE_SSS", "Retroceder (SSS)", TeclaRitmo.S, TeclaRitmo.S, TeclaRitmo.S, TipoAccion.MoverAtras);
        Add("MOVE_DDD", "Adelante (DDD)", TeclaRitmo.D, TeclaRitmo.D, TeclaRitmo.D, TipoAccion.MoverAdelante);
        Add("MOVE_AAA", "Atrás (AAA)", TeclaRitmo.A, TeclaRitmo.A, TeclaRitmo.A, TipoAccion.MoverAtras);

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
}
