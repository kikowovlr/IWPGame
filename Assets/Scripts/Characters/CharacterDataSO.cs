using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Characters/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    public string CharacterName;
    public Sprite CharacterIcon;
    public GameObject PrefabPackage;
}
