using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class EscapeRoomAgent : Agent
{
    [SerializeField] private Transform interrupteurTransform;
    [SerializeField] private Transform sortieTransform;
    [SerializeField] private GameObject porte;
    [SerializeField] private float speed = 5f;
    [SerializeField] private Renderer solRenderer;
    [SerializeField] private Material materielSucces;
    [SerializeField] private Material materielEchec;
    [SerializeField] private bool modeAleatoire = false;

    private Rigidbody rb;
    private bool porteOuverte = false;
    Vector3 positionSortie;
    Material matActuel;

    private float lastDistanceToInterrupteur;
    private float lastDistanceToSortie;


    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody manquant sur l'agent. Ajoute un composant Rigidbody.");
        }
        positionSortie = sortieTransform.localPosition;
        matActuel = solRenderer.material;


    }

    void FixedUpdate()
    {
        Vector3 pos = transform.position;

        bool horsLimite =
            pos.y < -1f || pos.y > 5f; // trop bas ou trop haut
            

        if (horsLimite)
        {
            Debug.Log("Agent hors zone : " + pos);
            AddReward(-1f); // Punition car il est sorti de la scène
            EndEpisode();
        }
    }




    private Vector3 GenererPositionDansZone()
    {
        float minX = -3.7f;
        float maxX = 5.0f;
        float minZ = -2.8f;
        float maxZ = 2.4f;

        return new Vector3(
            Random.Range(minX, maxX),
            0.5f,
            Random.Range(minZ, maxZ)
        );
    }



    public override void OnEpisodeBegin()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (modeAleatoire)
        {
            transform.localPosition = GenererPositionDansZone();
            interrupteurTransform.localPosition = GenererPositionDansZone();
        }
        else
        {
            // Position fixe pour entraînement de base
            transform.localPosition = new Vector3(-3f, 0f, 0.8f);
            interrupteurTransform.localPosition = new Vector3(3.9f, 0.6f, -0.10f);
        }


        porte.SetActive(true);
        porteOuverte = false;

        lastDistanceToInterrupteur = Vector3.Distance(transform.localPosition, interrupteurTransform.localPosition);
        lastDistanceToSortie = Vector3.Distance(transform.localPosition, sortieTransform.localPosition);
    }


    // il voit
    public override void CollectObservations(VectorSensor sensor)
    {
        //position de l'agent
        sensor.AddObservation(transform.localPosition);
        //position de l'interrupteur
        sensor.AddObservation(interrupteurTransform.localPosition);

        Vector3 dirToInterrupteur = interrupteurTransform.localPosition - transform.localPosition;
        sensor.AddObservation(dirToInterrupteur.normalized);

        if (porteOuverte)
        {
            sensor.AddObservation(sortieTransform.localPosition);
            Vector3 dirToSortie = sortieTransform.localPosition - transform.localPosition;
            sensor.AddObservation(dirToSortie.normalized);
        }
    }

    //il fait

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        Vector3 move = new Vector3(moveX, 0, moveZ);
        Vector3 newPosition = rb.position + move * Time.fixedDeltaTime * speed;

        rb.MovePosition(newPosition);

        AddReward(-0.001f); // pénalité temporelle

        if (!porteOuverte)
        {
            float dist = Vector3.Distance(transform.localPosition, interrupteurTransform.localPosition);

            if (dist < lastDistanceToInterrupteur)
            {
                AddReward(0.003f); // se rapproche → bonus
            }
            else
            {
                AddReward(-0.005f); // s’éloigne → punition
            }

            lastDistanceToInterrupteur = dist;
        }
        else
        {
            float dist = Vector3.Distance(transform.localPosition, sortieTransform.localPosition);

            if (dist < lastDistanceToSortie)
            {
                AddReward(0.003f); // va vers sortie
            }
            else
            {
                AddReward(-0.005f); // s’en éloigne
            }

            lastDistanceToSortie = dist;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Collision avec: " + other.tag);

        if (other.CompareTag("free"))
        {
            Debug.Log("Interrupteur activé");
            // Inverser l'état de la porte
            porteOuverte = !porteOuverte;
            porte.SetActive(!porteOuverte);

            if (porteOuverte)
            {
                AddReward(0.5f); // Récompense pour avoir ouvert la porte
            }
            else
            {
                AddReward(-0.5f); // Punition pour l'avoir refermée
            }
        }
        else if (other.CompareTag("fin"))
        {
            Debug.Log("Sortie atteinte");
            if (porteOuverte)
            {
                Debug.Log("Succès!");
                AddReward(1.5f); // Succès
                solRenderer.material = materielSucces;
            }
            else
            {
                Debug.Log("Échec: porte fermée");
                AddReward(-1f); // Tentative de sortie par porte fermée
                solRenderer.material = materielEchec;
            }
            EndEpisode();
        }
        else if (other.CompareTag("obstacle"))
        {
            Debug.Log("Collision avec obstacle");
            AddReward(-1f); // Collision avec un mur
            solRenderer.material = materielEchec;
            EndEpisode();
        }
        else if (other.CompareTag("porte") && !porteOuverte)
        {
            Debug.Log("Collision avec porte fermée");
            AddReward(-1f); // Collision avec la porte fermée
            solRenderer.material = materielEchec;
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;
        actions[0] = Input.GetAxis("Horizontal");
        actions[1] = Input.GetAxis("Vertical");
    }
}
