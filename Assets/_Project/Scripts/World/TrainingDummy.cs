using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Combat;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Muñeco de practica: un cuerpo de tela sobre un poste, unido con un resorte (se balancea al recibir golpes).
    /// Tiene vida; al llegar a 0 (o si se consume quemandose) se rompe y vuelve a aparecer a los pocos segundos.
    /// Va en la raiz; el cuerpo (Rigidbody + Health + ElementReactive) es un hijo.
    /// </summary>
    public class TrainingDummy : MonoBehaviour
    {
        public static readonly List<TrainingDummy> All = new List<TrainingDummy>();

        [SerializeField] private Health body;
        [SerializeField] private float respawnSeconds = 4f;

        private ElementReactive reactive;
        private Rigidbody bodyRb;
        private Vector3 bodyLocalPosition;
        private Quaternion bodyLocalRotation;
        private bool respawning;

        private void Awake()
        {
            reactive = body.GetComponent<ElementReactive>();
            bodyRb = body.GetComponent<Rigidbody>();
            bodyLocalPosition = body.transform.localPosition;
            bodyLocalRotation = body.transform.localRotation;
            All.Add(this);
        }

        private void OnDestroy() => All.Remove(this);

        private void OnEnable()
        {
            body.Died += OnDied;
            if (reactive != null) reactive.Reacted += OnReacted;
        }

        private void OnDisable()
        {
            body.Died -= OnDied;
            if (reactive != null) reactive.Reacted -= OnReacted;
        }

        private void OnDied(Health health)
        {
            if (reactive != null && body.gameObject.activeSelf)
                Shatter.Break(reactive, body.Center + Vector3.down * 0.2f, 3f);
            else body.gameObject.SetActive(false);
            Debug.Log($"[Mundo] {name} derribado.");
            ScheduleRespawn();
        }

        private void OnReacted(ElementReactive source, ReactionEvents events)
        {
            // Se consumio quemandose (MagicWorld ya lo rompio): tambien vuelve.
            if ((events & ReactionEvents.Broken) != 0) ScheduleRespawn();
        }

        private void ScheduleRespawn()
        {
            if (respawning) return;
            respawning = true;
            StartCoroutine(RespawnLater());
        }

        private IEnumerator RespawnLater()
        {
            yield return new WaitForSeconds(respawnSeconds);
            Restore();
        }

        /// <summary>Muñeco nuevo en su lugar (tambien lo usa el reinicio del mundo).</summary>
        public void Restore()
        {
            StopAllCoroutines();
            respawning = false;
            if (reactive != null) reactive.ResetState();
            body.gameObject.SetActive(true);
            body.transform.localPosition = bodyLocalPosition;
            body.transform.localRotation = bodyLocalRotation;
            if (bodyRb != null && !bodyRb.isKinematic)
            {
                bodyRb.linearVelocity = Vector3.zero;
                bodyRb.angularVelocity = Vector3.zero;
            }
            body.ResetHealth();
        }
    }
}
