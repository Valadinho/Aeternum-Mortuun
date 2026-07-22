using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Events;
using System.Collections.Generic;
using TMPro;
using System;

namespace EasyUI.PickerWheelUI
{
    [Serializable]
    public class WeightedPowerUpPool
    {
        public PowerUpPool pool;
        [Range(0f, 100f)] public float weight = 1f;
    }

    public class PickerWheel : MonoBehaviour
    {


        [Header("Pools ponderados")]
        [SerializeField] private List<WeightedPowerUpPool> powerUpPools = new List<WeightedPowerUpPool>();
        [SerializeField] private PowerUpPool powerUpPool;

        [Header("Referencias")]
        [SerializeField] private RewardPopupUI rewardPopup;
        [SerializeField] private GameObject linePrefab;
        [SerializeField] private Transform linesParent;
        [SerializeField] private Transform wheelCircle; // El objeto que gira
        [SerializeField] private GameObject wheelPiecePrefab;
        [SerializeField] private Transform wheelPiecesParent;

        [Header("Sonidos")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip tickAudioClip;
        [Range(0f, 1f)][SerializeField] private float volume = .5f;
        [Range(-3f, 3f)][SerializeField] private float pitch = 1f;

        [Header("Ajustes de Alineación y Debug")]
        [Tooltip("Ángulo donde está tu flecha (Ej: Arriba=90, Derecha=0)")]
        [SerializeField] private float wheelOffset = 90f;
        [Tooltip("Distancia del centro a la punta de la flecha (para el Gizmo verde)")]
        [SerializeField] private float pointerDistance = 150f; // <-- NUEVO CONTROL DE DISTANCIA
        [Tooltip("Tamaño de la esfera del Gizmo")]
        [SerializeField] private float pointerSize = 15f;      // <-- NUEVO CONTROL DE TAMAÑO

        [Header("Configuración de Giro")]
        [Range(1, 20)] public float spinDuration = 8f;
        [SerializeField] private int spinRounds = 10;
        [SerializeField] private AnimationCurve spinCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Datos (Solo lectura)")]
        public WheelPiece[] wheelPieces;
        private WheelPiece ultimoPremio;

        [Header("Usos")]
        [SerializeField] private int usosMaximos = 3;
        private int usosRestantes;

        [Header("Configuración de Skip")]
        [SerializeField] private float velocidadSkip = 12f;

        // Estado interno
        private bool _isSpinning = false;
        public bool IsSpinning => _isSpinning;
        private Tween _pickerTween;
        private float pieceAngle;
        private float halfPieceAngle;
        private double accumulatedWeight;
        private System.Random rand = new System.Random();

        // Variable temporal para el objetivo matemático (se usa para guiar la animación)
        private int targetIndexMath;

        // Eventos
        public Action<WheelPiece> OnSpinEnd;
        private UnityAction onSpinStartEvent;
        private UnityAction<WheelPiece> onSpinEndEvent;

        // Configuración UI
        private Vector2 pieceMinSize = new Vector2(81f, 146f);
        private Vector2 pieceMaxSize = new Vector2(144f, 213f);
        private int piecesMin = 2;
        private int piecesMax = 12;

        public int UsosRestantes => usosRestantes;
        public int UsosMaximos => usosMaximos;

        private void Awake() { usosRestantes = usosMaximos; }

        private void Start()
        {
            if (rewardPopup == null) rewardPopup = FindObjectOfType<RewardPopupUI>(true);
            SetupAudio();
            CargarPremiosDesdePoolsPonderados(); // Esto llama a Generate y CalculateWeights
        }

        private void SetupAudio()
        {
            if (audioSource != null)
            {
                audioSource.clip = tickAudioClip;
                audioSource.volume = volume;
                audioSource.pitch = pitch;
            }
        }

        // --- LÓGICA PRINCIPAL ---

        public void Spin()
        {
            // SKIP: acelerar el giro que ya existe
            if (_isSpinning)
            {
                if (_pickerTween != null && _pickerTween.IsActive())
                {
                    _pickerTween.timeScale = velocidadSkip;
                }

                return;
            }

            // Validación de usos
            if (usosRestantes <= 0)
            {
                Debug.LogWarning("Sin usos.");
                return;
            }

            // Inicio del giro normal
            _isSpinning = true;
            onSpinStartEvent?.Invoke();

            targetIndexMath = GetRandomPieceIndex();

            float anglePerItem = 360f / wheelPieces.Length;
            float targetBaseAngle = -(anglePerItem * targetIndexMath);
            targetBaseAngle += wheelOffset;

            float offsetRange = anglePerItem * 0.45f;
            float randomOffset = UnityEngine.Random.Range(-offsetRange, offsetRange);

            float finalTargetAngle = targetBaseAngle + randomOffset;
            float totalRotation = finalTargetAngle - (360f * spinRounds);

            float prevAngle = wheelCircle.eulerAngles.z;
            float currentAngle = prevAngle;
            bool isIndicatorOnLine = false;

            _pickerTween = wheelCircle
                .DORotate(
                    new Vector3(0, 0, totalRotation),
                    spinDuration,
                    RotateMode.FastBeyond360
                )
                .SetEase(spinCurve)
                .SetUpdate(true)
                .OnUpdate(() =>
                {
                    float diff = Mathf.Abs(prevAngle - currentAngle);

                    if (diff >= halfPieceAngle)
                    {
                        if (isIndicatorOnLine && audioSource != null)
                            audioSource.PlayOneShot(audioSource.clip);

                        prevAngle = currentAngle;
                        isIndicatorOnLine = !isIndicatorOnLine;
                    }

                    currentAngle = wheelCircle.eulerAngles.z;
                })
                .OnComplete(FinalizarGiro);
        }



        private float CalcularRotacionParaAlinearReferencia(Transform referencia)
        {
            // Dirección del puntero
            float angleRad = wheelOffset * Mathf.Deg2Rad;
            Vector3 localDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);
            Vector3 worldDir = wheelCircle.parent != null
                ? wheelCircle.parent.TransformDirection(localDir)
                : localDir;

            Vector3 pointerPos = wheelCircle.position + worldDir * pointerDistance;

            // Dirección desde el centro a la referencia
            Vector3 dirReferencia = referencia.position - wheelCircle.position;
            Vector3 dirPuntero = pointerPos - wheelCircle.position;

            float angleReferencia = Mathf.Atan2(dirReferencia.y, dirReferencia.x) * Mathf.Rad2Deg;
            float anglePuntero = Mathf.Atan2(dirPuntero.y, dirPuntero.x) * Mathf.Rad2Deg;

            float delta = anglePuntero - angleReferencia;

            return wheelCircle.eulerAngles.z + delta;
        }

        // --- NUEVA LÓGICA PARA DETERMINAR GANADOR ---

        private void FinalizarGiro()
        {
            _isSpinning = false;
            usosRestantes--;

            if (_pickerTween != null)
                _pickerTween.timeScale = 1f;

            _pickerTween = null;

            ultimoPremio = CalcularGanadorVisualmente();

            Debug.Log($"🎯 Ganador visual determinado: {ultimoPremio.Label}");

            MostrarPopupUltimoPremio();
            OnSpinEnd?.Invoke(ultimoPremio);
            onSpinEndEvent?.Invoke(ultimoPremio);
        }
        // Esta función calcula qué pieza está físicamente más cerca del puntero verde
        private WheelPiece CalcularGanadorVisualmente()
        {
            // 1. Calcular la posición mundial de la "pelota verde" (el puntero imaginario)
            float angleRad = wheelOffset * Mathf.Deg2Rad;
            // Dirección local basada en el offset
            Vector3 localDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);
            // Si la ruleta tiene un padre que rota, convertimos la dirección a espacio mundial
            Vector3 worldDir = wheelCircle.parent != null ? wheelCircle.parent.TransformDirection(localDir) : localDir;
            // Posición final del puntero
            Vector3 pointerPos = wheelCircle.position + (worldDir * pointerDistance);


            // 2. Recorrer todas las piezas y encontrar la más cercana
            float minDistance = float.MaxValue;
            int closestIndex = 0;

            // Asumimos que los hijos en wheelPiecesParent están en el mismo orden que el array wheelPieces
            for (int i = 0; i < wheelPiecesParent.childCount; i++)
            {
                Transform pieceTransform = wheelPiecesParent.GetChild(i);

                // Buscamos un punto de referencia central en la pieza. 
                // El "IconContainer" suele estar bien centrado en el gajo.
                Transform referencePoint = pieceTransform.Find("IconContainer");
                if (referencePoint == null) referencePoint = pieceTransform.GetChild(0); // Fallback

                // Calculamos distancia entre el puntero y el centro de este gajo
                float dist = Vector3.Distance(pointerPos, referencePoint.position);

                // Si esta pieza está más cerca que la anterior, es la nueva candidata
                if (dist < minDistance)
                {
                    minDistance = dist;
                    // El índice del hijo en la jerarquía corresponde al índice en el array
                    closestIndex = i;
                }
            }

            // Devolvemos la pieza que resultó estar más cerca
            return wheelPieces[closestIndex];
        }

        // --- VISUALIZACIÓN DE DEBUG (GIZMO MEJORADO) ---
        private void OnDrawGizmos()
        {
            if (wheelCircle != null)
            {
                Gizmos.color = Color.green;

                // Cálculo de posición
                float angleRad = (wheelOffset) * Mathf.Deg2Rad;
                Vector3 localDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);

                // Definimos la variable como 'worldDir'
                Vector3 worldDir = wheelCircle.parent != null ? wheelCircle.parent.TransformDirection(localDir) : localDir;

                Vector3 center = wheelCircle.position;

                // CORRECCIÓN: Aquí decía 'worldDirection', debe ser 'worldDir'
                Vector3 end = center + (worldDir * pointerDistance);

                Gizmos.DrawLine(center, end);
                Gizmos.DrawSphere(end, pointerSize);
            }
        }

        // --- MÉTODOS PÚBLICOS Y DE UTILIDAD (Sin cambios importantes) ---

        public void MostrarPopupUltimoPremio()
        {
            if (ultimoPremio != null && rewardPopup != null)
            {
                Sprite sprite = ultimoPremio.Icon;
                string name = (ultimoPremio.Effect != null) ? ultimoPremio.Effect.label : "Recompensa";
                string desc = ultimoPremio.Effect != null ? ultimoPremio.Effect.description : "";
                rewardPopup.ShowReward(sprite, name, desc);
            }
        }

        public void AplicarUltimoPremio()
        {
            if (ultimoPremio != null && ultimoPremio.Effect != null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) ultimoPremio.Effect.Apply(player);
            }
        }

        public void SincronizarSpinsConPlayer(PlayerController player)
        {
            if (player != null && player.extraSpins > 0)
            {
                usosMaximos += player.extraSpins;
                usosRestantes = usosMaximos;
            }
        }

        // --- GENERACIÓN INTERNA (Sin cambios) ---

        private void Generate()
        {
            // Limpiar hijos anteriores
            foreach (Transform child in wheelPiecesParent) Destroy(child.gameObject);
            foreach (Transform child in linesParent) Destroy(child.gameObject);

            // Cálculos de tamaño
            pieceAngle = 360f / wheelPieces.Length;
            halfPieceAngle = pieceAngle / 2f;

            float t = Mathf.InverseLerp(piecesMin, piecesMax, Mathf.Clamp(wheelPieces.Length, piecesMin, piecesMax));
            float pieceWidth = Mathf.Lerp(pieceMaxSize.x, pieceMinSize.x, t);
            float pieceHeight = Mathf.Lerp(pieceMaxSize.y, pieceMinSize.y, t);

            // Bucle de creación de piezas
            for (int i = 0; i < wheelPieces.Length; i++)
            {
                WheelPiece piece = wheelPieces[i];
                GameObject pieceObj = Instantiate(wheelPiecePrefab, wheelPiecesParent);
                Transform pieceTrns = pieceObj.transform.GetChild(0);

                // --- 1. CONFIGURACIÓN DEL ÍCONO Y VFX (RECUPERADO) ---
                Transform iconContainer = pieceTrns.Find("IconContainer");
                if (iconContainer != null)
                {
                    // A) Poner el Sprite
                    Transform iconTransform = iconContainer.Find("Icon");
                    if (iconTransform != null)
                        iconTransform.GetComponent<Image>().sprite = piece.Icon;

                    // B) LOGICA DE VFX (PARTÍCULAS DE FONDO)
                    Transform vfxIconObject = iconContainer.Find("Icon_VFX");
                    if (vfxIconObject != null)
                    {
                        if (piece.Effect != null)
                        {
                            bool vfxActive = false;

                            // Cambiar Material (si usas distintos materiales)
                            ParticleSystemRenderer vfxRenderer = vfxIconObject.GetComponent<ParticleSystemRenderer>();
                            if (piece.Effect.vfxMaterial != null && vfxRenderer != null)
                            {
                                vfxRenderer.material = piece.Effect.vfxMaterial;
                                vfxActive = true;
                            }

                            // Cambiar Color Base (si usas el mismo material pero tintado)
                            ParticleSystem pSystem = vfxIconObject.GetComponent<ParticleSystem>();
                            if (pSystem != null)
                            {
                                var mainModule = pSystem.main;
                                mainModule.startColor = piece.Effect.vfxStartColor;
                                vfxActive = true;
                            }

                            vfxIconObject.gameObject.SetActive(vfxActive);
                        }
                        else
                        {
                            vfxIconObject.gameObject.SetActive(false);
                        }
                    }
                }

                // --- 2. OCULTAR EL LABEL (LO QUE PEDISTE ANTES) ---
                Transform labelTransform = pieceTrns.Find("Label");
                if (labelTransform != null)
                {
                    labelTransform.gameObject.SetActive(false);
                }

                // --- 3. CONFIGURAR CANTIDAD Y TAMAÑO ---
                Transform amountObj = pieceTrns.Find("Amount");
                if (amountObj != null)
                    amountObj.GetComponent<Text>().text = piece.Amount.ToString();

                RectTransform rt = pieceTrns.GetComponent<RectTransform>();
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pieceWidth);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, pieceHeight);
                rt.localScale = Vector3.one;

                // --- 4. ROTACIÓN Y LÍNEAS SEPARADORAS ---
                pieceTrns.RotateAround(wheelPiecesParent.position, Vector3.back, pieceAngle * i);
                Transform lineTrns = Instantiate(linePrefab, linesParent.position, Quaternion.identity, linesParent).transform;
                lineTrns.RotateAround(wheelPiecesParent.position, Vector3.back, (pieceAngle * i) + halfPieceAngle);
            }
        }

        private void CargarPremiosDesdePoolsPonderados()
        {
            PowerUpPool elegido = null;
            if (powerUpPools != null && powerUpPools.Count > 0)
            {
                float total = 0f;
                foreach (var w in powerUpPools) if (w != null && w.pool != null && w.weight > 0f) total += w.weight;
                if (total > 0f)
                {
                    float r = UnityEngine.Random.Range(0f, total);
                    float acc = 0f;
                    foreach (var w in powerUpPools) { acc += w.weight; if (r <= acc) { elegido = w.pool; break; } }
                }
            }
            else elegido = powerUpPool;

            if (elegido == null || elegido.entries == null || elegido.entries.Length == 0) return;

            wheelPieces = new WheelPiece[elegido.entries.Length];
            for (int i = 0; i < elegido.entries.Length; i++)
            {
                PowerUpEntry entry = elegido.entries[i];
                if (entry != null && entry.effect != null)
                {
                    wheelPieces[i] = new WheelPiece { Icon = entry.effect.icon, Label = entry.effect.label, Amount = 1, Chance = entry.chance, Effect = entry.effect };
                }
            }
            Generate();
            CalculateWeightsAndIndices();
        }

        private int GetRandomPieceIndex()
        {
            double r = rand.NextDouble() * accumulatedWeight;
            for (int i = 0; i < wheelPieces.Length; i++) if (r < wheelPieces[i]._weight) return i;
            return 0;
        }

        private void CalculateWeightsAndIndices()
        {
            accumulatedWeight = 0;
            for (int i = 0; i < wheelPieces.Length; i++)
            {
                accumulatedWeight += wheelPieces[i].Chance;
                wheelPieces[i]._weight = accumulatedWeight;
                wheelPieces[i].Index = i; // Importante: guardar el índice original
            }
        }

        public WheelPiece ObtenerUltimoPremio() => ultimoPremio;
        public void AddSpinStartListener(UnityAction callback) => onSpinStartEvent += callback;
        public void AddSpinEndListener(UnityAction<WheelPiece> callback) => onSpinEndEvent += callback;
    }
}