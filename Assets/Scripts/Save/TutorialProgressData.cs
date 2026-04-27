using System;
using System.Collections.Generic;

[Serializable]
public class TutorialProgressData
{
    public List<string> shownInstructionIds = new();

    public bool HasShown(string instructionId)
    {
        return shownInstructionIds.Contains(instructionId);
    }

    public void MarkAsShown(string instructionId)
    {
        if (string.IsNullOrWhiteSpace(instructionId))
            return;

        if (!shownInstructionIds.Contains(instructionId))
        {
            shownInstructionIds.Add(instructionId);
        }
    }

    public void Clear()
    {
        shownInstructionIds.Clear();
    }
}