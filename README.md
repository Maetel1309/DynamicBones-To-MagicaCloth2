# Dynamic Bone to MagicaCloth V2 Converter

This Unity Editor script provides a utility to convert setups using `DynamicBone` components and colliders to `MagicaCloth2` equivalents. It aims to automate a significant portion of the migration process, but **manual review and tuning of the resulting MagicaCloth components will always be necessary.**

## Features

*   **Centralized `MC2` Hierarchy:**
    *   Automatically creates a parent GameObject named `MC2` at the root of the avatar.
    *   For each converted component, a new child GameObject (e.g., `MC_HairRoot`) is created under `MC2`, with its transform matching the original bone. This keeps all physics components neatly organized in one place.
*   **Hierarchy Conversion Options:**
    *   **Convert All:** Converts all `DynamicBone` components, logging warnings for those with `Exclusions`.
    *   **Skip Exclusions:** Converts all components *except* those that use `Exclusions`, leaving them untouched for manual conversion later.
    *   Accessible via `Tools > Convert DB to MagicaCloth V2 > ...` and the GameObject's right-click context menu.
*   **Single Component Conversion:**
    *   Converts a specific `DynamicBone` component.
    *   Accessible by right-clicking a `DynamicBone` component in the Inspector and selecting "Convert This Component to MagicaCloth V2".
*   **Property Mapping (Approximate):**
    *   **Roots:** `m_Root` and `m_Roots` are mapped to MagicaCloth `rootBones`.
    *   **Transforms:** The position, rotation, and scale of the original `DynamicBone` GameObject are copied to the new `MC_...` container GameObject.
    *   **Gravity:** `m_Gravity` vector is mapped to MagicaCloth gravity magnitude and direction.
    *   **Damping:** `m_Damping` and `m_DampingDistrib` (AnimationCurve) are mapped.
    *   **Inertia:** `m_Inert` is mapped to MagicaCloth `worldInertia`.
    *   **Elasticity & Stiffness:** `m_Elasticity` (and `m_ElasticityDistrib`) is mapped to MagicaCloth Angle Restoration Stiffness. `m_Stiffness` (and `m_StiffnessDistrib`) is approximately mapped to MagicaCloth Distance Restoration Stiffness (with a scaling factor).
    *   **Radius:** `m_Radius` and `m_RadiusDistrib` (AnimationCurve) are mapped.
    *   **Friction:** `m_Friction` is mapped.
    *   **Distant Disable:** `m_DistantDisable`, `m_DistanceToObject`, and `m_ReferenceObject` are mapped to MagicaCloth Distance Culling settings.
*   **Collider Conversion:**
    *   `DynamicBonePlaneCollider` is converted to `MagicaPlaneCollider`.
    *   `DynamicBoneCollider` (Sphere/Capsule) is converted to `MagicaSphereCollider` or `MagicaCapsuleCollider` based on its `m_Height` and `m_Radius`/`m_Radius2` properties.
    *   Existing `MagicaCloth.ColliderComponent`s on the same GameObject as a Dynamic Bone collider will be replaced if a new type is needed.
*   **Automatic `BoneSpring` Detection:**
    *   If a GameObject hosting a `DynamicBone` has a name containing keywords (e.g., "breast", "boob", "bust"), the corresponding `MagicaCloth` component's type will be set to `BoneSpring` instead of the default `BoneCloth`.
*   **User Prompts & Warnings:**
    *   Confirmation dialogs are shown before initiating hierarchy or single component conversions.
    *   Warnings are logged in the console for:
        *   `DynamicBone` components using `m_Exclusions` (conversion proceeds, but manual setup in MagicaCloth is required).
        *   `DynamicBone` components using `m_Force` (no direct mapping).
        *   `DynamicBone` components using `m_FreezeAxis` (no direct mapping, manual setup of Angle Limits in MagicaCloth needed).
        *   `DynamicBonePlaneCollider` using `m_Bound = Bound.Inside` (no direct MagicaCloth equivalent, behavior may differ).
        *   Situations where default root bones are assigned.
*   **Undo System Integration:**
    *   All changes made by the script (component additions, removals, property modifications) are registered with Unity's Undo system.
*   **Final Cleanup:**
    *   After the main conversion, the script performs a check for any `DynamicBone` components that were not processed (e.g., because you chose to skip them).
    *   If leftovers are found, a dialog prompts the user to confirm their removal. This cleanup is also undoable as a separate step.

## What It Doesn't Do / Limitations

*   **No Perfect 1:1 Mapping:** The conversion provides an **approximate** starting point. **Manual tuning of MagicaCloth settings is ALWAYS required** to achieve the desired behavior and appearance. Dynamic Bone and MagicaCloth have different underlying physics models and parameter ranges.
*   **Exclusions Require Manual Setup:** While the script warns about `DynamicBone` exclusions, it **does not automatically replicate this logic** in MagicaCloth. You will need to manually configure exclusions in MagicaCloth, for example, by using vertex painting, adjusting root bone selections, or utilizing MagicaCloth's own exclusion features if applicable.
*   **`FreezeAxis` Requires Manual Setup:** The script warns if `m_FreezeAxis` is used but does not map it. You will need to configure MagicaCloth's Angle Limits manually.
*   **`m_Force` Not Mapped:** The script warns if `m_Force` is used. Consider using MagicaCloth's Wind or Gravity features as alternatives.
*   **Complex Collider Logic:** Only basic `DynamicBonePlaneCollider` and `DynamicBoneCollider` (sphere/capsule) types are converted. Custom collider scripts inheriting from `DynamicBoneColliderBase` or highly complex standard collider setups may not convert correctly or at all.
*   **Performance Optimization:** The script focuses on functional conversion. The resulting MagicaCloth setup may not be optimized for performance and may require further adjustments.
*   **Does Not Leverage All MagicaCloth Features:** The conversion provides a base. You may want to explore and utilize more advanced features of MagicaCloth V2 after the initial conversion.

## How to Use

1.  **Prerequisites:**
    *   Ensure you have both Dynamic Bone and MagicaCloth V2 imported into your Unity project. (Magica Cloth 1 is unsupported)
2.  **Installation:**
    *   Place the `DynamicBoneToMagicaClothConverter.cs` script inside an `Editor` folder in your Unity project (e.g., `Assets/Editor/`).
3.  **Hierarchy Conversion:**
    *   Select the root GameObject in your Hierarchy window that contains the `DynamicBone` components you wish to convert.
    *   Go to **Tools > Convert DB to MagicaCloth V2** and choose one of the conversion options:
        *   **Hierarchy - Convert All (Warn on Exclusions)**
        *   **Hierarchy - Skip Components with Exclusions**
    *   Alternatively, right-click on the selected GameObject in the Hierarchy and choose from the **Convert Hierarchy to MagicaCloth V2** submenu.
    *   A confirmation dialog will appear. Review the information and click "Yes, Convert" to proceed.
4.  **Single Component Conversion:**
    *   Select the GameObject that has the `DynamicBone` component you want to convert.
    *   In the Inspector window, right-click on the header of the `DynamicBone` component.
    *   Choose **Convert This Component to MagicaCloth V2**.
    *   A confirmation dialog will appear. Click "Yes, Convert This Component" to proceed.
5.  **Review & Cleanup:**
    *   After the conversion, check the Unity Console for any warnings or informational messages. These will guide you on properties that require manual attention.
    *   A new `MC2` GameObject will be present at the root of your selection. **Inspect its children** to find and tune the newly created `MagicaCloth` components.
    *   If any `DynamicBone` components were not processed (e.g., because you chose to skip them), a final cleanup dialog will appear, offering to remove them.
    *   **Thoroughly inspect and test** the converted setup.

## Important Notes

*   **BACKUP YOUR PROJECT:** Before running this script on important assets, always back up your project or ensure you are using version control (like Git).
*   **Test Thoroughly:** Physics behavior can be nuanced. Always test the converted setup extensively.
*   **Consult MagicaCloth Documentation:** For detailed information on MagicaCloth V2 parameters and features, refer to its official documentation.

## Contributing

Feel free to fork this repository, make improvements, and submit pull requests!

## License

MIT License - Copyright (c) 2024 Maetel1309

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
