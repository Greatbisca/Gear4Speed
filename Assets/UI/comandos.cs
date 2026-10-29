using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class comandos : MonoBehaviour
{
	[SerializeField] private Image m_acelarador;
	[SerializeField] private Image m_manipulo;
	[SerializeField] private Image m_upgrade5;
	[SerializeField] private Image m_upgrade6;
	
	[SerializeField] private Texture2D acelarador_normal;
	[SerializeField] private Texture2D acelarador_pressed;
	[SerializeField] private Texture2D manipulo_normal;
	[SerializeField] private Texture2D manipulo_up;
	[SerializeField] private Texture2D manipulo_down;
	
	//Define posição do manipulo das mudanças
	Vector2Int posicao = new Vector2Int(0, 0);
	//Limites para upgrades
	Vector2Int limite_horizontal = new Vector2Int(-1, 1);
	bool up6 = false;
	
	// Posições das mudanças
	Vector2[] posicoes = {
		new Vector2(0, 44), // Ponto central caixa
		
		new Vector2(-18, 44), // Ponto entre mudanças 1 e 2 
		new Vector2(-18, 88), // Mudança 1
		new Vector2(-18, -42),  // Mudança 2
		
		new Vector2(18, 44), // Ponto entre mudanças 3 e 4 
		new Vector2(18, 88), // Mudança 3
		new Vector2(18, -42),  // Mudança 4
		
		new Vector2(36, 44),  // A partir de Upgrade 5
		
		new Vector2(54, 44),  // Ponto entre mudanças 5 e 6
		new Vector2(54, 88), // Mudança 5
		new Vector2(54, -42)  // Mudança 6
		
	};
	
    void OnEnable()
    {
        UIDocument UI = GetComponent<UIDocument>();
		
		m_acelarador = UI.rootVisualElement.Q<Image>("acelarador");
		m_manipulo = UI.rootVisualElement.Q<Image>("manipulo");
		m_upgrade5 = UI.rootVisualElement.Q<Image>("upgrade_5");
		m_upgrade6 = UI.rootVisualElement.Q<Image>("upgrade_6");
		
		//posição inicial do manipuço
		m_manipulo.style.left = posicoes[0].x;
		m_manipulo.style.bottom = posicoes[0].y;
    }

    void Update() {
		//Verificação upgrades caixa
		if (m_upgrade5.resolvedStyle.opacity == 1f){
			limite_horizontal.y = 3;
		}
		if (m_upgrade6.resolvedStyle.opacity == 1f){
			up6 = true;
		}
		
		//------Definir posição nas mudanças------//
		//--Horizontal--//
		if(posicao.y == 0){
			if (Keyboard.current.aKey.wasPressedThisFrame)
				if(posicao.x > limite_horizontal.x)
					posicao.x -= 1;
			if (Keyboard.current.dKey.wasPressedThisFrame)
				if(posicao.x < limite_horizontal.y)
					posicao.x += 1;
		}
		//--Vertical--//
		if(posicao.x != 0 && posicao.x != 2){
			if (Keyboard.current.wKey.wasPressedThisFrame && posicao.y < 1)
				posicao.y += 1;
			if (Keyboard.current.sKey.wasPressedThisFrame && posicao.y > -1)
				if((posicao.x == 3 && up6 == true) || posicao.y == 1)
					posicao.y -= 1;
				else if(posicao.x < 2)
					posicao.y -= 1;
		}
		
		
		switch ((posicao.x, posicao.y))
		{
			case (0, 0):
				m_manipulo.style.left = posicoes[0].x;
				m_manipulo.style.bottom = posicoes[0].y;
				break;
			case (-1,0):
				m_manipulo.style.left = posicoes[1].x;
				m_manipulo.style.bottom = posicoes[1].y;
				break;
			case (1,0):
				m_manipulo.style.left = posicoes[4].x;
				m_manipulo.style.bottom = posicoes[4].y;
				break;
			case (-1,1):
				m_manipulo.style.left = posicoes[2].x;
				m_manipulo.style.bottom = posicoes[2].y;
				break;
			case (-1,-1):
				m_manipulo.style.left = posicoes[3].x;
				m_manipulo.style.bottom = posicoes[3].y;
				break;
			case (1,1):
				m_manipulo.style.left = posicoes[5].x;
				m_manipulo.style.bottom = posicoes[5].y;
				break;
			case (1,-1):
				m_manipulo.style.left = posicoes[6].x;
				m_manipulo.style.bottom = posicoes[6].y;
				break;
			//A partir de upgrade 5
			case (2,0):
				m_manipulo.style.left = posicoes[7].x;
				m_manipulo.style.bottom = posicoes[7].y;
				break;
			case (3,0):
				m_manipulo.style.left = posicoes[8].x;
				m_manipulo.style.bottom = posicoes[8].y;
				break;
			case (3,1):
				m_manipulo.style.left = posicoes[9].x;
				m_manipulo.style.bottom = posicoes[9].y;
				break;
			case (3,-1):
				m_manipulo.style.left = posicoes[10].x;
				m_manipulo.style.bottom = posicoes[10].y;
				break;
		}
		
		//mudar sprite do manipulo
		if(posicao.y == 1){
			m_manipulo.style.backgroundImage = new StyleBackground(manipulo_up);
		}else if(posicao.y == -1){
			m_manipulo.style.backgroundImage = new StyleBackground(manipulo_down);
		}else{
			m_manipulo.style.backgroundImage = new StyleBackground(manipulo_normal);
		}
		
		//------Mudar imagem do pedal------//
        if (Keyboard.current.spaceKey.isPressed && m_acelarador != null)
		{
			m_acelarador.style.backgroundImage = new StyleBackground(acelarador_pressed);
			m_acelarador.style.bottom = -36;
		}
		else{
			m_acelarador.style.backgroundImage = new StyleBackground(acelarador_normal);
			m_acelarador.style.bottom = -27;
		}
    }
}
