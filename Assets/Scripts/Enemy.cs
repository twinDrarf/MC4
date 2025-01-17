using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PhotonTransformView))]
public class Enemy : LivingEntity
{
    public enum State { Idle, Chasing, Attacking };
    State currentState;

    public PhotonView PV;
    public ParticleSystem deathEffect;

    NavMeshAgent pathfinder;
    Transform target;
    LivingEntity targetEntity;
    Material skinMaterial;

    Color originalColour;

    float attackDistanceThreshold = .5f;
    float timeBetweenAttacks = 1;
    float damage = 1;

    float nextAttackTime;
    float myCollisionRadius;
    float targetCollisionRadius;

    bool hasTarget;

    public Gun gun0;
    public Gun gun1;
    public Gun gun2;
    public Gun gun3;

    List<Transform> players; // player list

    protected override void Start()
    {
        base.Start();
        pathfinder = GetComponent<NavMeshAgent>();
        skinMaterial = GetComponent<Renderer>().material;
        originalColour = skinMaterial.color;

        // All players
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        players = new List<Transform>();
        foreach (GameObject playerObject in playerObjects)
        {
            players.Add(playerObject.transform);
        }

        if (players.Count > 0)
        {
            currentState = State.Chasing;
            hasTarget = true;
            target = GetClosestPlayer();
            targetEntity = target.GetComponent<LivingEntity>();
            targetEntity.OnDeath += OnTargetDeath;

            myCollisionRadius = GetComponent<CapsuleCollider>().radius;
            targetCollisionRadius = target.GetComponent<CapsuleCollider>().radius;

            StartCoroutine(UpdatePath());
        }
    }

    public override void TakeHit(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        PV.RPC("TakeHitRPC", RpcTarget.All, damage, hitPoint, hitDirection);
    }
    
    [PunRPC]
    void TakeHitRPC(float damage, Vector3 hitPoint, Vector3 hitDirection) // what really happened in TakeHit
    {
        int randNum = Random.Range(1, 8); // drop weapons
        if (damage >= health)
        {
            Destroy(Instantiate(deathEffect, hitPoint, Quaternion.FromToRotation(Vector3.forward, hitDirection)), deathEffect.main.startLifetime.constant);
            if(randNum == 1)
            {
               PhotonNetwork.Instantiate(gun0.name, hitPoint, Quaternion.FromToRotation(Vector3.forward, hitDirection));
            }
            else if (randNum == 2)
            {
                PhotonNetwork.Instantiate(gun1.name, hitPoint, Quaternion.FromToRotation(Vector3.forward, hitDirection));
            }
            else if (randNum == 3)
            {
                PhotonNetwork.Instantiate(gun2.name, hitPoint, Quaternion.FromToRotation(Vector3.forward, hitDirection));
            }
            else if (randNum == 4)
            {
                PhotonNetwork.Instantiate(gun3.name, hitPoint, Quaternion.FromToRotation(Vector3.forward, hitDirection));
            }
        }
        base.TakeHit(damage, hitPoint, hitDirection);
    }

    void OnTargetDeath()
    {
        hasTarget = false;
        currentState = State.Idle;
    }

    void Update()
    {
        Transform closestPlayer = GetClosestPlayer();
        if (closestPlayer != null)
        {
            target = closestPlayer;
        }

        if (hasTarget)
        {
            if (Time.time > nextAttackTime)
            {
                float sqrDstToTarget = (target.position - transform.position).sqrMagnitude;
                if (sqrDstToTarget < Mathf.Pow(attackDistanceThreshold + myCollisionRadius + targetCollisionRadius, 2))
                {
                    nextAttackTime = Time.time + timeBetweenAttacks;
                    StartCoroutine(Attack());
                }
            }
        }
    }

    Transform GetClosestPlayer()
    {
        Transform closestPlayer = null;
        float closestDistance = float.MaxValue;

        foreach (Transform playerTransform in players) // compare dists
        {
            if (playerTransform != null) // if player is alive
            {
                float distance = Vector3.Distance(transform.position, playerTransform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = playerTransform;
                }
            }
        }
        return closestPlayer;
    }

    IEnumerator Attack()
    {
        currentState = State.Attacking;
        pathfinder.enabled = false;

        Vector3 originalPosition = transform.position;
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        Vector3 attackPosition = target.position - dirToTarget * (myCollisionRadius);

        float attackSpeed = 3;
        float percent = 0;

        skinMaterial.color = Color.red;
        bool hasAppliedDamage = false;

        while (percent <= 1)
        {
            if (percent >= .5f && !hasAppliedDamage)
            {
                hasAppliedDamage = true;
                targetEntity.TakeDamage(damage);
            }
            percent += Time.deltaTime * attackSpeed;
            float interpolation = (-Mathf.Pow(percent, 2) + percent) * 4;
            transform.position = Vector3.Lerp(originalPosition, attackPosition, interpolation);
            yield return null;
        }

        skinMaterial.color = originalColour;
        currentState = State.Chasing;
        pathfinder.enabled = true;
    }

    IEnumerator UpdatePath()
    {
        float refreshRate = 0.25f;

        while (hasTarget)
        {
            if (currentState == State.Chasing)
            {
                Vector3 dirToTarget = (target.position - transform.position).normalized;
                Vector3 targetPosition = target.position - dirToTarget * (myCollisionRadius + targetCollisionRadius + attackDistanceThreshold / 2);
                if (!dead)
                {
                    pathfinder.SetDestination(targetPosition);
                }
            }
            yield return new WaitForSeconds(refreshRate);
        }
    }
}
