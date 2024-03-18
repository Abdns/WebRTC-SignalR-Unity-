using UnityEngine;

public class ObjectVisualTester : MonoBehaviour
{
   [SerializeField] public float _rotationSpeed = 1f;
   [SerializeField] public float _colorChangeSpeed = 1f;

    private Renderer _cubeRenderer;
    private float _hue;

    private void Awake()
    {
        _cubeRenderer = GetComponent<Renderer>();
    }

    private void Update()
    {
        transform.Rotate(Vector3.up * _rotationSpeed * Time.deltaTime);

        _hue += _colorChangeSpeed * Time.deltaTime;
        _hue %= 1f; 
        _cubeRenderer.material.color = Color.HSVToRGB(_hue, 1f, 1f);
    }
}
