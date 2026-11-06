using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class comandos_multiplayer : MonoBehaviour
{
	[SerializeField] private Image p1_ecra;
	[SerializeField] private Image p1_acelarador;
	[SerializeField] private Image p1_manipulo;
	[SerializeField] private Image p1_upgrade5;
	[SerializeField] private Image p1_upgrade6;
	[SerializeField] private Image p1_upgradeBar;
	
	[SerializeField] private Image p2_ecra;
	[SerializeField] private Image p2_acelarador;
	[SerializeField] private Image p2_manipulo;
	[SerializeField] private Image p2_upgrade5;
	[SerializeField] private Image p2_upgrade6;
	[SerializeField] private Image p2_upgradeBar;
	
	//-------------------------------------------------------//
	[SerializeField] private Texture2D acelarador_normal;
	[SerializeField] private Texture2D acelarador_pressed;

	[SerializeField] private Texture2D manipulo_normal;
	[SerializeField] private Texture2D manipulo_up;
	[SerializeField] private Texture2D manipulo_down;

	[SerializeField] private Texture2D ecra_mudancas;
	[SerializeField] private Texture2D ecra_UP5;
	[SerializeField] private Texture2D ecra_UP6;

	//Define posição do manipulo das mudanças
	Vector2Int posicao = new Vector2Int(0, 0);
	Vector2Int posicao2 = new Vector2Int(0, 0);
	//Limites para upgrades
	Vector2Int limite_horizontal = new Vector2Int(-1, 1);
	Vector2Int limite_horizontal2 = new Vector2Int(-1, 1);
	bool up6 = false;
	bool up6_2 = false;
	
	//Valor da barra de upgrade || cheio = 240
	int upgrade_value_p1 = 0;
	int upgrade_value_p2 = 0;

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

		// Procurar o Player 1
        VisualElement player1 = UI.rootVisualElement.Q<VisualElement>("player1");
		// Procurar o Player 2
        VisualElement player2 = UI.rootVisualElement.Q<VisualElement>("player2");

		p1_acelarador = player1.Q<Image>("acelarador");
        p1_manipulo = player1.Q<Image>("manipulo");
        p1_upgrade5 = player1.Q<Image>("upgrade_5");
        p1_upgrade6 = player1.Q<Image>("upgrade_6");
        p1_ecra = player1.Q<Image>("gearscreen");
		p1_upgradeBar = player1.Q<Image>("up_bar");

		p2_acelarador = player2.Q<Image>("acelarador");
        p2_manipulo = player2.Q<Image>("manipulo");
        p2_upgrade5 = player2.Q<Image>("upgrade_5");
        p2_upgrade6 = player2.Q<Image>("upgrade_6");
        p2_ecra = player2.Q<Image>("gearscreen");
		p2_upgradeBar = player2.Q<Image>("up_bar");
		
		//posição inicial do manipuço
		p1_manipulo.style.left = posicoes[0].x;
		p1_manipulo.style.bottom = posicoes[0].y;

		p1_manipulo.style.backgroundImage = new StyleBackground(ecra_mudancas);

		p2_manipulo.style.left = posicoes[0].x;
		p2_manipulo.style.bottom = posicoes[0].y;

		p2_manipulo.style.backgroundImage = new StyleBackground(ecra_mudancas);
    }

    void Update() {
	
		//----Barra de Upgrade----//
		//Código de teste apenas para encher a barra
		if (upgrade_value_p1 < 240)
		{
			upgrade_value_p1++;
		}
		
		if (upgrade_value_p2 < 240)
		{
			upgrade_value_p2++;
		}
		
		//Define o valor da barra de upgrade
		p1_upgradeBar.style.width = upgrade_value_p1;
		p2_upgradeBar.style.width = upgrade_value_p2;
	
		//player 1
		AtualizarJogador(p1_ecra, p1_acelarador, p1_manipulo, p1_upgrade5, p1_upgrade6,
			ref posicao, ref limite_horizontal, ref up6,
			Keyboard.current.aKey, Keyboard.current.dKey,
			Keyboard.current.wKey, Keyboard.current.sKey,
			Keyboard.current.spaceKey);
		//player 2
		AtualizarJogador(p2_ecra, p2_acelarador, p2_manipulo, p2_upgrade5, p2_upgrade6,
			ref posicao2, ref limite_horizontal2, ref up6_2,
			Keyboard.current.leftArrowKey, Keyboard.current.rightArrowKey,
			Keyboard.current.upArrowKey, Keyboard.current.downArrowKey,
			Keyboard.current.rightCtrlKey);

    }

	void AtualizarJogador(Image ecra, Image acelerador, Image manipulo, Image upgrade5, Image upgrade6,
		ref Vector2Int posicao, ref Vector2Int limite_horizontal, ref bool up6,
		KeyControl teclaEsquerda, KeyControl teclaDireita, KeyControl teclaCima, KeyControl teclaBaixo,
		KeyControl teclaAcelerador)
	{
		//Verificação upgrades caixa
		if (upgrade5.resolvedStyle.opacity == 1f){
			limite_horizontal.y = 3;
			ecra.style.backgroundImage = new StyleBackground(ecra_UP5);
		}
		if (upgrade6.resolvedStyle.opacity == 1f){
			up6 = true;
			ecra.style.backgroundImage = new StyleBackground(ecra_UP6);
		}

		//------Definir posição nas mudanças------//
		//--Horizontal--//
		if(posicao.y == 0){
			if (teclaEsquerda.wasPressedThisFrame)
				if(posicao.x > limite_horizontal.x)
					posicao.x -= 1;
			if (teclaDireita.wasPressedThisFrame)
				if(posicao.x < limite_horizontal.y)
					posicao.x += 1;
		}
		//--Vertical--//
		if(posicao.x != 0 && posicao.x != 2){
			if (teclaCima.wasPressedThisFrame && posicao.y < 1)
				posicao.y += 1;
			if (teclaBaixo.wasPressedThisFrame && posicao.y > -1)
				if((posicao.x == 3 && up6 == true) || posicao.y == 1)
					posicao.y -= 1;
				else if(posicao.x < 2)
					posicao.y -= 1;
		}


		switch ((posicao.x, posicao.y))
		{
			case (0, 0):
				manipulo.style.left = posicoes[0].x;
				manipulo.style.bottom = posicoes[0].y;
				break;
			case (-1,0):
				manipulo.style.left = posicoes[1].x;
				manipulo.style.bottom = posicoes[1].y;
				break;
			case (1,0):
				manipulo.style.left = posicoes[4].x;
				manipulo.style.bottom = posicoes[4].y;
				break;
			case (-1,1):
				manipulo.style.left = posicoes[2].x;
				manipulo.style.bottom = posicoes[2].y;
				break;
			case (-1,-1):
				manipulo.style.left = posicoes[3].x;
				manipulo.style.bottom = posicoes[3].y;
				break;
			case (1,1):
				manipulo.style.left = posicoes[5].x;
				manipulo.style.bottom = posicoes[5].y;
				break;
			case (1,-1):
				manipulo.style.left = posicoes[6].x;
				manipulo.style.bottom = posicoes[6].y;
				break;
			//A partir de upgrade 5
			case (2,0):
				manipulo.style.left = posicoes[7].x;
				manipulo.style.bottom = posicoes[7].y;
				break;
			case (3,0):
				manipulo.style.left = posicoes[8].x;
				manipulo.style.bottom = posicoes[8].y;
				break;
			case (3,1):
				manipulo.style.left = posicoes[9].x;
				manipulo.style.bottom = posicoes[9].y;
				break;
			case (3,-1):
				manipulo.style.left = posicoes[10].x;
				manipulo.style.bottom = posicoes[10].y;
				break;
		}

		//mudar sprite do manipulo
		if(posicao.y == 1){
			manipulo.style.backgroundImage = new StyleBackground(manipulo_up);
		}else if(posicao.y == -1){
			manipulo.style.backgroundImage = new StyleBackground(manipulo_down);
		}else{
			manipulo.style.backgroundImage = new StyleBackground(manipulo_normal);
		}


		//------Mudar imagem do pedal------//
        if (teclaAcelerador.isPressed && acelerador != null)
		{
			acelerador.style.backgroundImage = new StyleBackground(acelarador_pressed);
			acelerador.style.bottom = -36;

		}
		else{
			acelerador.style.backgroundImage = new StyleBackground(acelarador_normal);
			acelerador.style.bottom = -27;
		}
		
		
	}
}