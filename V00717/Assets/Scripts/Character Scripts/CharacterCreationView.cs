﻿using System;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.AI;

// Deals with view render changes
public class CharacterCreationView : MonoBehaviour
{   
    public CreationController CreationController;
    public GameObject characterModelPrefab;
    public GameObject newCharacterModelInstance;

    // The Baby Model GOs to render/change mesh, and the scriptable object to pull data from
    private Renderer BabyModelHeadRenderer;
    private Renderer BabyModelTorsoRenderer;
    public MeshFilter BabyModelHeadMeshFilter;
    public MeshFilter BabyModelTorsoMeshFilter;    
    public GameController GameController;
    // Scriptable object with assets
    public ModelAssets ModelAssets;

    // Unique colonist personnel ID in colonist creation screen
    public TMP_Text uniqueColonistPersonnelID_CC;

    // Delegate for changing sex
    public delegate void SexChangeAction(string sex);
    public static event SexChangeAction _OnSexChanged; // listened to by SoundController.cs

    public static Vector3 characterModelPrefabCoords = Vector3.zero;
    public static Vector3 characterModelPrefabInstanceCoords = Vector3.zero;

    // Queue of editors to spawn (and destroy later if unrequired)
    public Queue<GameObject> auditionEditorToSpawn;

    // Attach the event listeners
    public void OnEnable()
    {
        SaveSystem._SuccessfulSaveAction += UpdateColonistUUIDText;
    }

    // Detach the event listeners
    public void OnDisable()
    {
        SaveSystem._SuccessfulSaveAction -= UpdateColonistUUIDText;
    }

    // Updates the colonist uuid text on start
    private void Start()
    {
        // Create a new character template mesh
        GameController = GameObject.FindObjectOfType<GameController>();
        CreationController = GameController.CreationController;
        characterModelPrefabCoords = new Vector3(-2.14f, 0.75f, 2.9f);
        characterModelPrefabInstanceCoords = new Vector3(2.14f, 0.75f, characterModelPrefabCoords.z += 50.0f);

        BabyModelHeadRenderer = newCharacterModelInstance.GetComponent<Renderer>();
        BabyModelTorsoRenderer = newCharacterModelInstance.GetComponent<Renderer>();

        BabyModelHeadMeshFilter = newCharacterModelInstance.GetComponent<MeshFilter>();
        BabyModelTorsoMeshFilter = newCharacterModelInstance.GetComponent<MeshFilter>();
        // Setup its camera and render texture
        UpdateColonistUUIDText();
        auditionEditorToSpawn = new Queue<GameObject>();
}

    // Setter for new colonist nickname
    public void OnNickNameChanged(string nickName)
    {
        newCharacterModelInstance.GetComponent<CharacterModel>().NickName = nickName;
        GetComponent<AuditionEditor>().SetNameChoice(nickName);
        Debug.Log($"And so {nickName} was given his nickname.");
    }
    // Updates the colonist uuid text in identification tab
    public void UpdateColonistUUIDText()
    {
        if(uniqueColonistPersonnelID_CC)
        {
            uniqueColonistPersonnelID_CC.SetText($"Unique Actor Union ID: {CharacterModelObject.uniqueColonistPersonnelID}");
        }
    }
    // Views update itself concerning head/torso changes in UI (type == 0-4 : head mesh, type 4-8 : torso mesh)
    // Changes the mesh of the baby model by accessing its mesh filter setter and mesh property
    public void UpdateMesh(int meshIndex)
    {
        int capacity = ModelAssets.heads.Capacity;
        // Capacity (4 for now) is the last head mesh index, so every index larger to that is a torso
        List<MeshFilter> meshList = meshIndex < capacity ? ModelAssets.heads : ModelAssets.torsos;
        MeshFilter? meshFilter = meshList[meshIndex % capacity];
        Mesh newMesh = null;
        
        if(meshFilter != null)
        {
            newMesh = meshFilter.sharedMesh;
        }
        else
        {
            return;
        }
        // Transform Child 1 are torsos, child 0 are heads
        if (meshIndex < capacity)
        {
            BabyModelHeadMeshFilter.mesh = newMesh;
        }
        else
        {
            BabyModelTorsoMeshFilter.mesh = newMesh;
        }
    }

    // Called from creation menu (cast button)
    public void AddNewColonistToRegistry()
    {
        if (!GameController.NumberOfCharactersBelowMax())
        {
            Debug.Log("max characters already");
            return;
        }
        CharacterModelObject.uniqueColonistPersonnelID++;
        SetupNewCharacter();
        GameController.Save();
        GetComponent<CreationMenuController>().DestroyEditor();

        if(GameController.NumberOfCharactersBelowMax())
        {
            GameController.StartAuditionsAfterDelay(1);
        } else
        {
            GameController.SetupQuadrantSelectionPhase();
            GameController.seasonController.EndAuditions();
            GameController.auditionStatus.enabled = true;
            StartCoroutine(GameController.CloseAfterDelay(GameController.CloseSpecialEventsWindow, 5.0f));
            // Clear any pending audition window to spawn
            foreach(GameObject g in GameController.auditionEditorsInGame)
            {
                Destroy(g.gameObject);
            }
        }
    }

    public void SetupNewCharacter()
    {
        CreationController creationController = GameController.CreationController;

        // Set the new Material runner games character to the last track position (set from live game character count)
        int trackLanePosition = creationController.FindAvailableCameraLane();

        GameObject newCharacterMesh = newCharacterModelInstance;
        CharacterModel newCharacterModel = newCharacterMesh.GetComponent<CharacterModel>();
        try
        {
            newCharacterModel.UniqueColonistPersonnelID_ = CharacterModelObject.uniqueColonistPersonnelID;
            RenameGameObject(newCharacterMesh, newCharacterModel.NickName);
            AddGameObjectToList(newCharacterMesh);
            SetupTransformPosition(newCharacterMesh.transform, GameController.landingPositions[trackLanePosition].transform.position);
            newCharacterMesh.transform.SetParent(GameController.landingPositions[trackLanePosition].transform);
            SetCameraTarget(creationController, newCharacterMesh.transform, trackLanePosition);
            GameController.SetupStartingQuadrant(newCharacterMesh);
            // Setup dragged action handler with this actor's UUID
            // TODO should match with UUID (if delete character and reload game, this won't be nice)
            for (int i = 0; i < GameController.draggedActionHandlers.Length; i++)
            {
                int actionActorTargetUUID = GameController.draggedActionHandlers[i].ActionActorTargetUUID;
                if (actionActorTargetUUID != -1)
                {
                    continue;
                }
                GameController.draggedActionHandlers[i].ActionActorTargetUUID = newCharacterModel.UniqueColonistPersonnelID_;
                GameController.cameraFeedActorNameLabels[i].GetComponent<TMP_Text>().SetText(newCharacterModel.NickName);
                break;
            }
        }
        catch (ArgumentNullException ane)
        {
            Debug.Log(ane.Message);
            Debug.LogError("Error: No prefab model for characters loaded.");
        }
        catch (ArgumentException ae)
        {
            Debug.LogError(ae.Message);
        }
    }

    public void RenameGameObject(GameObject renamed, string newName)
    {
        renamed.gameObject.name = newName;
    }
    public void AddGameObjectToList(GameObject gameObject)
    {
       GameController.Colonists.Add(gameObject);
    }
    public void SetCameraTarget(CreationController cameraController, Transform target, int trackLanePosition)
    {
        // TODO Set its mesh to the players' choices using the character model component        
        cameraController.SetTrackLanePosition(trackLanePosition, target);
    }
    public void SetupTransformPosition(Transform moved, Vector3 position)
    {
        moved.position = position;
    }
}
