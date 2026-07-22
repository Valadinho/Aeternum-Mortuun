using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "DiceRNGPowerUp", menuName = "PowerUps/Lanza los dados!")]
public class DiceRNGPowerUp : PowerUp
{
    [Header("Dados (valores posibles)")]
    public int[] diceFaces = new int[] { -3, -2, -1, 1, 2, 3 };

    [Header("Overlay")]
    [Tooltip("Segundos de 'falso shuffle' por stat antes de mostrar el resultado")]
    public float rollAnimPerStat = 0.6f;

    [Tooltip("Segundos que el resultado final queda visible")]
    public float showFinalFor = 1.2f;

    [Header("Overlay Visual")]
    [Tooltip("Fondo del panel donde se muestran las tiradas.")]
    public Sprite panelBackground;
    [Range(0f, 1f)]
    [Tooltip("Opacidad del fondo. 0.7 equivale a 30% transparente.")]
    public float panelBackgroundAlpha = 0.7f;

    // ✅ Marker global: si existe, esta perk ya se ejecutó en esta run
    private const string AppliedOnceMarkerName = "DiceRNG_APPLIED_ONCE";

    public override void Apply(PlayerController player)
    {
        if (player == null) return;

        // ✅ Si ya se ejecutó una vez en la run, NO volver a ejecutar
        if (GameObject.Find(AppliedOnceMarkerName) != null)
        {
            // Igual limpiamos de la lista por si volvió a aparecer
            RemoveFromPlayerList(player);
            return;
        }

        // Evita doble aplicación en el mismo frame/escena
        if (GameObject.Find("DiceRNGMarker") != null) return;

        // ✅ Marcador permanente de “ya se ejecutó”
        var appliedOnce = new GameObject(AppliedOnceMarkerName);
        Object.DontDestroyOnLoad(appliedOnce);

        // Marcador temporal para bloquear reprocesos inmediatos
        var marker = new GameObject("DiceRNGMarker");
        Object.DontDestroyOnLoad(marker);

        // Overlay que tira los dados y aplica en escena actual
        var go = new GameObject("DiceRollOverlay");
        var overlay = go.AddComponent<DiceRollOverlay>();
        overlay.Initialize(player, diceFaces, rollAnimPerStat, showFinalFor, marker, panelBackground, panelBackgroundAlpha);

        // Remover la perk de initialPowerUps (one-shot)
        RemoveFromPlayerList(player);
    }

    private void RemoveFromPlayerList(PlayerController player)
    {
        if (player.initialPowerUps == null || player.initialPowerUps.Length == 0) return;

        var list = new List<PowerUp>(player.initialPowerUps);
        bool removedAny = false;

        while (list.Remove(this))
            removedAny = true;

        if (removedAny)
            player.initialPowerUps = list.ToArray();
    }

    public override void Remove(PlayerController player) { }
}
