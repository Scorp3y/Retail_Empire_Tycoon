using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpeechBubble;

public class SpeechBubbleExample : MonoBehaviour
{
    private int counter=0;

    [SerializeField]
    private SpeechBubble_TMP speechBubble;

    [SerializeField]
    private SpeechBubbleStyle defaultStyle;
    [SerializeField]
    private SpeechBubbleStyle darkStyle;

    // Start is called before the first frame update
    void Start()
    {
        showCorrectSlide();        
    }

    private void showCorrectSlide()
    {
        counter = Mathf.Clamp(counter, 0, 10);

        switch (counter)
        {
            case 0:
                speechBubble.setDialogueText("Hello there!\nThis is a \'Square\' textbox.\nIt is good for conveying factual information such as this!");
                speechBubble.setBubbleType(SpeechBubbleType.Square);
                break;
            case 1:
                speechBubble.setDialogueText("This is an \'Octagon\' textbox.\nIt is another good for conveying factual information.");
                speechBubble.setBubbleType(SpeechBubbleType.Octagon);
                break;
            case 2:
                speechBubble.setDialogueText("This is a \'Note\' textbox.\nIt is the third way to convey factual information.");
                speechBubble.setBubbleType(SpeechBubbleType.Note);
                break;
            case 3:
                speechBubble.setDialogueText("There are too many factual information dialogue boxes to choose from!\nUse the \'Stress\' textbox when a character is losing their sanity.");
                speechBubble.setBubbleType(SpeechBubbleType.Stress);
                break;
            case 4:
                speechBubble.setDialogueText("Character 1: You got this!!!\nCharacter 2: WHAT?\nCharacter 1: You need hearing aids! I was even using the \'Yell\' box to talk loud!");
                speechBubble.setBubbleType(SpeechBubbleType.Yell);
                break;
            case 5:
                speechBubble.setDialogueText("Am I deep in thought or just daydreaming about flying hotdogs?\nThe \'Think\' box is good for when a character is pondering these deep thoughts.");
                speechBubble.setBubbleType(SpeechBubbleType.Think);
                break;
            case 6:
                speechBubble.setDialogueText("I have a secret to tell you... But I don't want anyone to hear it so I am using the \'Whisper\' box to speak quietly.");
                speechBubble.setBubbleType(SpeechBubbleType.Whisper);
                break;
            case 7:
                speechBubble.setDialogueText("In addition to Speech Bubble Type's, you can also create Speech Bubble Styles to quickly change the colors of a textbox.\nDoes this dark text box style look cooler?");
                speechBubble.setBubbleType(SpeechBubbleType.Square);
                speechBubble.setStyle(darkStyle);
                break;
            case 8:
                speechBubble.setDialogueText("You can even edit the colors individually instead of using a Speech Bubble Style! ");
                //colors can be set by using a predifined color...
                speechBubble.setFillColor(Color.cyan);
                speechBubble.setDialogueTextColor(Color.blue);

                //or by using a custom color
                speechBubble.setOutlineColor(new Color(0, 0.5f, 0.7f));
                break;
            case 9:
                speechBubble.setDialogueText("If you are done with using the custom colors, you can easily go back to the colors of the current style by using revertStyle() function.");
                speechBubble.revertStyle();
                break;
            case 10:
                speechBubble.setDialogueText("Enjoy the speech bubble!");
                break;

        }

       


    }


    public void nextButtonPushed()
    {
        counter += 1;
        showCorrectSlide();
    }

    public void previousButtonPushed()
    {
        counter -= 1;

        showCorrectSlide();
    }

}
