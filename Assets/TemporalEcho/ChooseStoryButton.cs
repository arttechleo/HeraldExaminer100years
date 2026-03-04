using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChooseStoryButton : MonoBehaviour
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
            storyManager.ToggleSelectionMode();
    }
}
