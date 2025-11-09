using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    public Transform waypoint;
    public float interval = 1f;
    public int count = 1;       // cuántas instancias spawnear
    public bool enabled = true;
}

[System.Serializable]
public class Wave
{
    public string name = "Wave";
    public List<SpawnEntry> entries = new List<SpawnEntry>();
}

public class WaveManager : MonoBehaviour
{
    [Header("Waves")]
    public List<Wave> waves = new List<Wave>();
    public int wavesToComplete = 0; // si 0 => usar waves.Count

    [Header("Behavior")]
    public bool startOnAwake = false;
    public GameObject destroyTargetOnWavesComplete; // se destruye cuando completes wavesToComplete

    [Header("Parallel timer (opcional)")]
    public bool startParallelTimerOnStart = false;
    public float parallelTimerDuration = 30f;
    public GameObject parallelTimerDestroyTarget;

    [Header("Parallel timer UI")]
    public TextMeshProUGUI parallelTimerText;

    // internal
    private int _currentWaveIndex = -1;
    private bool _running = false;

    // Conteo de enemigos vivos instanciados por el manager en la wave actual
    private int _aliveEnemies = 0;

    // Conteo de instancias pendientes por crear (spawn planificados pero no todos instanciados aún)
    private int _pendingSpawns = 0;

    private void Start()
    {
        if (startOnAwake)
        {
            StartWaves();
        }
    }

    // Llamar para iniciar la secuencia de oleadas
    public void StartWaves()
    {
        if (_running) return;
        if (waves == null || waves.Count == 0)
        {
            Debug.LogWarning("[WaveManager] No waves configured.", this);
            return;
        }

        _running = true;
        _currentWaveIndex = -1;

        if (startParallelTimerOnStart)
        {
            StartCoroutine(ParallelTimerRoutine());
        }

        StartCoroutine(NextWaveRoutine());
        Debug.Log("[WaveManager] Waves started.", this);
    }

    // Rutina: inicia la siguiente wave secuencialmente
    private IEnumerator NextWaveRoutine()
    {
        int targetWaves = (wavesToComplete > 0) ? wavesToComplete : waves.Count;

        while (_running)
        {
            _currentWaveIndex++;
            if (_currentWaveIndex >= waves.Count)
            {
                Debug.Log("[WaveManager] No more configured waves.", this);
                break;
            }

            Debug.Log("[WaveManager] Starting wave " + (_currentWaveIndex + 1) + " / " + waves.Count + " (" + waves[_currentWaveIndex].name + ")", this);

            // Ejecutar la wave y esperar a que termine
            yield return StartCoroutine(RunWave(waves[_currentWaveIndex]));

            // chequeo si alcanzamos el objetivo de wavesToComplete
            if ((_currentWaveIndex + 1) >= targetWaves)
            {
                Debug.Log("[WaveManager] Reached target waves: " + targetWaves, this);
                if (destroyTargetOnWavesComplete != null)
                {
                    Debug.Log("[WaveManager] Destroying target on waves complete: " + destroyTargetOnWavesComplete.name, this);
                    Destroy(destroyTargetOnWavesComplete);
                }

                // Marcar como no corriendo: esto hará que el timer paralelo termine sin ejecutar su acción final.
                _running = false;
                yield break;
            }

            // pequeña pausa opcional entre oleadas
            yield return new WaitForSeconds(1f);
        }

        _running = false;
    }

    // Ejecuta una sola wave: iniciar todos los spawn entries y esperar hasta completarla
    private IEnumerator RunWave(Wave wave)
    {
        if (wave == null)
        {
            Debug.LogWarning("[WaveManager] RunWave called with null wave.", this);
            yield break;
        }

        // reset counters para esta wave
        _aliveEnemies = 0;
        _pendingSpawns = 0;

        // Lista de coroutines para los spawn loops (no estrictamente necesaria, pero útil para debugging)
        List<Coroutine> runningSpawns = new List<Coroutine>();

        // iniciar spawn loops por cada entry válida
        for (int i = 0; i < wave.entries.Count; i++)
        {
            SpawnEntry entry = wave.entries[i];
            if (entry != null && entry.enabled && entry.prefab != null && entry.interval >= 0f && entry.count > 0)
            {
                // marcar cuántas instancias están pendientes por crear
                _pendingSpawns += entry.count;

                Coroutine c = StartCoroutine(SpawnLoop(entry));
                runningSpawns.Add(c);
            }
            else
            {
                string info = "Entry " + i + " skipped:";
                if (entry == null) info += " entry==null";
                else
                {
                    info += " enabled=" + entry.enabled;
                    info += " prefab=" + (entry.prefab != null ? entry.prefab.name : "null");
                    info += " interval=" + entry.interval;
                    info += " count=" + entry.count;
                }
                Debug.Log("[WaveManager] " + info, this);
            }
        }

        // Esperar hasta que no queden spawns pendientes y no queden enemigos vivos
        while ((_pendingSpawns > 0) || (_aliveEnemies > 0))
        {
            yield return null;
        }

        Debug.Log("[WaveManager] Wave '" + wave.name + "' completed.", this);
        yield break;
    }

    // SpawnLoop que crea 'count' instancias espaciadas por 'interval'
    private IEnumerator SpawnLoop(SpawnEntry entry)
    {
        if (entry == null) yield break;

        int remaining = entry.count;
        Debug.Log("[WaveManager] SpawnLoop starting for prefab " + (entry.prefab != null ? entry.prefab.name : "null") + " count=" + remaining + " interval=" + entry.interval, this);

        Vector3 pos = (entry.waypoint != null) ? entry.waypoint.position : this.transform.position;

        // spawn inmediata la primera instancia (si preferís esperar antes del primer spawn, mové el yield WaitForSeconds arriba)
        while (remaining > 0)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning("[WaveManager] SpawnLoop: prefab is null, skipping remaining.", this);
                // ajustar pending para no quedar colgado
                _pendingSpawns -= remaining;
                if (_pendingSpawns < 0) _pendingSpawns = 0;
                yield break;
            }

            Vector3 spawnPos = (entry.waypoint != null) ? entry.waypoint.position : this.transform.position;
            GameObject go = Instantiate(entry.prefab, spawnPos, Quaternion.identity);

            // Añadir componente tracker para que notifique cuando el enemigo muera
            SpawnedEnemy tracker = go.AddComponent<SpawnedEnemy>();
            tracker.manager = this;

            // incrementar contador de enemigos vivos
            _aliveEnemies = _aliveEnemies + 1;

            Debug.Log("[WaveManager] Spawned " + go.name + " at " + spawnPos + " (alive now: " + _aliveEnemies + ")", this);

            remaining--;

            if (remaining > 0)
            {
                // esperar intervalo antes del siguiente spawn
                yield return new WaitForSeconds(entry.interval);
            }
        }

        // terminé de spawnear todas las instancias de este entry
        _pendingSpawns -= entry.count;
        if (_pendingSpawns < 0) _pendingSpawns = 0;

        Debug.Log("[WaveManager] SpawnLoop finished for prefab " + (entry.prefab != null ? entry.prefab.name : "null"), this);
        yield break;
    }

    // Método llamado por SpawnedEnemy cuando una instancia es destruida
    public void NotifyEnemyDestroyed()
    {
        _aliveEnemies = _aliveEnemies - 1;
        if (_aliveEnemies < 0) _aliveEnemies = 0;
        Debug.Log("[WaveManager] Enemy destroyed. Alive left: " + _aliveEnemies, this);
    }

    // Timer paralelo opcional
    private IEnumerator ParallelTimerRoutine()
    {
        float elapsed = 0f;
        Debug.Log("[WaveManager] Parallel timer started: " + parallelTimerDuration + "s", this);

        // mientras el manager siga corriendo y no alcance la duración
        while (elapsed < parallelTimerDuration && _running)
        {
            elapsed += Time.deltaTime;
            float remaining = Mathf.Clamp(parallelTimerDuration - elapsed, 0, parallelTimerDuration);

            // ACTUALIZAMOS EL TEXTO
            if (parallelTimerText != null)
            {
                parallelTimerText.text = Mathf.CeilToInt(remaining).ToString();
            }

            yield return null;
        }

        // Si el loop terminó porque _running pasó a false => no ejecutamos la acción de terminar timer
        if (!_running)
        {
            Debug.Log("[WaveManager] Parallel timer stopped because waves finished.", this);
            yield break;
        }

        // Si llegamos hasta aquí, el timer llegó a 0 mientras _running sigue true: ejecutamos la acción (ej. destruir target)
        Debug.Log("[WaveManager] Parallel timer finished.", this);

        if (parallelTimerDestroyTarget != null)
        {
            Debug.Log("[WaveManager] Destroying parallelTimerDestroyTarget: " + parallelTimerDestroyTarget.name, this);
            Destroy(parallelTimerDestroyTarget);
        }
    }
}
