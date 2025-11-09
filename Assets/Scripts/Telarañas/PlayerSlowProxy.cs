using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Componente que se a�ade al jugador en tiempo de ejecuci�n (no modifica c�digo del PlayerController).
/// Gestiona slows por fuente (identificador string) y recalcula moveSpeed / dashSpeed si existen como campos p�blicos.
/// Si no existen esos campos, aplica un fallback que escala la velocidad f�sica en FixedUpdate.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSlowProxy : MonoBehaviour
{
    // Referencia al componente original (si existe)
    private Component playerController;
    private Rigidbody2D rb;

    // Reflection: campos en PlayerController si existen
    private FieldInfo fld_moveSpeed;
    private FieldInfo fld_dashSpeed;

    // Base original para recalcular
    private float baseMoveSpeed = -1f;
    private float baseDashSpeed = -1f;

    // Active slows: sourceId -> (moveMul, dashMul)
    private Dictionary<string, (float moveMul, float dashMul)> activeSlows =
        new Dictionary<string, (float moveMul, float dashMul)>();

    // Coroutines por source para duraci�n
    private Dictionary<string, Coroutine> durationCoros = new Dictionary<string, Coroutine>();

    // Fallback mode flag (si no se encontr� moveSpeed/dashSpeed p�blicos)
    private bool useFallbackVelocityScaling = false;
    private float currentMoveMultiplier = 1f; // se aplica sobre movimiento f�sico en fallback

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Intentar encontrar cualquier tipo PlayerController (nombre habitual)
        var t = GetComponent("PlayerController")?.GetType();
        if (t != null)
        {
            playerController = GetComponent("PlayerController") as Component;
            fld_moveSpeed = t.GetField("moveSpeed", BindingFlags.Public | BindingFlags.Instance);
            fld_dashSpeed = t.GetField("dashSpeed", BindingFlags.Public | BindingFlags.Instance);

            if (fld_moveSpeed != null)
            {
                object val = fld_moveSpeed.GetValue(playerController);
                if (val is float f) baseMoveSpeed = f;
            }
            if (fld_dashSpeed != null)
            {
                object val = fld_dashSpeed.GetValue(playerController);
                if (val is float f) baseDashSpeed = f;
            }
        }

        // Si no encontramos campos p�blicos, usaremos fallback
        if (fld_moveSpeed == null && fld_dashSpeed == null)
        {
            useFallbackVelocityScaling = true;
            // Si no hay rb, fallback ser� d�bil; avisamos en consola.
            if (rb == null)
            {
                Debug.LogWarning("[PlayerSlowProxy] No encontr� moveSpeed/dashSpeed p�blicos y el jugador no tiene Rigidbody2D. El slow no podr� aplicarse correctamente.");
            }
            else
            {
                // Intentamos inferir una velocidad base (opcional); dejamos baseMoveSpeed = -1 para indicar no usamos los campos.
                baseMoveSpeed = -1f;
            }
        }
    }

    /// <summary>
    /// Aplica (o refresca) un slow identificado por sourceId.
    /// moveMultiplier: 0..1 (0.5 -> 50% velocidad). dashMultiplier: 0..1.
    /// duration <= 0 => persistente hasta RemoveSlow.
    /// interruptDash: si se quiere intentar cancelar dash y existe el PlayerController public, se intenta via reflection (busca IsDashing o ChangeState).
    /// </summary>
    public void ApplySlow(string sourceId, float moveMultiplier = 1f, float dashMultiplier = 1f, float duration = 1f, bool interruptDash = false)
    {
        if (string.IsNullOrEmpty(sourceId)) sourceId = Guid.NewGuid().ToString();

        // Cancelar coroutine previa si existe (refrescar)
        if (durationCoros.ContainsKey(sourceId))
        {
            StopCoroutine(durationCoros[sourceId]);
            durationCoros.Remove(sourceId);
        }

        activeSlows[sourceId] = (moveMultiplier, dashMultiplier);

        RecalculateAndApply();

        // Si duration > 0 programar remoci�n
        if (duration > 0f)
        {
            Coroutine c = StartCoroutine(SlowDurationCoroutine(sourceId, duration));
            durationCoros[sourceId] = c;
        }

        // Intentar interrumpir dash si el PlayerController tiene la propiedad IsDashing p�blica o m�todo ChangeState
        if (interruptDash && playerController != null)
        {
            TryInterruptDash();
        }
    }

    public void RemoveSlow(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        if (activeSlows.ContainsKey(sourceId))
            activeSlows.Remove(sourceId);

        if (durationCoros.ContainsKey(sourceId))
        {
            StopCoroutine(durationCoros[sourceId]);
            durationCoros.Remove(sourceId);
        }

        RecalculateAndApply();
    }

    private IEnumerator SlowDurationCoroutine(string sourceId, float duration)
    {
        yield return new WaitForSeconds(duration);
        RemoveSlow(sourceId);
    }

    private void RecalculateAndApply()
    {
        // Producto de multiplicadores
        float moveMul = 1f;
        float dashMul = 1f;
        foreach (var kv in activeSlows.Values)
        {
            moveMul *= kv.moveMul;
            dashMul *= kv.dashMul;
        }

        // Seguridad
        moveMul = Mathf.Max(0f, moveMul);
        dashMul = Mathf.Max(0f, dashMul);

        currentMoveMultiplier = moveMul;

        // Si tenemos campos p�blicos, los seteamos
        if (fld_moveSpeed != null && playerController != null)
        {
            if (baseMoveSpeed < 0f)
            {
                // Si no ten�amos guardado el base, guardarlo ahora
                object orig = fld_moveSpeed.GetValue(playerController);
                if (orig is float f) baseMoveSpeed = f;
                else baseMoveSpeed = 0f;
            }
            float newMove = baseMoveSpeed * moveMul;
            fld_moveSpeed.SetValue(playerController, newMove);
        }

        if (fld_dashSpeed != null && playerController != null)
        {
            if (baseDashSpeed < 0f)
            {
                object orig = fld_dashSpeed.GetValue(playerController);
                if (orig is float f) baseDashSpeed = f;
                else baseDashSpeed = 0f;
            }
            float newDash = baseDashSpeed * dashMul;
            fld_dashSpeed.SetValue(playerController, newDash);
        }
    }

    private void FixedUpdate()
    {
        // Fallback: si no podemos tocar campos de PlayerController, escalamos la velocidad f�sica.
        if (!useFallbackVelocityScaling) return;
        if (rb == null) return;

        // Este fallback pretende simular un slowdown aumentando drag * temporalmente *
        // Alternativa: multiplicar la velocidad actual (no permanente). Aqu� aplicamos un ligero damping proporcional.
        float mul = currentMoveMultiplier;
        // Por seguridad, si mul == 1 no hacemos nada
        if (Mathf.Approximately(mul, 1f)) return;

        // Si hay movimiento, reducimos la velocidad actual
        rb.linearVelocity = rb.linearVelocity * mul;
    }

    /// <summary>
    /// Intenta interrumpir dash (si existe). Busca propiedades o m�todos comunes mediante reflection:
    /// - Propiedad p�blica "IsDashing" (bool) => si true y existe m�todo ChangeState or set IsDashing = false.
    /// - M�todo "ChangeState" con un par�metro (int/enum/object) se ignora (riesgoso).
    /// Si no hay forma segura, no hace nada.
    /// </summary>
    private void TryInterruptDash()
    {
        if (playerController == null) return;
        Type t = playerController.GetType();

        // 1) Buscar propiedad IsDashing (bool)
        var prop = t.GetProperty("IsDashing", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
        {
            bool isD = (bool)prop.GetValue(playerController);
            if (isD) prop.SetValue(playerController, false);
            return;
        }

        // 2) Buscar campo p�blico IsDashing
        var fd = t.GetField("IsDashing", BindingFlags.Public | BindingFlags.Instance);
        if (fd != null && fd.FieldType == typeof(bool))
        {
            bool isD = (bool)fd.GetValue(playerController);
            if (isD) fd.SetValue(playerController, false);
            return;
        }

        // 3) Intentar llamar a ChangeState(IdleState) si existe IdleState campo/proper y ChangeState method.
        // Esto es arriesgado porque requiere conocer el IdleState instance; en general preferimos no tocar.
    }

    private void OnDestroy()
    {
        // Restaurar valores originales si existieran
        if (fld_moveSpeed != null && playerController != null && baseMoveSpeed >= 0f)
            fld_moveSpeed.SetValue(playerController, baseMoveSpeed);

        if (fld_dashSpeed != null && playerController != null && baseDashSpeed >= 0f)
            fld_dashSpeed.SetValue(playerController, baseDashSpeed);
    }
}
