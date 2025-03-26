using UnityEngine;

public class BodyPartTextureChange : MonoBehaviour
{

    public BodyPart bodyPart;
    public Texture2D texture;

    public BodyPartTextureChange(BodyPart part, Texture2D tex)
    {
        bodyPart = part;
        texture = tex;
    }
}


