using TMPro;
using UnityEngine;

namespace Speedrun
{
    public class SpeedrunRow : MonoBehaviour
    {
        // We hide this from the inspector because we find it automatically now
        private TextMeshProUGUI rowText;

        void Awake()
        {
            // Automatically find the text component on this same GameObject
            rowText = GetComponent<TextMeshProUGUI>();

            // Error checking in case the wrong text type was used
            if (rowText == null)
            {
                Debug.LogError("SpeedrunRow: No TextMeshProUGUI component found! " +
                               "Make sure you created a 'UI > Text - TextMeshPro', NOT a standard 'Text'.");
            }
        }

        public void Setup(string textToDisplay)
        {
            if (rowText != null)
            {
                rowText.text = textToDisplay;
            }
        }
    }
}