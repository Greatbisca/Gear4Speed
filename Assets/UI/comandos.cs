using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class comandos : MonoBehaviour
{
	[SerializeField] private Image m_acelarador;
	[SerializeField] private Texture2D acelarador_normal;
	[SerializeField] private Texture2D acelarador_pressed;
	
    void OnEnable()
    {
        UIDocument UI = GetComponent<UIDocument>();
		m_acelarador = UI.rootVisualElement.Q<Image>("acelarador");
    }

    void Update() {
        if (Keyboard.current.spaceKey.isPressed && m_acelarador != null)
		{
			m_acelarador.style.backgroundImage = new StyleBackground(acelarador_pressed);
		}
		else{
			m_acelarador.style.backgroundImage = new StyleBackground(acelarador_normal);
		}
    }
}
