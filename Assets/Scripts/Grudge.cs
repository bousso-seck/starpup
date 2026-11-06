using UnityEngine;
using UnityEngine.SceneManagement;

// The hidden grudge tally. Every enemy that is defeated, or beaten by dodging until it walks away,
// adds to it according to the consequence it ended with:
//   Full haunt  +3   (a direct strike, or defeating them)
//   Mild haunt  +1   (dodged until tired, or a parry on a possessed classmate)
//   Respect      0   (parried well: the best result)
// The Level 1 boss (the gym teacher) reads the total when the fight starts and becomes stronger.
// Nothing is shown on screen. The Console prints each change so you can test it.
// Points = this level's tally (the gym teacher reads this). It starts again from zero every time the scene loads.
// Carried = what earlier levels banked. LevelGoal calls Grudge.Bank() when a level is completed, so a final boss
// can read Grudge.Total (carried + this level). Call Grudge.NewGame() from the title screen's Play button.
public static class Grudge
{
    public const int FullHauntPoints = 3;
    public const int MildHauntPoints = 1;
    public const int MaxPoints = 9;     // three full haunts = the strongest boss

    public static int Points { get; private set; }        // this level only
    public static int Carried { get; private set; }       // banked from finished levels
    public static int Total { get { return Carried + Points; } }

    // Level finished: keep this level's grudge for the later bosses.
    public static void Bank()
    {
        Carried += Points;
        Debug.Log("Grudge banked. Carried total: " + Carried);
    }

    // Starting a brand new run: forget everything.
    public static void NewGame()
    {
        Carried = 0;
        Points = 0;
    }

    // 0 = no grudge at all, 1 = the most the boss can get.
    public static float Strength { get { return Mathf.Clamp01(Points / (float)MaxPoints); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Points = 0;
        Carried = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded; // so it is only ever hooked up once
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Points = 0;
    }

    public static void Record(Enemy.Consequence consequence)
    {
        int add = 0;
        if (consequence == Enemy.Consequence.FullHaunt) add = FullHauntPoints;
        else if (consequence == Enemy.Consequence.MildHaunt) add = MildHauntPoints;

        Points = Mathf.Min(MaxPoints, Points + add);
        Debug.Log("Grudge: " + consequence + " adds " + add + ". Total " + Points + "/" + MaxPoints + ".");
    }
}