using System.Collections.Generic;
using UnityEngine;
using TMPro;

//=^..^=   =^..^=   VERSION 1.1.1 (April 2026)    =^..^=    =^..^=
//                    Last Update 27/04/2026
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

//This avoids TMP filling when logging each game in simulations.
//Keeps a sort of buffer or queue.
public class LogSystem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private int maxLines = 100;

    private readonly Queue<string> logLines = new Queue<string>();

    private void Awake()
    {
        if (logText == null)
            logText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void AddLog(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;
        logLines.Enqueue(message);

        if (logLines.Count > maxLines)
            logLines.Dequeue();

        RefreshText();
    }

    public void ClearLogs()
    {
        logLines.Clear();
        RefreshText();
    }

    public TextMeshProUGUI LogText => logText;

    private void RefreshText()
    {
        if (logText == null)
            return;
        logText.text = string.Join("\n", logLines.ToArray());
    }
}