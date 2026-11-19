using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mourneveil
{
    /// <summary>
    /// Small pointer-event bridge, configured by MourneveilPrologue at runtime.
    /// Do not attach this manually in the Inspector.
    /// </summary>
    public sealed class MourneveilVialHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private int index;
        private Action<int, bool> callback;

        public void Configure(int vialIndex, Action<int, bool> hoverCallback)
        {
            index = vialIndex;
            callback = hoverCallback;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (callback != null) callback(index, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (callback != null) callback(index, false);
        }
    }
}
