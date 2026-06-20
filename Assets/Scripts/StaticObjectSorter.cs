using UnityEngine;

[ExecuteInEditMode]
public class StaticObjectSorter : MonoBehaviour
{
    private ChunkGenerator chunkGenerator;

    void Start()
    {
        UpdateSorting();
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateSorting();
        }
    }

    public void UpdateSorting()
    {
        if (chunkGenerator == null) chunkGenerator = ChunkGenerator.Instance;
        if (chunkGenerator == null) chunkGenerator = FindAnyObjectByType<ChunkGenerator>();

        bool useYSorting = chunkGenerator != null ? chunkGenerator.useYSorting : true;

        if (useYSorting)
        {
            float worldY = transform.position.y;
            
            // Kiểm tra Sprite Sort Point nếu có Sprite Renderer
            var spriteRenderer = GetComponent<SpriteRenderer>();
            
            int offset = 15000;
            int sortingOrder = offset - Mathf.FloorToInt(worldY * 100f);

            // Áp dụng cho SortingGroup (nếu không có thì tự động thêm để gom nhóm các con)
            var sortingGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            }
            sortingGroup.sortingOrder = sortingOrder;
        }
    }
}
