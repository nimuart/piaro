using UnityEngine;
using FMODUnity;
using System.Runtime.InteropServices;

public class RitmoManager : MonoBehaviour
{
    public EventReference musicaBase;
    private FMOD.Studio.EventInstance instanciaMusica;

    // Variables para la lógica de Patapon
    public float tolerancia = 0.15f; 
    private double tiempoUltimoPulso;
    private int pulsoActual = 0;

    // Estructura para pasar datos entre el hilo de Audio y Unity
    class TimelineInfo {
        public int beat = 0;
    }
    TimelineInfo timelineInfo;
    FMOD.Studio.EVENT_CALLBACK beatCallback;

    void Start() {
        timelineInfo = new TimelineInfo();
        beatCallback = new FMOD.Studio.EVENT_CALLBACK(BeatEventCallback);

        instanciaMusica = RuntimeManager.CreateInstance(musicaBase);
        
        // Esto permite que FMOD nos avise cuando hay un beat
        instanciaMusica.setUserData(GCHandle.ToIntPtr(GCHandle.Alloc(timelineInfo)));
        instanciaMusica.setCallback(beatCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
        
        instanciaMusica.start();
    }

    void Update() {
        // Chequeamos si el pulso cambió en el hilo de audio
        if (pulsoActual != timelineInfo.beat) {
            pulsoActual = timelineInfo.beat;
            tiempoUltimoPulso = Time.timeAsDouble;
            Debug.Log("¡PULSO! #" + pulsoActual);
        }

        // Input de Patapon (Cuadrado = PATA)
        if (Input.GetKeyDown(KeyCode.A)) {
            ProcesarTambor("PATA");
        }
    }

    void ProcesarTambor(string tipo) {
        double tiempoAhora = Time.timeAsDouble;
        double diferencia = System.Math.Abs(tiempoAhora - tiempoUltimoPulso);

        if (diferencia <= tolerancia) {
            Debug.Log("<color=green>¡PERFECTO!</color> " + tipo + " en el pulso " + pulsoActual);
        } else {
            Debug.Log("<color=red>FALLASTE</color> por " + diferencia + " segundos.");
        }
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    static FMOD.RESULT BeatEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, System.IntPtr instancePtr, System.IntPtr parameterPtr) {
        FMOD.Studio.EventInstance instance = new FMOD.Studio.EventInstance(instancePtr);
        System.IntPtr timelineInfoPtr;
        instance.getUserData(out timelineInfoPtr);

        if (timelineInfoPtr != System.IntPtr.Zero) {
            GCHandle timelineHandle = GCHandle.FromIntPtr(timelineInfoPtr);
            TimelineInfo info = (TimelineInfo)timelineHandle.Target;
            
            if (type == FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT) {
                var parameter = (FMOD.Studio.TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.TIMELINE_BEAT_PROPERTIES));
                info.beat = parameter.beat;
            }
        }
        return FMOD.RESULT.OK;
    }
}