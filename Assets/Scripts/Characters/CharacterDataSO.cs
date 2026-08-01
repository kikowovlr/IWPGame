using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Characters/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    public string CharacterName;
    public Sprite CharacterIcon;
    public GameObject PrefabPackage;

    [Header("Info Popup")]
    [TextArea(3, 6)] public string CharacterDescription;
    public Sprite CharacterSplashArt;
    public Sprite CharacterInfoCard;
}
