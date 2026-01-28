using UnityEngine;
using FMODUnity;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;

public class RitmoManager : MonoBehaviour
{
    [Header("FMOD Setup")]
    public EventReference musicaBase;
    private FMOD.Studio.EventInstance instanciaMusica;

    [Header("Tolerancias (Segundos)")]
    public float tolPerfect = 0.05f;
    public float tolRegular = 0.12f;
    public float tolGoofy = 0.20f; 

    [Header("Estado del Combo")]
    public List<string> secuenciaActual = new List<string>();
    private Dictionary<string, string> bibliotecaCombos = new Dictionary<string, string>();
    public static event Action<string> OnComandoDetectado;

    private double tiempoUltimoPulso;
    private int pulsoActual = 0;
    private bool yaPresionoEnEstePulso = false;

    class TimelineInfo { public int beat = 0; }
    TimelineInfo timelineInfo;
    FMOD.Studio.EVENT_CALLBACK beatCallback;
    GCHandle timelineHandle;

    void Start() {
        InicializarDiccionario();
        timelineInfo = new TimelineInfo();
        beatCallback = new FMOD.Studio.EVENT_CALLBACK(BeatEventCallback);
        instanciaMusica = RuntimeManager.CreateInstance(musicaBase);
        timelineHandle = GCHandle.Alloc(timelineInfo);
        instanciaMusica.setUserData(GCHandle.ToIntPtr(timelineHandle));
        instanciaMusica.setCallback(beatCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
        instanciaMusica.start();
    }

    void InicializarDiccionario() {
        // FORMATO: "Tecla1-Tecla2-Tecla3-Tecla4"
        // NOTA: El 4to siempre debe ser "Space" según tu regla.

        // --- CATEGORÍA A (OFENSIVOS) ---
        bibliotecaCombos.Add("A-A-A-Space", "MARCHA_PataPataPataPon");
        bibliotecaCombos.Add("A-S-A-Space", "ATAQUE_PataChakaPataPon");
        bibliotecaCombos.Add("A-W-A-Space", "SALTO_PataDonPataPon");
        bibliotecaCombos.Add("A-D-A-Space", "CARGA_PataPonPataPon");

        // --- CATEGORÍA S (DEFENSIVOS) ---
        bibliotecaCombos.Add("S-S-S-Space", "DEFENSA_ChakaChakaChakaPon");
        bibliotecaCombos.Add("S-A-S-Space", "CONTRA_ChakaPataChakaPon");
        bibliotecaCombos.Add("S-W-S-Space", "MURO_ChakaDonChakaPon");
        bibliotecaCombos.Add("S-D-S-Space", "ESCUDO_ChakaPonChakaPon");

        // --- CATEGORÍA W (MOVILIDAD) ---
        bibliotecaCombos.Add("W-W-W-Space", "HUIDA_DonDonDonPon");
        bibliotecaCombos.Add("W-A-W-Space", "FLANQUEO_DonPataDonPon");
        bibliotecaCombos.Add("W-S-W-Space", "TACTICA_DonChakaDonPon");
        bibliotecaCombos.Add("W-D-W-Space", "TELEPORT_DonPonDonPon");

        // --- CATEGORÍA D (EXTRAS/MAGIA) ---
        bibliotecaCombos.Add("D-D-D-Space", "ULTRA_PonPonPonPon");
        bibliotecaCombos.Add("D-A-D-Space", "EXPLOSION_PonPataPonPon");
        bibliotecaCombos.Add("D-S-D-Space", "CURA_PonChakaPonPon");
        bibliotecaCombos.Add("D-W-D-Space", "IMPULSO_PonDonPonPon");
    }

    void Update() {
        if (pulsoActual != timelineInfo.beat) {
            pulsoActual = timelineInfo.beat;
            tiempoUltimoPulso = Time.timeAsDouble;
            yaPresionoEnEstePulso = false; 
        }

        // Lógica de Inputs: 3 teclas de dirección + 1 de remate
        if (secuenciaActual.Count < 3) {
            if (Input.GetKeyDown(KeyCode.A)) ProcesarTambor("A");
            else if (Input.GetKeyDown(KeyCode.S)) ProcesarTambor("S");
            else if (Input.GetKeyDown(KeyCode.D)) ProcesarTambor("D"); 
            else if (Input.GetKeyDown(KeyCode.W)) ProcesarTambor("W");
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) FallarCombo("Remate antes de tiempo");
        } 
        else { 
            // Esperando el espacio o click para cerrar el combo
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) ProcesarTambor("Space");
            else if (Input.anyKeyDown) FallarCombo("Debías usar Espacio");
        }
    }

    void ProcesarTambor(string teclaPresionada) {
        if (yaPresionoEnEstePulso) return;
        double tiempoAhora = Time.timeAsDouble;
        double diferencia = System.Math.Abs(tiempoAhora - tiempoUltimoPulso);
        yaPresionoEnEstePulso = true;

        if (diferencia <= tolGoofy) {
            secuenciaActual.Add(teclaPresionada);
            Debug.Log($"<color=cyan>{teclaPresionada}</color> ({secuenciaActual.Count}/4)");
            if (secuenciaActual.Count == 4) VerificarCombo();
        } else {
            FallarCombo("Fuera de tiempo");
        }
    }

    void FallarCombo(string razon) {
        Debug.Log($"<color=red>MISS: {razon}</color>");
        secuenciaActual.Clear();
        OnComandoDetectado?.Invoke("FALLO");
    }

    void VerificarCombo() {
        string comboFinal = string.Join("-", secuenciaActual);
        
        if (bibliotecaCombos.ContainsKey(comboFinal)) {
            string accion = bibliotecaCombos[comboFinal];
            Debug.Log($"<color=magenta>COMANDO: {accion}!</color>");
            OnComandoDetectado?.Invoke(accion);
        } else {
            Debug.Log("<color=gray>Esa combinación no existe.</color>");
            OnComandoDetectado?.Invoke("FALLO");
        }
        secuenciaActual.Clear();
    }

    void OnDestroy() {
        instanciaMusica.setCallback(null);
        instanciaMusica.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        instanciaMusica.release();
        if (timelineHandle.IsAllocated) timelineHandle.Free();
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    static FMOD.RESULT BeatEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, System.IntPtr instancePtr, System.IntPtr parameterPtr) {
        System.IntPtr timelineInfoPtr;
        FMOD.Studio.EventInstance instance = new FMOD.Studio.EventInstance(instancePtr);
        instance.getUserData(out timelineInfoPtr);
        if (timelineInfoPtr != System.IntPtr.Zero) {
            GCHandle timelineHandle = GCHandle.FromIntPtr(timelineInfoPtr);
            TimelineInfo info = (TimelineInfo)timelineHandle.Target;
            info.beat++;
        }
        return FMOD.RESULT.OK;
    }
}