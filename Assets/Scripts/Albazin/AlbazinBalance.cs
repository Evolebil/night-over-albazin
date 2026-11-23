using UnityEngine;
[CreateAssetMenu(menuName="Албазин/Баланс")]
public sealed class AlbazinBalance : ScriptableObject
{
    public float phaseSeconds = 300, speed = 5, aggro = 8, release = 14, infantrySpeed = 2.5f;
    public int[] waves = {10,16,24};
    public int playerHP = 100, stab = 20, infantryHP = 40, startingGold = 50;
    public float waveInterval = 15;
}
