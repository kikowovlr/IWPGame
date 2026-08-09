using UnityEngine;
using UnityEngine.UI;

public class SettingsTabController : MonoBehaviour
{
    [System.Serializable]
    public struct Tab
    {
        public Button button;      
        public GameObject content; 
    }

    [SerializeField] private Tab[] _tabs;

    [Header("Tab Colors")]
    [SerializeField] private Color _activeColor = Color.white;
    [SerializeField] private Color _inactiveColor = new Color(0.886f, 0.773f, 0.773f);

    private int _currentIndex = -1;

    private void Awake()
    {
        // wire each button to select its own tab
        for (int i = 0; i < _tabs.Length; i++)
        {
            int index = i; // capture for closure
            if (_tabs[i].button != null)
                _tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }
    }

    private void OnEnable()
    {
        // default to the first tab every time the panel opens
        SelectTab(0);
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Length) return;
        _currentIndex = index;

        for (int i = 0; i < _tabs.Length; i++)
        {
            bool isActive = (i == index);

            // show/hide the content container
            if (_tabs[i].content != null)
                _tabs[i].content.SetActive(isActive);

            // recolor the tab button
            if (_tabs[i].button != null)
            {
                Image img = _tabs[i].button.targetGraphic as Image;
                if (img != null)
                    img.color = isActive ? _activeColor : _inactiveColor;
            }
        }
    }
}
