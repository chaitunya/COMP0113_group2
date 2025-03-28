using Ubiq;
using UnityEngine;

/// <summary>
/// Recroom/rayman style avatar with hands, torso and head. This class is only
/// here for compatibility. You can safely ignore it for this example/tutorial.
/// </summary>
public class FloatingAvatar : MonoBehaviour
{
    public Transform head;
    public Transform torso;
    public Transform leftHand;
    public Transform rightHand;

    public Renderer headRenderer;
    public Renderer torsoRenderer;
    public Renderer leftHandRenderer;
    public Renderer rightHandRenderer;

    public Transform baseOfNeckHint;

    // public float torsoFacingHandsWeight;
    public AnimationCurve torsoFootCurve;

    public AnimationCurve torsoFacingCurve;

    private TexturedAvatar texturedAvatar;
    private HeadAndHandsAvatar headAndHandsAvatar;
    private Vector3 footPosition;
    private Quaternion torsoFacing;
    
    private InputVar<Pose> lastGoodHeadPose;

    public Material headBaseMaterial;
    public Material torsoBaseMaterial;
    public Material leftHandBaseMaterial;
    public Material rightHandBaseMaterial;


    private void OnEnable()
    {
        headAndHandsAvatar = GetComponentInParent<HeadAndHandsAvatar>();

        if (headAndHandsAvatar)
        {
            headAndHandsAvatar.OnHeadUpdate.AddListener(HeadAndHandsEvents_OnHeadUpdate);
            headAndHandsAvatar.OnLeftHandUpdate.AddListener(HeadAndHandsEvents_OnLeftHandUpdate);
            headAndHandsAvatar.OnRightHandUpdate.AddListener(HeadAndHandsEvents_OnRightHandUpdate);
        }

        texturedAvatar = GetComponentInParent<TexturedAvatar>();

        if (texturedAvatar)
        {
            // texturedAvatar.OnTextureChanged.AddListener(TexturedAvatar_OnTextureChanged);
            texturedAvatar.OnBodyPartTextureChanged.AddListener(ApplyBodyPartTexture);
        }

        headBaseMaterial = new Material(headRenderer.material);
        torsoBaseMaterial = new Material(torsoRenderer.material);
        leftHandBaseMaterial = new Material(leftHandRenderer.material);
        rightHandBaseMaterial = new Material(rightHandRenderer.material);
    }

    private void OnDisable()
    {
        if (headAndHandsAvatar && headAndHandsAvatar != null)
        {
            headAndHandsAvatar.OnHeadUpdate.RemoveListener(HeadAndHandsEvents_OnHeadUpdate);
            headAndHandsAvatar.OnLeftHandUpdate.RemoveListener(HeadAndHandsEvents_OnLeftHandUpdate);
            headAndHandsAvatar.OnRightHandUpdate.RemoveListener(HeadAndHandsEvents_OnRightHandUpdate);
        }

        if (texturedAvatar && texturedAvatar != null)
        {
            // texturedAvatar.OnTextureChanged.RemoveListener(TexturedAvatar_OnTextureChanged);
            texturedAvatar.OnBodyPartTextureChanged.RemoveListener(ApplyBodyPartTexture);
        }
    }

    private void HeadAndHandsEvents_OnHeadUpdate(InputVar<Pose> pose)
    {
        if (!pose.valid)
        {
            if (!lastGoodHeadPose.valid)
            {
                headRenderer.enabled = false;
                return;
            }
            
            pose = lastGoodHeadPose;
        }
        
        head.position = pose.value.position;
        head.rotation = pose.value.rotation;        
        lastGoodHeadPose = pose;
    }

    private void HeadAndHandsEvents_OnLeftHandUpdate(InputVar<Pose> pose)
    {
        if (!pose.valid)
        {
            leftHandRenderer.enabled = false;
            return;
        }
        
        leftHandRenderer.enabled = true;
        leftHand.position = pose.value.position;
        leftHand.rotation = pose.value.rotation;                    
    }

    private void HeadAndHandsEvents_OnRightHandUpdate(InputVar<Pose> pose)
    {
        if (!pose.valid)
        {
            rightHandRenderer.enabled = false;
            return;
        }

        rightHandRenderer.enabled = true;
        rightHand.position = pose.value.position;
        rightHand.rotation = pose.value.rotation;                    
    }

    // private void TexturedAvatar_OnTextureChanged(Texture2D tex)
    // {
    //     headRenderer.material.mainTexture = tex;
    //     torsoRenderer.material = headRenderer.material;
    //     leftHandRenderer.material = headRenderer.material;
    //     rightHandRenderer.material = headRenderer.material;
    // }

    private void ApplyBodyPartTexture(BodyPartTextureChange change)
    {
        Material newMaterial = null;

        switch (change.bodyPart)
        {
            case BodyPart.Head:
                newMaterial = new Material(headBaseMaterial);
                newMaterial.mainTexture = change.texture;
                headRenderer.material = newMaterial;
                break;

            case BodyPart.Torso:
                // newMaterial = new Material(torsoBaseMaterial);
                // newMaterial.mainTexture = change.texture;
                // torsoRenderer.material = newMaterial;

                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                torsoRenderer.GetPropertyBlock(propBlock);
                propBlock.SetTexture("_MainTex", change.texture);
                torsoRenderer.SetPropertyBlock(propBlock);
                break;

            case BodyPart.LeftHand:
                newMaterial = new Material(leftHandBaseMaterial);
                newMaterial.mainTexture = change.texture;
                leftHandRenderer.material = newMaterial;
                break;

            case BodyPart.RightHand:
                newMaterial = new Material(rightHandBaseMaterial);
                newMaterial.mainTexture = change.texture;
                rightHandRenderer.material = newMaterial;
                break;
        }
    }



    // private void TexturedAvatar_OnTextureChanged(Texture2D tex)
    // {
    //     // Assign head texture
    //     var headMaterial = new Material(headBaseMaterial); // Create a new instance from base
    //     headMaterial.mainTexture = tex;
    //     headRenderer.material = headMaterial;

    //     // Assign torso texture (could be same texture or a different one)
    //     var torsoMaterial = new Material(torsoBaseMaterial);
    //     torsoMaterial.mainTexture = tex; // or a different texture if you want
    //     torsoRenderer.material = torsoMaterial;

    //     // Assign left hand texture
    //     var leftHandMaterial = new Material(leftHandBaseMaterial);
    //     leftHandMaterial.mainTexture = tex;
    //     leftHandRenderer.material = leftHandMaterial;

    //     // Assign right hand texture
    //     var rightHandMaterial = new Material(rightHandBaseMaterial);
    //     rightHandMaterial.mainTexture = tex;
    //     rightHandRenderer.material = rightHandMaterial;
    // }

    private void Update()
    {
        UpdateTorso();
    }

    private void UpdateTorso()
    {
        // Give torso a bit of dynamic movement to make it expressive

        // Update virtual 'foot' position, just for animation, wildly inaccurate :)
        var neckPosition = baseOfNeckHint.position;
        footPosition.x += (neckPosition.x - footPosition.x) * Time.deltaTime * torsoFootCurve.Evaluate(Mathf.Abs(neckPosition.x - footPosition.x));
        footPosition.z += (neckPosition.z - footPosition.z) * Time.deltaTime * torsoFootCurve.Evaluate(Mathf.Abs(neckPosition.z - footPosition.z));
        footPosition.y = 0;

        // Forward direction of torso is vector in the transverse plane
        // Determined by head direction primarily, hint provided by hands
        var torsoRotation = Quaternion.identity;

        // Head: Just use head direction
        var headFwd = head.forward;
        headFwd.y = 0;

        // Hands: Imagine line between hands, take normal (in transverse plane)
        // Use head orientation as a hint to give us which normal to use
        // var handsLine = rightHand.position - leftHand.position;
        // var handsFwd = new Vector3(-handsLine.z,0,handsLine.x);
        // if (Vector3.Dot(handsFwd,headFwd) < 0)
        // {
        //     handsFwd = new Vector3(handsLine.z,0,-handsLine.x);
        // }
        // handsFwdStore = handsFwd;

        // var headRot = Quaternion.LookRotation(headFwd,Vector3.up);
        // var handsRot = Quaternion.LookRotation(handsFwd,Vector3.up);

        // // Rotation is handsRotation capped to a distance from headRotation
        // var headToHandsAngle = Quaternion.Angle(headRot,handsRot);
        // Debug.Log(headToHandsAngle);
        // var rot = Quaternion.RotateTowards(headRot,handsRot,Mathf.Clamp(headToHandsAngle,-torsoFacingHandsWeight,torsoFacingHandsWeight));

        // // var rot = Quaternion.SlerpUnclamped(handsRot,headRot,torsoFacingHeadToHandsWeightRatio);

        var rot = Quaternion.LookRotation(headFwd, Vector3.up);
        var angle = Quaternion.Angle(torsoFacing, rot);
        var rotateAngle = Mathf.Clamp(Time.deltaTime * torsoFacingCurve.Evaluate(Mathf.Abs(angle)), 0, angle);
        torsoFacing = Quaternion.RotateTowards(torsoFacing, rot, rotateAngle);

        // Place torso so it makes a straight line between neck and feet
        torso.position = neckPosition;
        torso.rotation = Quaternion.FromToRotation(Vector3.down, footPosition - neckPosition) * torsoFacing;
    }

    // private Vector3 handsFwdStore;

    // private void OnDrawGizmos()
    // {
    //     Gizmos.color = Color.blue;
    //     Gizmos.DrawLine(head.position, footPosition);
    //     // Gizmos.DrawLine(head.position,head.position + handsFwdStore);
    // }
}
