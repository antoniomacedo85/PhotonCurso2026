using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Starter
{
    /// <summary>
    /// CLASE 2 · Lista de salas del lobby.
    /// UIGameMenu recibe el evento OnSessionListUpdate, filtra por minijuego y llama a Refresh
    /// pasando qué hacer al pulsar "Unirse" (su método JoinSession).
    /// Cada vez que cambia algo en Photon (sala nueva, alguien entra o sale) llega la lista completa.
    /// </summary>
    public class UISessionList : MonoBehaviour
    {
        [Tooltip("Contenedor de las filas (el Content de un Scroll View con Vertical Layout Group)")]
        public Transform Content;
        public SessionRow RowPrefab;
        [Tooltip("Texto 'No hay salas, ¡creá una!' (opcional)")]
        public GameObject EmptyLabel;

        private readonly List<SessionRow> _rows = new List<SessionRow>();

        public void Refresh(List<SessionInfo> sessions, Action<SessionInfo> onJoin)
        {
            // Lo más simple: borrar todas las filas y crearlas de nuevo
            foreach (var row in _rows)
            {
                Destroy(row.gameObject);
            }
            _rows.Clear();

            foreach (var session in sessions)
            {
                // Las salas con IsVisible = false no llegan a la lista
                var row = Instantiate(RowPrefab, Content);
                row.Setup(session, onJoin);
                _rows.Add(row);
            }

            if (EmptyLabel != null)
            {
                EmptyLabel.SetActive(sessions.Count == 0);
            }
        }
    }
}
