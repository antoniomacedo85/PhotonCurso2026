#if UNITY_EDITOR
using System;
using System.Reflection;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Workaround para Photon Fusion 2 + Multiplayer Play Mode.
///
/// Fusion envía a los Player virtuales sus "custom dependencies" guardándolas en Temp/FusionMppm,
/// pero esa carpeta se vacía cada vez que se abre Unity, y algunas dependencias (orden de scripts,
/// tabla de prefabs) solo se vuelven a enviar cuando se reimporta un script o un prefab de red.
/// Resultado: el Player 2 ve el NetworkProjectConfig desactualizado, no puede reimportarlo
/// (su Asset Database es de solo lectura) y falla con "Failed to load NetworkProjectConfigAsset".
///
/// Este script, en el editor principal, vuelve a registrar todas las dependencias de Fusion
/// una vez por sesión de Unity (y a mano desde el menú), para que el Player 2 arranque sincronizado.
/// </summary>
[InitializeOnLoad]
static class FusionMppmDependencySync
{
    const string SessionKey = "FusionMppmDependencySync.Done";

    static FusionMppmDependencySync()
    {
        if (FusionMppm.Status != FusionMppmStatus.MainInstance)
            return;

        if (SessionState.GetBool(SessionKey, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return; // se reintenta en la próxima recarga del editor

            Sync();
            SessionState.SetBool(SessionKey, true);
        };
    }

    [MenuItem("Tools/Fusion/Sincronizar Multiplayer Play Mode", priority = 300)]
    static void Sync()
    {
        if (FusionMppm.Status != FusionMppmStatus.MainInstance)
        {
            Debug.LogWarning("[MPPM] Ejecutar solo desde el editor principal.");
            return;
        }

        int count = 0;
        Type[] types;
        try
        {
            types = typeof(FusionCustomDependency).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = Array.FindAll(e.Types, t => t != null);
        }

        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var type in types)
        {
            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType != typeof(FusionCustomDependency))
                    continue;

                if (field.GetValue(null) is FusionCustomDependency dependency)
                {
                    dependency.Refresh();
                    count++;
                }
            }
        }

        Debug.Log($"[MPPM] {count} dependencias de Fusion sincronizadas con los Player virtuales.");
    }
}
#endif
