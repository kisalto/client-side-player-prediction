using System;
using UnityEngine;

public class ParallaxController : MonoBehaviour
{
	[Serializable]
	public class ParallaxLayerData
	{
		public Transform layerTransform;

		public float parallaxFactor;
	}

	public ParallaxLayerData[] layers;

	private Transform cam;

	private Vector3 previousCamPosition;

	private void Start()
	{
		cam = Camera.main.transform;
		previousCamPosition = cam.position;
	}

	private void LateUpdate()
	{
		if (cam == null)
		{
			return;
		}
		float num = cam.position.x - previousCamPosition.x;
		float num2 = cam.position.y - previousCamPosition.y;
		ParallaxLayerData[] array = layers;
		foreach (ParallaxLayerData parallaxLayerData in array)
		{
			if (!(parallaxLayerData.layerTransform == null))
			{
				Vector3 vector = new Vector3(num * parallaxLayerData.parallaxFactor, num2 * parallaxLayerData.parallaxFactor, 0f);
				parallaxLayerData.layerTransform.position += vector;
			}
		}
		previousCamPosition = cam.position;
	}
}
