using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class BuildingSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("Identity")]
    public string buildingId;

    [Header("Art")]
    public SpriteRenderer art;
    public Sprite builtSprite;
    public Sprite siteSprite;              // leave empty for buildings that start built
    public bool startsBuilt = true;

    [Header("Events")]
    public UnityEvent onBuiltClicked;      // if building comes built from start
    public UnityEvent onSiteClicked;       // if building has to be built later

    bool built;

    void Reset() { art = GetComponent<SpriteRenderer>(); }
    void Start() { SetBuilt(startsBuilt); }

    public void SetBuilt(bool value)
    {
        built = value;
        art.sprite = built ? builtSprite : siteSprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"Tapped {buildingId} (built: {built})");
        if (built) onBuiltClicked.Invoke();
        else onSiteClicked.Invoke();
    }
}