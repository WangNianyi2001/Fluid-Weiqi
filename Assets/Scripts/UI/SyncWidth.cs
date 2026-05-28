using UnityEngine;

[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class SyncWidth : MonoBehaviour
{
	RectTransform self;
	[SerializeField] RectTransform target;

	protected void Awake()
	{
		self = GetComponent<RectTransform>();
	}

	protected void Update()
	{
		Sync();
	}

	void Sync()
	{
		if(target == null)
			return;
		if(self == null)
			self = GetComponent<RectTransform>();
		if(!self)
			return;

		Vector2 targetSize = target.rect.size;

		self.anchorMin = new Vector2(0, 0.5f);
		self.anchorMax = new Vector2(0, 0.5f);

		self.sizeDelta = targetSize;
	}
}
