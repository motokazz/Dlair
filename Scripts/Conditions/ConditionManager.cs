
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ConditionManager : MonoBehaviour
{
    [System.Serializable]
    public class Condition
    {
        public SuccessBranch.SuccessCondition successCondition;
    }

    [SerializeField] private QTEInput qTEInput;

    public List<Condition> con;

    public async Task<int> ProcessConditions(List<SuccessBranch> sbs)
    {
        Debug.Log("ProcessConditions");
        foreach (SuccessBranch sb in sbs)
        {
            ProcessCondition(sb);
        }


        return sbs.Count;
    }
    
    public void ProcessCondition(SuccessBranch sb)
    {
        switch (sb.condition)
        {
            case SuccessBranch.SuccessCondition.QTE:
                QTE(sb);
                break;
        }
    }

    public void QTE(SuccessBranch sb)
    {
        qTEInput.inputStart = sb.inputStart;
        qTEInput.inputLimit = sb.inputWindow;
        qTEInput.QTEStart();
    }

}

