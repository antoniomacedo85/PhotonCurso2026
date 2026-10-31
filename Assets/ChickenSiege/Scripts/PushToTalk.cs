using Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 4 · Chat de voz: pulsar V para hablar + icono de "está hablando".
    /// Va en el prefab Player, junto a su NetworkObject.
    ///
    /// IMPORTANTE: este script solo compila DESPUÉS de instalar Photon Voice (Fusion Hub > Voice).
    ///
    /// QUÉ HACE PHOTON VOICE (y qué NO hace Fusion)
    ///  · La voz NO viaja por Fusion: va por su propio servidor de Photon Voice (otro AppId).
    ///  · FusionVoiceClient (en el Runner) se conecta a una sala de voz con el MISMO nombre
    ///    que nuestra sesión de Fusion. Así cada partida tiene su propio canal de voz.
    ///  · VoiceNetworkObject (en el Player) une "este jugador de Fusion" con "esta voz":
    ///    en MI jugador graba el micro (Recorder); en los demás reproduce (Speaker).
    ///  · El Speaker es un AudioSource: si lo ponemos en 3D, oímos a cada uno DESDE donde está.
    /// </summary>
    public class PushToTalk : NetworkBehaviour
    {
        [Header("Referencias (dentro del prefab Player)")]
        [Tooltip("El Recorder: graba MI micro (solo funciona en mi propio jugador)")]
        public Recorder Recorder;
        [Tooltip("El Speaker: reproduce la voz de los OTROS jugadores")]
        public Speaker Speaker;
        [Tooltip("Opcional: un icono sobre la cabeza que se enciende al hablar")]
        public GameObject SpeakingIcon;

        [Header("Pulsar para hablar")]
        [Tooltip("Desactivado = micro abierto todo el rato")]
        public bool UsePushToTalk = true;
        public Key TalkKey = Key.V;

        public override void Spawned()
        {
            // Solo MI jugador graba. Empezamos callados si usamos "pulsar para hablar".
            if (HasStateAuthority && Recorder != null)
            {
                Recorder.TransmitEnabled = UsePushToTalk == false;
            }
        }

        public override void Render()
        {
            // 1. Mi jugador: la tecla abre y cierra el micro
            if (HasStateAuthority && Recorder != null && UsePushToTalk)
            {
                var keyboard = Keyboard.current;
                Recorder.TransmitEnabled = keyboard != null && keyboard[TalkKey].isPressed;
            }

            // 2. El icono. Fijaos: NO es [Networked]. Cada ordenador lo deduce solo:
            //    · en mi jugador -> ¿estoy transmitiendo?
            //    · en los demás  -> ¿me está llegando su voz?
            if (SpeakingIcon != null)
            {
                bool speaking = HasStateAuthority
                    ? Recorder != null && Recorder.IsCurrentlyTransmitting
                    : Speaker != null && Speaker.IsPlaying;

                SpeakingIcon.SetActive(speaking);
            }
        }
    }
}
