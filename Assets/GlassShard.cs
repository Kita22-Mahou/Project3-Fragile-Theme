
using UnityEngine;
using UnityEngine.UI;

public class GlassShard : MaskableGraphic
{
    private Texture sourceTexture;
    private Vector2[] vertices = new Vector2[3];
    private Vector2[] uvCoordinates = new Vector2[3];

    public override Texture mainTexture
    {
        get
        {
            return sourceTexture != null
                ? sourceTexture
                : Texture2D.whiteTexture;
        }
    }

    public void Initialize(
        Texture texture,
        Vector2[] triangleVertices,
        Vector2[] triangleUVs,
        Color tint)
    {
        sourceTexture = texture;
        vertices = triangleVertices;
        uvCoordinates = triangleUVs;
        color = tint;
        raycastTarget = false;

        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (vertices == null ||
            uvCoordinates == null ||
            vertices.Length != 3 ||
            uvCoordinates.Length != 3)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = vertices[i];
            vertex.uv0 = uvCoordinates[i];
            vertex.color = color;

            vh.AddVert(vertex);
        }

        vh.AddTriangle(0, 1, 2);
    }
}
