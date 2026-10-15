# Curso Multijugador con Photon Fusion · Unity

Proyecto del curso **Multijugador con Photon Fusion 2** (CGIFP Eduardo Barreiros, octubre de 2026).
Basado en el **Fusion Starter** oficial de Photon (Shared Mode 2.1.1).

## Requisitos

- **Unity 6000.3.10f1** (desde Unity Hub)
- Git o GitHub Desktop

## Cómo abrirlo

1. Clonad el repositorio con GitHub Desktop, o bajadlo con **Code → Download ZIP**.
2. Unity Hub → **Add → Add project from disk** → la carpeta del proyecto → abrir con **6000.3.10f1**.
3. Esperad a que termine de importar. En la consola tiene que aparecer **"[MPPM] … dependencias de Fusion sincronizadas"**.
4. Abrid `Assets/00_MainMenu/00_MainMenu.unity` → **Play** → **Third Person Character** → nombre de sala → **Start Game**.
5. Segundo jugador: **Window → Multiplayer → Multiplayer Play Mode** → activad **Player 2** y entrad en la misma sala.

## Clases

Cada clase termina con un **tag**: en GitHub Desktop, *History*, o en la web, *Tags*, podéis volver al estado del proyecto al final de cada sesión.

| Tag | Sesión | Contenido |
| --- | --- | --- |
| `clase-01` | Jue 15 oct | Fundamentos, primera conexión, script `PlayerColor` (un color por jugador) |
| `clase-02` | Sáb 24 oct | Salas, lobby y arquitectura |
| `clase-03` | Mar 27 oct | Sincronización, autoridad y mecánicas |
| `clase-04` | Sáb 31 oct | Optimización y proyecto final |

Consultas: sábados 24 y 31 de octubre, de 15:30 a 18:30.

## Dónde está cada cosa

| Ruta | Qué es |
| --- | --- |
| `Assets/Common/Scripts/UIGameMenu.cs` | La conexión a la red (`StartGame()`) |
| `Assets/01_ThirdPersonCharacter/GameManager.cs` | Aparición de cada jugador (`Runner.Spawn`) |
| `Assets/01_ThirdPersonCharacter/Player.cs` | Movimiento, `[Networked]` y `HasStateAuthority` |
| `Assets/Scripts/PlayerColor.cs` | Nuestro primer script de red |
| `Assets/Editor/FusionMppmDependencySync.cs` | Arreglo para Multiplayer Play Mode (no tocar) |
| `Assets/Photon/` | SDK de Fusion (no tocar) |

## Si algo falla

| Problema | Solución |
| --- | --- |
| El Player 2 da *"Failed to load NetworkProjectConfigAsset"* | Desactivad el Player 2 → **Tools → Fusion → Sincronizar Multiplayer Play Mode** → volved a activarlo |
| Cambié un script y el Player 2 no lo ve | Guardad y reiniciad el Player 2 |
| No puedo cambiar de ventana / no veo el ratón | **Esc** abre el menú y suelta el cursor |
| No nos vemos | Misma sala, mismo minijuego y misma región (`eu`) |
| No conecta | Probad con el móvil como hotspot (cortafuegos de la red) |
| GitHub quiere subir miles de archivos | Falta el `.gitignore` de Unity: `Library/` no se sube nunca |

## AppId

El proyecto usa un **AppId de Photon temporal, solo para el curso**, que se desactivará al terminar.
Para vuestro proyecto propio, cread el vuestro en [dashboard.photonengine.com](https://dashboard.photonengine.com) (**Create a new app → Fusion → Fusion 2**) y pegadlo en **Tools → Fusion → Realtime Settings → App Id Fusion**.
