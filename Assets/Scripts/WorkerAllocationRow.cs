using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Put this on the root of the ROW PREFAB. It only holds references to the row's own UI parts.
// Everything except the Button and Background is optional - leave a field empty to skip it.
public sealed class WorkerAllocationRow : MonoBehaviour
{
    public Button button;           // required: the whole row is clickable
    public Sprite background;        // required: tinted by state (normal / source / target)
    public Sprite background2;
    public RawImage background3;
    public TMP_Text nameText;       // "Restaurant (Lv 2)"
    public TMP_Text countText;      // "2/3"
    public TMP_Text speedText;      // "73%"
    public TMP_Text actionText;     // "PICK" / "MOVE HERE" / "FROM (cancel)" / "FULL"
}
