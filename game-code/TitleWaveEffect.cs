using System;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TitleWaveEffect : MonoBehaviour
{
	[Header("Wave")]
	[Tooltip("Vertical amplitude of the bob in UI units.")]
	[SerializeField]
	private float amplitude = 8f;

	[Tooltip("How many full cycles per second.")]
	[SerializeField]
	private float frequency = 1.2f;

	[Tooltip("Phase difference between adjacent characters in radians. Higher = tighter wave.")]
	[SerializeField]
	private float phaseStep = 0.45f;

	[Header("Squash and Stretch")]
	[Tooltip("Subtle vertical scale at peak and trough. Set to 0 to disable.")]
	[SerializeField]
	private float squashStretchAmount = 0.08f;

	private TMP_Text textComponent;

	private TMP_MeshInfo[] cachedMeshInfo;

	private string cachedText;

	private void Awake()
	{
		textComponent = GetComponent<TMP_Text>();
	}

	private void Start()
	{
		CacheMesh();
	}

	private void OnDisable()
	{
		if (textComponent != null)
		{
			textComponent.ForceMeshUpdate();
		}
	}

	private void CacheMesh()
	{
		textComponent.ForceMeshUpdate();
		cachedMeshInfo = textComponent.textInfo.CopyMeshInfoVertexData();
		cachedText = textComponent.text;
	}

	private void Update()
	{
		if (textComponent.text != cachedText)
		{
			CacheMesh();
		}
		if (cachedMeshInfo == null)
		{
			return;
		}
		TMP_TextInfo textInfo = textComponent.textInfo;
		int characterCount = textInfo.characterCount;
		if (characterCount == 0)
		{
			return;
		}
		for (int i = 0; i < characterCount; i++)
		{
			TMP_CharacterInfo tMP_CharacterInfo = textInfo.characterInfo[i];
			if (tMP_CharacterInfo.isVisible)
			{
				int materialReferenceIndex = tMP_CharacterInfo.materialReferenceIndex;
				int vertexIndex = tMP_CharacterInfo.vertexIndex;
				Vector3[] vertices = cachedMeshInfo[materialReferenceIndex].vertices;
				Vector3[] vertices2 = textInfo.meshInfo[materialReferenceIndex].vertices;
				float num = Mathf.Sin(Time.time * frequency * MathF.PI * 2f - (float)i * phaseStep);
				float num2 = num * amplitude;
				float num3 = 1f + num * squashStretchAmount;
				Vector3 vector = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
				for (int j = 0; j < 4; j++)
				{
					Vector3 vector2 = vertices[vertexIndex + j];
					float num4 = vector.y + (vector2.y - vector.y) * num3;
					vertices2[vertexIndex + j] = new Vector3(vector2.x, num4 + num2, vector2.z);
				}
			}
		}
		textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
	}
}
