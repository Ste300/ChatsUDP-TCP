using UnityEngine;

public class CanvasSwitcher : MonoBehaviour
{
    [Header("Asigna los Canvas en el Inspector")]
    public GameObject canvasActual;
    public GameObject canvasDestino;

    public void CambiarCanvas()
    {
        if (canvasActual != null) canvasActual.SetActive(false);
        if (canvasDestino != null) canvasDestino.SetActive(true);
    }
}
