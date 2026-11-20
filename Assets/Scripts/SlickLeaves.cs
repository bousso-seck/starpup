
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlickLeaves : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PupcatController player =
            other.GetComponentInParent<PupcatController>();

        if (player != null)
            player.IsOnSlickLeaves = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PupcatController player =
            other.GetComponentInParent<PupcatController>();

        if (player != null)
            player.IsOnSlickLeaves = false;
    }
}
