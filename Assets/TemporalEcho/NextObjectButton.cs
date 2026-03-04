using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NextObjectButton : MonoBehaviour
{
    public StorySelectionManager storyManager;

    private void Awake()
    {
        if (storyManager == null)
            storyManager = FindAnyObjectByType<StorySelectionManager>();
    }

    public void Trigger()
    {
        if (storyManager != null)
            storyManager.SelectNext();
    }
}
