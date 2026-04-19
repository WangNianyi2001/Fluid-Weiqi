using UnityEngine;

public class MainMenu : MonoBehaviour
{
	#region Unity life cycle
	protected void Start()
	{
		CloseSecondaryMenu();
	}
	#endregion

	#region UI
	[SerializeField] GameObject secondaryMenu;
	[SerializeField] Transform secondaryMenuContainer;

	public void OpenSecondaryMenu(GameObject menu)
	{
		if(menu == null)
			return;
		if(!menu.transform.IsChildOf(secondaryMenuContainer.transform))
			return;
		for(int i = 0; i < secondaryMenuContainer.transform.childCount; ++i)
		{
			GameObject child = secondaryMenuContainer.transform.GetChild(i).gameObject;
			child.SetActive(child == menu);
		}
		secondaryMenu.SetActive(true);
	}

	public void CloseSecondaryMenu()
	{
		secondaryMenu.SetActive(false);
	}
	#endregion
}
