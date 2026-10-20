using System.Collections.Generic;
using UnityEngine;

public class DestinationManager : MonoBehaviour
{
    [Header("目的地一覧")]
    [SerializeField, InspectorName("目的地データ")]
    [Tooltip("このステージで使用する目的地データです。")]
    private List<DestinationData> destinations = new List<DestinationData>();

    public IReadOnlyList<DestinationData> Destinations => destinations;
}
