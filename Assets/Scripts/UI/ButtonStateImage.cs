using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonStateImage : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    private Button button;
    private Image image;
    private Sprite normalSprite;
    private Sprite highlightedSprite;
    private Sprite pressedSprite;
    private Sprite disabledSprite;

    private bool pointerInside;
    private bool pointerDown;
    private bool selected;

    public void Init(Image target, Sprite normal, Sprite highlighted, Sprite pressed, Sprite disabled = null)
    {
        button = GetComponent<Button>();
        image = target;
        normalSprite = normal;
        highlightedSprite = highlighted != null ? highlighted : normal;
        pressedSprite = pressed != null ? pressed : normal;
        disabledSprite = disabled != null ? disabled : normal;
        ApplyCurrentState();
    }

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnDisable()
    {
        pointerInside = false;
        pointerDown = false;
        selected = false;
        ApplyCurrentState();
    }

    private void Update()
    {
        ApplyCurrentState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        ApplyCurrentState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        ApplyCurrentState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDown = true;
        ApplyCurrentState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
        ApplyCurrentState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        ApplyCurrentState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        ApplyCurrentState();
    }

    private void ApplyCurrentState()
    {
        if (image == null) return;

        bool interactable = button == null || button.interactable;
        Sprite sprite = normalSprite;

        if (!interactable)
        {
            sprite = disabledSprite;
        }
        else if (pointerDown)
        {
            sprite = pressedSprite;
        }
        else if (pointerInside || selected)
        {
            sprite = highlightedSprite;
        }

        if (image.sprite != sprite) image.sprite = sprite;
        image.color = interactable ? Color.white : Theme.ButtonImageDisabled;
    }
}
