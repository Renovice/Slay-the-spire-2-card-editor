using System.Collections;
using UnityEngine;

public class CardRotator : MonoBehaviour
{
    [SerializeField] private float maxRotationX = 30.0f;
    [SerializeField] private float maxRotationY = 30.0f;
    [SerializeField] private float rotationSpeed = 90.0f;
    [SerializeField] private float snapBackDuration = 0.1f;

    private bool isRotating = false;
    private Coroutine snapBackRoutine = null;
    private Vector3 rotationStartPos;

    private void Update()
    {
        var mousePos = Input.mousePosition;
        mousePos.z = 1.0f;

        //Debug.Log(mousePos);

        if (!isRotating && Input.GetMouseButtonDown(0))
        {
            isRotating = true;
            StopSnapBack();
            rotationStartPos = Camera.main.ScreenToWorldPoint(mousePos);
        }

        if (isRotating && Input.GetMouseButtonUp(0))
        {
            isRotating = false;
            StopSnapBack();
            snapBackRoutine = StartCoroutine(SnapBack());
        }

        if (isRotating)
        {
            var rotationCurrentPos = Camera.main.ScreenToWorldPoint(mousePos);
            var offset = rotationCurrentPos - rotationStartPos;

            var xRotation = offset.x * -rotationSpeed;
            var yRotation = offset.y * rotationSpeed;

            if(Mathf.Abs(xRotation) > maxRotationX)
            {
                xRotation = maxRotationX * Mathf.Sign(xRotation);
            }

            if (Mathf.Abs(yRotation) > maxRotationY)
            {
                yRotation = maxRotationY * Mathf.Sign(yRotation);
            }

            transform.rotation = Quaternion.Euler(yRotation, xRotation, 0);

            Debug.Log(new Vector2(xRotation, yRotation));
            Debug.Log(rotationCurrentPos);
        }
    }

    private void StopSnapBack()
    {
        if(snapBackRoutine != null)
        {
            StopCoroutine(snapBackRoutine);
            snapBackRoutine = null;
        }
    }

    private IEnumerator SnapBack()
    {
        var startRotation = transform.rotation;
        var endRotation = Quaternion.identity;

        for(float t = 0.0f; t < snapBackDuration; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t / snapBackDuration);
            yield return null;
        }

        transform.rotation = endRotation;
    }
}
