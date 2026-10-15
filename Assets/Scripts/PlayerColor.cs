using Fusion;
using UnityEngine;

public class PlayerColor : NetworkBehaviour
{
    public Renderer Body;

    [Networked, OnChangedRender(nameof(OnColorChanged))]
    public Color BodyColor { get; set; }

    public override void Spawned()
    {
        // Solo el dueño elige el color; los demás lo reciben por red
        if (HasStateAuthority)
            BodyColor = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);

        // OnChangedRender no salta con el valor inicial
        OnColorChanged();
    }

    private void OnColorChanged()
    {
        foreach (var material in Body.materials)
            material.color = BodyColor;
    }
}