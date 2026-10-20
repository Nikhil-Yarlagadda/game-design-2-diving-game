using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    public float moveSpeed = 7f;
    private Vector3 targetPosition;
    private bool isMoving = false;

    void Awake()
    {
        Instance = this;
        targetPosition = transform.position;
    }

    void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
    }

    public void MoveToRoom(Transform roomTarget)
    {
        targetPosition = new Vector3(roomTarget.position.x, roomTarget.position.y, transform.position.z);
        isMoving = true;
    }
}
