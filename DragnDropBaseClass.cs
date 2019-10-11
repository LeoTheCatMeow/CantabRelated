using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//a helpful base class for drag and drop style games, written by leothecat
//the script should be attached to the uppermost parent gameobject where all interactables are its children
//draggable objects should have an event trigger component that sends BeginDrag, Drag, and EndDrag event to this script
//dropzone objects should have an event trigger component that sends Drop event to this script
//draggable objects will be brought out of their parents during drag if they are not directly under the game parent
//draggable objects will be parented under dropzones if the drop is successful 
//EvaluateDrop() and EvaluateGame() must be implemented, they should contain game logic 
//be aware of objects' positions in the hiearchy, higher ones will appear underneath lower ones

public abstract class DragnDropBaseClass : MonoBehaviour
{
    public enum OnDragOptions { Clone, Move }
    public enum OnDropOptions { Stay, Return, Return_Immediately, Destroy }
    public OnDragOptions BehaviorOnDrag;
    public OnDropOptions BehaviorOnSuccessfulDrop;
    public OnDropOptions BehaviorOnFailedDrop;
    [Tooltip("If the object is in the right place, prevent further dragging, only applicable to OnDropStay behavior")]
    public bool lockIfSuccess;
    public bool lockRayCastOnly = false;
    [Tooltip("Stick object to the pivot of the drop zone when dropped, only applicable to OnDropStay behavior")]
    public bool alignToDropZone;

    private RectTransform activeObject;
    private RectTransform activeDropZone;
    private bool successfulDrop;
    private Dictionary<RectTransform, Vector2> originalLocations = new Dictionary<RectTransform, Vector2>();

    public virtual void BeginDrag(BaseEventData data)
    {
        //the object we are dragging 
        activeObject = ((PointerEventData)data).pointerDrag.GetComponent<RectTransform>();

        //whether or not to clone the object, only clone objects that haven't been dragged 
        if (BehaviorOnDrag == OnDragOptions.Clone && !originalLocations.ContainsKey(activeObject))
        {
            GameObject clone = Instantiate(activeObject.gameObject, activeObject.parent);
            //the clone takes active object's current spot and pushes it down by 1 in the hiearchy, allowing active object to show on top
            clone.transform.SetSiblingIndex(activeObject.GetSiblingIndex());
        }

        //only drag with respect to the parent game object
        activeObject.SetParent(transform);
        //increase scale a bit
        activeObject.localScale *= 1.2f;

        //track the object's original location so it can be returned
        if (!originalLocations.ContainsKey(activeObject))
        {
            originalLocations.Add(activeObject, activeObject.anchoredPosition);
        }
    }

    public virtual void Drag(BaseEventData data)
    {
        //update position
        Vector2 newPosition = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(GetComponent<RectTransform>(), ((PointerEventData)data).position, null, out newPosition);
        activeObject.anchoredPosition = newPosition;
        //disable draggable object raycast so drop event can be received
        activeObject.GetComponent<Graphic>().raycastTarget = false;
    }

    //internally in unity, Drop event is always sent before EndDrag event 
    public virtual void Drop(BaseEventData data)
    {
        //the object we dropped on
        activeDropZone = ((PointerEventData)data).pointerCurrentRaycast.gameObject.GetComponent<RectTransform>();
        //determine if we can drop here or not
        successfulDrop = EvaluateDrop(activeObject, activeDropZone);
    }

    public virtual void EndDrag(BaseEventData data)
    {
        //enable draggable object raycast so it can be dragged again
        activeObject.GetComponent<Graphic>().raycastTarget = true;
        //reset object size
        activeObject.localScale /= 1.2f;

        if (successfulDrop)
        {
            if (BehaviorOnSuccessfulDrop == OnDropOptions.Return)
            {
                StartCoroutine(ReturnObject(activeObject));
            }
            else if (BehaviorOnSuccessfulDrop == OnDropOptions.Return_Immediately)
            {
                ReturnImmediately(activeObject);
            }
            else if (BehaviorOnSuccessfulDrop == OnDropOptions.Destroy)
            {
                Destroy(activeObject.gameObject);
            }
            else if (BehaviorOnSuccessfulDrop == OnDropOptions.Stay)
            {
                //parent to the drop zone so we can evaluate the game using the transform hiearchy 
                activeObject.SetParent(activeDropZone);

                //if needed, lock the object in place and prevent further dragging 
                if (lockIfSuccess)
                {
                    activeObject.GetComponent<Graphic>().raycastTarget = false;
                    if (!lockRayCastOnly)
                    {
                        Destroy(activeObject.GetComponent<EventTrigger>());
                    }
                }
                //if needed, align the object to the pivot of the drop zone
                if (alignToDropZone)
                {
                    activeObject.anchoredPosition = Vector2.zero;
                }
            }

            //check if the game is won, if so, prevent the player from dragging anything else
            if (EvaluateGame() == true)
            {
                ToggleRayCastForAll(false);
            }

            //reset condition
            successfulDrop = false;
        } else
        {
            if (BehaviorOnFailedDrop == OnDropOptions.Return)
            {
                StartCoroutine(ReturnObject(activeObject));
            } else if (BehaviorOnFailedDrop == OnDropOptions.Return_Immediately)
            {
                ReturnImmediately(activeObject);
            } else if (BehaviorOnFailedDrop == OnDropOptions.Destroy)
            {
                Destroy(activeObject.gameObject);
            } else if (BehaviorOnFailedDrop == OnDropOptions.Stay)
            {
                //do nothing 
            }
        }
    }

    //gradually return the object to its origin 
    protected virtual IEnumerator ReturnObject(RectTransform t)
    {
        Vector2 originalLocation = originalLocations[t];
        float maxStep = Vector2.Distance(t.anchoredPosition, originalLocation) * 0.05f;
        while ((t.anchoredPosition - originalLocation).magnitude > 0.1f)
        {
            t.anchoredPosition = Vector2.MoveTowards(t.anchoredPosition, originalLocation, maxStep);
            yield return null;
        }
        t.anchoredPosition = originalLocation;
    }

    protected virtual void ReturnImmediately(RectTransform t)
    {
        t.anchoredPosition = originalLocations[t];
    }

    //check if a drop is allowed, return true will perform the successful drop behavior, otherwise perform the failed drop behavior
    //you can end a game in this method too, be flexible
    protected abstract bool EvaluateDrop(RectTransform obj, RectTransform dropZone);

    //called after a drop is finalized, can be used to end a game, if return true, ToggleRayCastForAll(false) is called
    protected abstract bool EvaluateGame();

    //toggle raycast for all children with graphic component
    protected void ToggleRayCastForAll(bool state)
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>();
        foreach (Graphic x in graphics)
        {
            x.raycastTarget = state;
        }
    }
}
