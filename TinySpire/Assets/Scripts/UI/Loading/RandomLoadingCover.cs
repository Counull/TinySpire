using UnityEngine;

[DisallowMultipleComponent]
public sealed class RandomLoadingCover : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] covers;

    private void Awake()
    {
        if (targetRenderer == null || covers == null || covers.Length == 0)
        {
            Debug.LogWarning("Random loading cover is not configured.", this);
            return;
        }

        targetRenderer.sprite = covers[Random.Range(0, covers.Length)];
    }
}
