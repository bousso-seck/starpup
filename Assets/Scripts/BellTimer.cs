using TMPro;
using UnityEngine;

public class BellTimer : MonoBehaviour
{
    public float remainingTime; // the remaining time for the timer
    public float goHomeDelay = 1.5f; // delay before calling GoHome function
    [SerializeField] TextMeshProUGUI timerText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(remainingTime > 0) // if the remaining time is greater than 0 keep counting down
        {
            remainingTime -= Time.deltaTime;
        }
        else if (remainingTime < 0) // if timer is less than 0 call TimesUp() function
        {
            TimesUp();
        }
        int minutes = Mathf.FloorToInt(remainingTime / 60); // gets minutes from remaining time / 60
        int seconds = Mathf.FloorToInt(remainingTime % 60); // gets seconds from remaining time modulo 60
        
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds); // puts remaining time in timer format
    }

    void GoHome()
    {
        // For now "going home" just restarts the scene. The home screen and route choice come later.
        DevTools.Restart();
    }
    void TimesUp()
    {
        remainingTime = 0; // remaining time is set to 0
        timerText.color = Color.red; // timer text color is set to red
        Debug.Log("TIMES UP!"); // prints "TIMES UP!" in the console
        Invoke(nameof(GoHome), goHomeDelay); // calls GoHome function after delay
    }
}
