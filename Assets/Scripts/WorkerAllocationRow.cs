using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

// Row references and pointer feedback; allocation and purchases are owned by WorkerAllocationUI.
// Everything except the Button and Background is optional - leave a field empty to skip it.
public sealed class WorkerAllocationRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Move feedback")]
    public Image rowImage;
    public Outline selectionOutline;
    [NonSerialized] public Action<bool> hoverChanged;
    public void OnPointerEnter(PointerEventData eventData) => hoverChanged?.Invoke(true);
    public void OnPointerExit(PointerEventData eventData) => hoverChanged?.Invoke(false);

    public Button button;           // required: the whole row is clickable
    public Sprite background;        // required: tinted by state (normal / source / target)
    public Sprite background2;
    public RawImage background3;
    public TMP_Text nameText;       // "Restaurant (Lv 2)"
    public TMP_Text countText;      // "2/3"
    public TMP_Text speedText;      // "73%"
    public TMP_Text actionText;     // "PICK" / "MOVE HERE" / "FROM (cancel)" / "FULL"

    [Header("Hiring - authored in the row prefab")]
    public Button hireWorkerButton;
    public TMP_Text hireWorkerText;
    public TMP_Text workerUnavailableText;
    public Button hireManagerButton;
    public TMP_Text hireManagerText;
    public TMP_Text managerUnavailableText;
}
