// MIT License
//
// Copyright (c) 2025 Maetel
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Unity.Mathematics;
using MagicaCloth2;

public class DynamicBoneToMagicaClothConverter : EditorWindow
{
    const float DISTANCE_STIFFNESS_APPROX_FACTOR = 0.1f;
    private static readonly List<string> boneSpringKeywords = new List<string>
    {
        "breast",
        "boob",
        "bust",
        // Add more keywords here as needed
    };

    [MenuItem("Tools/Convert DB to MagicaCloth V2/Hierarchy - Convert All (Warn on Exclusions)", false, 0)]
    [MenuItem("GameObject/Convert Hierarchy to MagicaCloth V2/Convert All (Warn on Exclusions)", false, 0)]
    static void ConvertHierarchyAll()
    {
        InitiateHierarchyConversion(false);
    }

    [MenuItem("Tools/Convert DB to MagicaCloth V2/Hierarchy - Skip Components with Exclusions", false, 1)]
    [MenuItem("GameObject/Convert Hierarchy to MagicaCloth V2/Skip Components with Exclusions", false, 1)]
    static void ConvertHierarchySkipExclusions()
    {
        InitiateHierarchyConversion(true);
    }

    private static void InitiateHierarchyConversion(bool skipWithExclusions)
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Conversion Error",
                "No GameObject selected. Please select the root GameObject containing Dynamic Bone components.", "OK");
            return;
        }

        DynamicBone[] dynamicBones = selectedObject.GetComponentsInChildren<DynamicBone>(true);

        if (dynamicBones.Length == 0)
        {
            EditorUtility.DisplayDialog("Conversion Info",
                $"No Dynamic Bone components found in the hierarchy of '{selectedObject.name}'.", "OK");
            return;
        }

        string actionDescription = skipWithExclusions
            ? "convert all components EXCEPT those with Exclusions"
            : "convert ALL components (and warn about those with Exclusions)";

        bool proceedConversion = EditorUtility.DisplayDialog(
            "Confirm Hierarchy Conversion",
            $"Found {dynamicBones.Length} Dynamic Bone component(s) in the hierarchy of '{selectedObject.name}'.\n\n" +
            $"This will {actionDescription}.\n\n" +
            "This operation can be undone. Do you want to proceed?",
            "Yes, Convert",
            "Cancel");

        if (!proceedConversion)
        {
            return;
        }

        // Find or create the container object for the new MagicaCloth components
        Transform containerTransform = selectedObject.transform.Find("MC2");
        GameObject mc2Container;
        if (containerTransform == null)
        {
            mc2Container = new GameObject("MC2");
            Undo.RegisterCreatedObjectUndo(mc2Container, "Create MC2 Container");
            mc2Container.transform.SetParent(selectedObject.transform, false); // Place it at the root of the selection
        }
        else
        {
            mc2Container = containerTransform.gameObject;
        }

        ConvertDynamicBones(dynamicBones, selectedObject.name + " (Hierarchy)", skipWithExclusions, mc2Container);
        CleanupRemainingDynamicBones(new GameObject[] { selectedObject });
    }

    [MenuItem("CONTEXT/DynamicBone/Convert This Component to MagicaCloth V2", false, 0)]
    static void ConvertSingleComponent(MenuCommand command)
    {

        DynamicBone db = command.context as DynamicBone;
        if (db == null)
        {
            Debug.LogError("Failed to get DynamicBone component from context for conversion.");
            return;
        }

        string exclusionWarning = "";
        if (db.m_Exclusions != null && db.m_Exclusions.Count > 0)
        {
            exclusionWarning = "\n\nWARNING: This component uses Exclusions. These will NOT be automatically converted and require manual setup in MagicaCloth.";
        }

        bool proceed = EditorUtility.DisplayDialog(
            "Confirm Single Conversion",
            $"Convert the Dynamic Bone component on '{db.gameObject.name}' to MagicaCloth V2?{exclusionWarning}\n\n" +
            "This will remove the Dynamic Bone component and its associated Dynamic Bone Colliders, replacing them with MagicaCloth equivalents.\n\n" +
            "Mapping is APPROXIMATE. Manual tuning WILL be required.\n\n" +
            "Proceed?",
            "Yes, Convert This Component",
            "Cancel");

        if (!proceed)
        {
            return;
        }

        // Find the hierarchy root to place the "MC2" container
        Transform root = db.transform.root;

        // Find or create the container object at the root
        Transform containerTransform = root.Find("MC2");
        GameObject mc2Container;
        if (containerTransform == null)
        {
            mc2Container = new GameObject("MC2");
            Undo.RegisterCreatedObjectUndo(mc2Container, "Create MC2 Container");
            mc2Container.transform.SetParent(root, false);
        }
        else
        {
            mc2Container = containerTransform.gameObject;
        }

        DynamicBone[] dynamicBonesToConvert = new DynamicBone[] { db };
        // For single component conversion, the user is explicitly targeting it. We always attempt conversion (never skip).
        ConvertDynamicBones(dynamicBonesToConvert, db.gameObject.name + " (Single Component)", false, mc2Container);
        CleanupRemainingDynamicBones(new GameObject[] { db.gameObject });
    }

    public static void ConvertDynamicBones(DynamicBone[] dynamicBones, string contextName, bool skipWithExclusions, GameObject magicaClothContainer)
    {
        int convertedCount = 0;
        int skippedCount = 0;
        Undo.SetCurrentGroupName("Convert DB to MagicaCloth V2");
        int group = Undo.GetCurrentGroup();

        foreach (DynamicBone db in dynamicBones)
        {
            GameObject targetGo = db.gameObject;

            // Check for exclusions and skip if requested by the user
            if (skipWithExclusions && db.m_Exclusions != null && db.m_Exclusions.Count > 0)
            {
                List<string> exclusionNames = new List<string>();
                foreach (Transform exclusion in db.m_Exclusions)
                {
                    if (exclusion != null) {
                        exclusionNames.Add(exclusion.name);
                    }
                }
                Debug.Log($"SKIPPING: Dynamic Bone on '{targetGo.name}' was skipped because it uses Exclusions: [{string.Join(", ", exclusionNames)}]. " +
                          "This component was not converted or removed.", targetGo);
                skippedCount++;
                continue; // Skip to the next DynamicBone
            }

            // Create a new child GameObject inside the container for this specific MagicaCloth component
            GameObject newMcGo = new GameObject($"MC_{targetGo.name}");
            Undo.RegisterCreatedObjectUndo(newMcGo, $"Create MC GameObject for {targetGo.name}");

            // Match the transform of the original GameObject that held the DynamicBone component
            newMcGo.transform.position = targetGo.transform.position;
            newMcGo.transform.rotation = targetGo.transform.rotation;
            newMcGo.transform.localScale = targetGo.transform.localScale;

            // Parent it to the container, preserving its new world position
            newMcGo.transform.SetParent(magicaClothContainer.transform, true);
            // Add the new MagicaCloth component to the newly created child GameObject
            MagicaCloth mc = Undo.AddComponent<MagicaCloth>(newMcGo);
            if (mc == null)
            {
                Debug.LogError($"Failed to add MagicaCloth component to the new GameObject '{newMcGo.name}'. Skipping conversion for DB on '{targetGo.name}'.", newMcGo);
                Undo.DestroyObjectImmediate(newMcGo); // Clean up the failed GameObject
                continue;
            }

            var sdata = mc.SerializeData;
             if (sdata == null) {
                 Debug.LogError($"Failed to access or initialize SerializeData for MagicaCloth on '{newMcGo.name}'. Skipping. You might need to manually add/configure this component.", newMcGo);
                 Undo.DestroyObjectImmediate(newMcGo); // Clean up the failed GameObject and its component
                 continue;
            }

            // Determine ClothType: Default to BoneCloth, switch to BoneSpring if keywords match
            sdata.clothType = ClothProcess.ClothType.BoneCloth; // Default
            foreach (string keyword in boneSpringKeywords)
            {
                if (targetGo.name.ToLowerInvariant().Contains(keyword.ToLowerInvariant()))
                {
                    sdata.clothType = ClothProcess.ClothType.BoneSpring;
                    break; 
                }
            }

            
            sdata.rootBones.Clear();
            if (db.m_Root != null)
            {
                sdata.rootBones.Add(db.m_Root);
            }
            else if (db.m_Roots != null && db.m_Roots.Count > 0)
            {
                foreach (Transform root in db.m_Roots)
                {
                    if (root != null) sdata.rootBones.Add(root);
                }
            }

            if (sdata.rootBones.Count == 0)
            {
                 sdata.rootBones.Add(targetGo.transform);
                 Debug.LogWarning($"Dynamic Bone on '{targetGo.name}' had no Root specified. Defaulting MagicaCloth Root Bone to the component's transform itself. Verify this is correct.", targetGo);
            }


            
            if (db.m_Exclusions != null && db.m_Exclusions.Count > 0)
            {
                 List<string> exclusionNames = new List<string>();
                 foreach (Transform exclusion in db.m_Exclusions)
                 {
                     if (exclusion != null) {
                         exclusionNames.Add(exclusion.name);
                     }
                 }
                 Debug.LogWarning($"WARNING: Dynamic Bone (InstanceID: {db.GetInstanceID()}) on '{targetGo.name}' uses Exclusions: [{string.Join(", ", exclusionNames)}]. " +
                                  "The conversion will proceed for this component, but you MUST manually replicate this exclusion logic in MagicaCloth " +
                                  "(e.g., using vertex paint, adjusting root bones, or using MagicaCloth's exclusion features if available).", targetGo);
             }

            
            float dbGravityMagnitude = db.m_Gravity.magnitude;
            Vector3 dbGravityDirection = (dbGravityMagnitude > Mathf.Epsilon) ? db.m_Gravity.normalized : new Vector3(0, -1, 0);

             sdata.gravity = dbGravityMagnitude;
            sdata.gravityDirection = new float3(dbGravityDirection.x, dbGravityDirection.y, dbGravityDirection.z);


            
            if (db.m_Force != Vector3.zero) {
                 Debug.Log($"INFO: Dynamic Bone (InstanceID: {db.GetInstanceID()}) on '{targetGo.name}' uses Force ({db.m_Force}). No direct mapping; consider Wind/Gravity.", targetGo);
            }

            
            if (db.m_DampingDistrib != null && db.m_DampingDistrib.length > 1) {
                sdata.damping.SetValue(db.m_Damping, db.m_DampingDistrib);
            } else {
                sdata.damping.SetValue(db.m_Damping);
            }

            
            sdata.inertiaConstraint.worldInertia = db.m_Inert;

            sdata.angleRestorationConstraint.useAngleRestoration = db.m_Elasticity > 0f;
            if (db.m_ElasticityDistrib != null && db.m_ElasticityDistrib.length > 1) {
                sdata.angleRestorationConstraint.stiffness.SetValue(db.m_Elasticity, db.m_ElasticityDistrib);
            } else {
                sdata.angleRestorationConstraint.stiffness.SetValue(db.m_Elasticity);
            }

            float baseStiffness = db.m_Stiffness * DISTANCE_STIFFNESS_APPROX_FACTOR;
            if (db.m_StiffnessDistrib != null && db.m_StiffnessDistrib.length > 1) {
                AnimationCurve scaledStiffnessCurve = ScaleCurveValues(db.m_StiffnessDistrib, DISTANCE_STIFFNESS_APPROX_FACTOR);
                sdata.distanceConstraint.stiffness.SetValue(baseStiffness, scaledStiffnessCurve);
            } else {
                sdata.distanceConstraint.stiffness.SetValue(baseStiffness);
            }

            Debug.Log($"INFO: APPROXIMATE mapping for Elasticity/Stiffness on '{targetGo.name}' (DB InstanceID: {db.GetInstanceID()}). " +
                             $"DB Elasticity ({db.m_Elasticity}) -> MC Angle Restoration Stiffness. " +
                             $"DB Stiffness ({db.m_Stiffness}) -> MC Distance Restoration Stiffness * ~{DISTANCE_STIFFNESS_APPROX_FACTOR}. " +
                             "MANUAL TUNING ABSOLUTELY REQUIRED.", targetGo);

            
            switch (db.m_FreezeAxis)
            {
                case DynamicBone.FreezeAxis.X:
                case DynamicBone.FreezeAxis.Y:
                case DynamicBone.FreezeAxis.Z:
                     Debug.Log($"INFO: Dynamic Bone (InstanceID: {db.GetInstanceID()}) on '{targetGo.name}' uses FreezeAxis ({db.m_FreezeAxis}). No direct mapping; manual setup required (Angle Limits).", targetGo);
                    break;
            }

            
            if (db.m_RadiusDistrib != null && db.m_RadiusDistrib.length > 1) {
                sdata.radius.SetValue(db.m_Radius, db.m_RadiusDistrib);
            } else {
                sdata.radius.SetValue(db.m_Radius);
            }
            sdata.colliderCollisionConstraint.friction = db.m_Friction; 

            bool hasDbColliders = db.m_Colliders != null && db.m_Colliders.Count > 0;
            sdata.colliderCollisionConstraint.mode = (db.m_Radius > 0f || db.m_Friction > 0f || hasDbColliders) ? ColliderCollisionConstraint.Mode.Point : ColliderCollisionConstraint.Mode.None;

            sdata.colliderCollisionConstraint.colliderList.Clear(); 
            if (hasDbColliders) {
                bool colliderWarningNeeded = false;
                foreach (var dbColliderBase in db.m_Colliders)
                {
                    if (dbColliderBase == null) continue;

                    GameObject colliderGo = dbColliderBase.gameObject;
                    ColliderComponent mcCollider = colliderGo.GetComponent<ColliderComponent>();

                    
                    DynamicBonePlaneCollider dbPlaneCollider = dbColliderBase as DynamicBonePlaneCollider;
                    if (dbPlaneCollider != null)
                    {
                        MagicaPlaneCollider mcPlane = mcCollider as MagicaPlaneCollider;

                        if (mcPlane == null)
                        {
                            if (mcCollider != null)
                            {
                                Debug.LogWarning($"Replacing existing ColliderComponent '{mcCollider.GetType().Name}' on '{colliderGo.name}' with MagicaPlaneCollider.", colliderGo);
                                Undo.DestroyObjectImmediate(mcCollider);
                            }
                            mcPlane = Undo.AddComponent<MagicaPlaneCollider>(colliderGo);
                            if (mcPlane == null) {
                                Debug.LogError($"Failed to add MagicaPlaneCollider to '{colliderGo.name}'. Skipping collider.", colliderGo);
                                continue;
                            }
                            mcCollider = mcPlane;
                        }

                        mcPlane.center = dbPlaneCollider.m_Center;
                        if (dbPlaneCollider.m_Bound == DynamicBoneColliderBase.Bound.Inside) {
                            Debug.Log($"INFO: Dynamic Bone Plane Collider on '{colliderGo.name}' used 'Inside' bound. No direct MC equivalent found; behavior may differ.", colliderGo);
                        }

                        mcPlane.UpdateParameters();

                        if (!sdata.colliderCollisionConstraint.colliderList.Contains(mcCollider)) {
                            sdata.colliderCollisionConstraint.colliderList.Add(mcCollider);
                        }

                        EditorUtility.SetDirty(mcCollider);
                        string dbPlaneColliderName = dbColliderBase.name; // Store name before destruction
                        Undo.DestroyObjectImmediate(dbColliderBase);

                        colliderWarningNeeded = true;
                        continue;
                    }

                    
                    DynamicBoneCollider dbCollider = dbColliderBase as DynamicBoneCollider;
                    if (dbCollider == null)
                    {
                        Debug.LogWarning($"Skipping collider '{dbColliderBase.name}' on '{colliderGo.name}' as it is not a supported Dynamic Bone collider type (Plane, Sphere, Capsule).", colliderGo);
                        continue;
                    }

                    
                    if (dbCollider.m_Height > 0) 
                    {
                        MagicaCapsuleCollider mcCapsule = mcCollider as MagicaCapsuleCollider;

                        if (mcCapsule == null)
                        {
                            if (mcCollider != null)
                            {
                                Debug.LogWarning($"Replacing existing ColliderComponent '{mcCollider.GetType().Name}' on '{colliderGo.name}' with MagicaCapsuleCollider for DB Capsule.", colliderGo);
                                Undo.DestroyObjectImmediate(mcCollider);
                            }
                            mcCapsule = Undo.AddComponent<MagicaCapsuleCollider>(colliderGo);
                            if (mcCapsule == null) {
                                Debug.LogError($"Failed to add MagicaCapsuleCollider to '{colliderGo.name}'. Skipping collider.", colliderGo);
                                continue;
                            }
                            mcCollider = mcCapsule;
                        }

                        mcCapsule.center = dbCollider.m_Center;
                        mcCapsule.direction = (MagicaCapsuleCollider.Direction)(int)dbCollider.m_Direction; // Explicit cast to int first

                        bool useRadiusSeparation = dbCollider.m_Radius2 > 0 && Mathf.Abs(dbCollider.m_Radius - dbCollider.m_Radius2) >= 0.01f;
                        mcCapsule.radiusSeparation = useRadiusSeparation;

                        float startRadius = dbCollider.m_Radius;
                        float endRadius = useRadiusSeparation ? dbCollider.m_Radius2 : dbCollider.m_Radius;
                        float length = dbCollider.m_Height;

                        mcCapsule.SetSize(startRadius, endRadius, length);

                        mcCapsule.UpdateParameters();
                    }
                    else 
                    {
                        MagicaSphereCollider mcSphere = mcCollider as MagicaSphereCollider;

                        if (mcSphere == null)
                        {
                            if (mcCollider != null) 
                            {
                                Debug.LogWarning($"Replacing existing ColliderComponent '{mcCollider.GetType().Name}' on '{colliderGo.name}' with MagicaSphereCollider for DB Sphere.", colliderGo);
                                Undo.DestroyObjectImmediate(mcCollider);
                            }
                            mcSphere = Undo.AddComponent<MagicaSphereCollider>(colliderGo);
                            if (mcSphere == null) {
                                Debug.LogError($"Failed to add MagicaSphereCollider to '{colliderGo.name}'. Skipping collider.", colliderGo);
                                continue;
                            }
                            mcCollider = mcSphere;
                        }

                        mcSphere.center = dbCollider.m_Center;
                        mcSphere.SetSize(dbCollider.m_Radius); 

                        mcSphere.UpdateParameters();
                    }

                    if (!sdata.colliderCollisionConstraint.colliderList.Contains(mcCollider)) {
                         sdata.colliderCollisionConstraint.colliderList.Add(mcCollider);
                    }

                    EditorUtility.SetDirty(mcCollider);
                    string dbColliderName = dbColliderBase.name; // Store name before destruction
                    Undo.DestroyObjectImmediate(dbColliderBase);

                    colliderWarningNeeded = true;
                }

                if (colliderWarningNeeded) {
                     Debug.Log($"INFO: Processed Dynamic Bone Colliders for '{targetGo.name}' (DB InstanceID: {db.GetInstanceID()}). Basic Sphere/Capsule/Plane mapped. Verify settings.", targetGo);
                }
            }

            
            if (db.m_DistantDisable)
            {
                sdata.cullingSettings.distanceCullingLength.use = true;
                sdata.cullingSettings.distanceCullingLength.value = db.m_DistanceToObject;
                sdata.cullingSettings.distanceCullingReferenceObject = db.m_ReferenceObject != null ? db.m_ReferenceObject.gameObject : null;
                sdata.cullingSettings.distanceCullingFadeRatio = 0.0f;
            } else {
                 sdata.cullingSettings.distanceCullingLength.use = false;
            }

            
            sdata.updateMode = ClothUpdateMode.UnityPhysics;

            EditorUtility.SetDirty(mc);

            convertedCount++;
            
            Undo.DestroyObjectImmediate(db); 
        }

        Undo.CollapseUndoOperations(group);
        string summaryMessage = $"Conversion Complete for '{contextName}': Attempted {dynamicBones.Length} Dynamic Bones. " +
                                $"Converted: {convertedCount}. " +
                                (skippedCount > 0 ? $"Skipped (due to exclusions): {skippedCount}. " : "") +
                                "Please review results and Undo if necessary.";
        Debug.Log(summaryMessage);
    }

    /// <summary>
    /// Finds any DynamicBone components remaining within the specified roots and prompts the user for removal.
    /// </summary>
    static void CleanupRemainingDynamicBones(GameObject[] rootsToScan)
    {
        if (rootsToScan == null || rootsToScan.Length == 0) return;

        List<DynamicBone> dbsToCleanup = new List<DynamicBone>();
        foreach (GameObject root in rootsToScan)
        {
            if (root != null) // Ensure root itself hasn't been destroyed
            {
                dbsToCleanup.AddRange(root.GetComponentsInChildren<DynamicBone>(true));
            }
        }

        if (dbsToCleanup.Count > 0)
        {
            bool proceedCleanup = EditorUtility.DisplayDialog(
                "Cleanup Remaining Dynamic Bones",
                $"Found {dbsToCleanup.Count} Dynamic Bone component(s) that were not converted (e.g., they were skipped due to having Exclusions).\n\n" +
                "Do you want to remove these remaining components?\n\n" +
                "This operation can be undone separately.",
                "Yes, Remove Them",
                "No, Keep Them");

            if (!proceedCleanup) return;

            Undo.SetCurrentGroupName("Cleanup of Remaining DynamicBones");
            int cleanupGroup = Undo.GetCurrentGroup();
            foreach (DynamicBone db in dbsToCleanup)
            {
                if (db != null) Undo.DestroyObjectImmediate(db);
            }
            Undo.CollapseUndoOperations(cleanupGroup);
            Debug.Log($"Cleanup complete: Removed {dbsToCleanup.Count} remaining Dynamic Bone components.");
        }
    }
    
    static AnimationCurve ScaleCurveValues(AnimationCurve sourceCurve, float scaleFactor)
    {
        if (sourceCurve == null) return null;

        Keyframe[] keys = sourceCurve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].value *= scaleFactor;
        }
        return new AnimationCurve(keys) { preWrapMode = sourceCurve.preWrapMode, postWrapMode = sourceCurve.postWrapMode };
    }
}
