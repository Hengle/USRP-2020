﻿using System.Collections.Generic;
using UnityEngine;

public class Animal : LivingEntity
{
    public const int maxViewDistance = 10;

    [EnumFlags]
    public Species diet;

    public CreatureAction currentAction;
    public Genes genes;
    public Color maleColour;
    public Color femaleColour;

    // Settings
    float timeBetweenActionChoices = 1;
    float moveSpeed = 1.5f;
    float timeToDeathByHunger = 200;
    float timeToDeathByThirst = 200;
    float staminaTimeFactor = 150;
    float desireTimeFactor = 400;

    float drinkDuration = 6;
    float eatDuration = 10;
    float restDuration = 14;

    float criticalPercent = 0.5f;

    // Visual settings
    float moveArcHeight = .2f;

    [Header("State")]
    public float hunger;
    public float thirst;
    public float stamina;
    public float desire;

    public float[] states = new float[sizeof(AnimalStates)];

    private enum AnimalStates
    {
        Hunger,
        Thirst,
        Exhaustion,
        ReproductiveUrge
    }

    // Used for targeting movement
    protected LivingEntity foodTarget;
    protected Coord waterTarget;
    protected Animal mateTarget;

    // Movement data
    bool animatingMovement;
    Coord moveFromCoord;
    Coord moveTargetCoord;
    Vector3 moveStartPos;
    Vector3 moveTargetPos;
    float moveTime;
    float moveSpeedFactor;
    float moveArcHeightFactor;
    Coord[] path;
    int pathIndex;

    // Other
    float lastActionChooseTime;
    const float sqrtTwo = 1.4142f;
    const float oneOverSqrtTwo = 1 / sqrtTwo;

    public override void Init(Coord coord)
    {
        base.Init(coord);
        moveFromCoord = coord;
        genes = Genes.RandomGenes(1);

        material.color = (genes.isMale) ? maleColour : femaleColour;

        ChooseNextAction();
    }

    protected virtual void Update()
    {
        // Increase stats over time; these influence what the animals does.
        hunger += Time.deltaTime * 1 / timeToDeathByHunger;
        thirst += Time.deltaTime * 1 / timeToDeathByThirst;
        stamina += Time.deltaTime * 1 / staminaTimeFactor;
        desire += Time.deltaTime * 1 / desireTimeFactor;

        // Animate movement. After moving a single tile, the animal will be able to choose its next action.
        if (animatingMovement)
        {
            AnimateMove();
        }
        else
        {
            // Handle interactions with external things, like food, water, mates.
            HandleInteractions();
            float timeSinceLastActionChoice = Time.time - lastActionChooseTime;
            if (timeSinceLastActionChoice > timeBetweenActionChoices)
            {
                ChooseNextAction();
            }
        }

        if (hunger >= 1)
        {
            Die(CauseOfDeath.Hunger);
        }
        else if (thirst >= 1)
        {
            Die(CauseOfDeath.Thirst);
        }
        else if (stamina >= 1)
        {
            Die(CauseOfDeath.Age);
        }
    }

    // Animals choose their next action after each movement step (1 tile),
    // or, when not moving (e.g interacting with food etc), at a fixed time interval.
    protected virtual void ChooseNextAction()
    {
        lastActionChooseTime = Time.time;

        bool currentlyEating = currentAction == CreatureAction.Eating && foodTarget && hunger > 0;
        bool currentlyDrinking = currentAction == CreatureAction.Drinking && waterTarget != null && thirst > 0;
        bool currentlyResting = currentAction == CreatureAction.Resting && stamina > 0;

        if (!currentlyResting)



        if (hunger >= thirst || currentlyEating && thirst < criticalPercent)
        {
            FindFood();
        }
        else
        {
            FindWater();
        }

        Act();
    }

    protected virtual void FindFood()
    {
        LivingEntity foodSource = Environment.SenseFood(coord, this, FoodPreferencePenalty);
        if (foodSource)
        {
            currentAction = CreatureAction.GoingToFood;
            foodTarget = foodSource;
            CreatePath(foodTarget.coord);

        }
        else
        {
            currentAction = CreatureAction.Exploring;
        }
    }

    protected virtual void FindWater()
    {
        Coord waterTile = Environment.SenseWater(coord);
        if (waterTile != Coord.invalid)
        {
            currentAction = CreatureAction.GoingToWater;
            waterTarget = waterTile;
            CreatePath(waterTarget);

        }
        else
        {
            currentAction = CreatureAction.Exploring;
        }
    }

    protected virtual void FindPotentialMates()
    {
        List<Animal> potentialMates = Environment.SensePotentialMates(coord, this);
        if (potentialMates.Count > 0)
        {
            currentAction = CreatureAction.SearchingForMate;
            mateTarget = potentialMates[Random.Range(0, potentialMates.Count)];
            CreatePath(mateTarget.coord);
        }
        else
        {
            currentAction = CreatureAction.Exploring;
        }
    }

    // When choosing from multiple food sources, the one with the lowest penalty will be selected.
    protected virtual int FoodPreferencePenalty(LivingEntity self, LivingEntity food)
    {
        return Coord.SqrDistance(self.coord, food.coord);
    }

    protected void Act()
    {
        switch (currentAction)
        {
            case CreatureAction.Exploring:
                StartMoveToCoord(Environment.GetNextTileWeighted(coord, moveFromCoord));
                break;
            case CreatureAction.GoingToFood:
                if (Coord.AreNeighbours(coord, foodTarget.coord))
                {
                    LookAt(foodTarget.coord);
                    currentAction = CreatureAction.Eating;
                }
                else
                {
                    StartMoveToCoord(path[pathIndex]);
                    pathIndex++;
                }
                break;
            case CreatureAction.GoingToWater:
                if (Coord.AreNeighbours(coord, waterTarget))
                {
                    LookAt(waterTarget);
                    currentAction = CreatureAction.Drinking;
                }
                else
                {
                    StartMoveToCoord(path[pathIndex]);
                    pathIndex++;
                }
                break;
            case CreatureAction.SearchingForMate:
                if (Coord.AreNeighbours(coord, mateTarget.coord))
                {
                    LookAt(mateTarget.coord);
                    currentAction = CreatureAction.Mating;
                }
                else
                {
                    StartMoveToCoord(path[pathIndex]);
                    pathIndex++;
                }
                break;
        }
    }

    protected void CreatePath(Coord target)
    {
        // Create new path if current is not already going to target
        if (path == null || pathIndex >= path.Length || (path[path.Length - 1] != target || path[pathIndex - 1] != moveTargetCoord))
        {
            path = EnvironmentUtility.GetPath(coord.x, coord.y, target.x, target.y);
            pathIndex = 0;
        }
    }

    protected void StartMoveToCoord(Coord target)
    {
        moveFromCoord = coord;
        moveTargetCoord = target;
        moveStartPos = transform.position;
        moveTargetPos = Environment.tileCentres[moveTargetCoord.x, moveTargetCoord.y];
        animatingMovement = true;

        bool diagonalMove = Coord.SqrDistance(moveFromCoord, moveTargetCoord) > 1;
        moveArcHeightFactor = (diagonalMove) ? sqrtTwo : 1;
        moveSpeedFactor = (diagonalMove) ? oneOverSqrtTwo : 1;

        LookAt(moveTargetCoord);
    }

    protected void LookAt(Coord target)
    {
        if (target != coord)
        {
            Coord offset = target - coord;
            transform.eulerAngles = Vector3.up * Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
        }
    }

    void HandleInteractions()
    {
        switch (currentAction)
        {
            case CreatureAction.Eating:
                if (foodTarget && hunger > 0)
                {
                    float eatAmount = Mathf.Min(hunger, Time.deltaTime * 1 / eatDuration);

                    if (foodTarget is Plant)
                    {
                        eatAmount = ((Plant)foodTarget).Consume(eatAmount);
                    }
                    else if (foodTarget is Animal)
                    {
                        ((Animal)foodTarget).Die(CauseOfDeath.Eaten);
                    }

                    hunger -= eatAmount;
                }
                break;
            case CreatureAction.Drinking:
                if (thirst > 0)
                {
                    thirst -= Time.deltaTime * 1 / drinkDuration;
                    thirst = Mathf.Clamp01(thirst);
                }
                break;
            case CreatureAction.Resting:
                if (stamina > 0)
                {
                    stamina -= Time.deltaTime * 1 / restDuration;
                    stamina = Mathf.Clamp01(stamina);
                }
                break;
            case CreatureAction.Mating:
                if (mateTarget && desire > 0)
                {
                    desire = 0;
                    var entity = Instantiate(this);
                    entity.Init(coord);
                    Environment.speciesMaps[entity.species].Add(entity, coord);
                }
                break;
        }
    }

    void AnimateMove()
    {
        // Move in an arc from start to end tile.
        moveTime = Mathf.Min(1, moveTime + Time.deltaTime * moveSpeed * moveSpeedFactor);
        float height = (1 - 4 * (moveTime - .5f) * (moveTime - .5f)) * moveArcHeight * moveArcHeightFactor;
        transform.position = Vector3.Lerp(moveStartPos, moveTargetPos, moveTime) + Vector3.up * height;

        // Finished moving.
        if (moveTime >= 1)
        {
            Environment.RegisterMove(this, coord, moveTargetCoord);
            coord = moveTargetCoord;

            animatingMovement = false;
            moveTime = 0;
            ChooseNextAction();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            var surroundings = Environment.Sense(coord);
            Gizmos.color = Color.white;
            if (surroundings.nearestFoodSource != null)
            {
                Gizmos.DrawLine(transform.position, surroundings.nearestFoodSource.transform.position);
            }
            if (surroundings.nearestWaterTile != Coord.invalid)
            {
                Gizmos.DrawLine(transform.position, Environment.tileCentres[surroundings.nearestWaterTile.x, surroundings.nearestWaterTile.y]);
            }

            if (currentAction == CreatureAction.GoingToFood)
            {
                var path = EnvironmentUtility.GetPath(coord.x, coord.y, foodTarget.coord.x, foodTarget.coord.y);
                Gizmos.color = Color.black;
                for (int i = 0; i < path.Length; i++)
                {
                    Gizmos.DrawSphere(Environment.tileCentres[path[i].x, path[i].y], .2f);
                }
            }
        }
    }

}