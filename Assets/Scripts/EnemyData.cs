using UnityEngine;

// A "settings sheet" for one classmate. Create one per enemy:
// right-click in the Project panel > Create > Starpup > Enemy Data.
// The Enemy script reads these numbers, so new enemies need no new code.
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Starpup/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Test Classmate";
    public bool isPossessed = false;    // tick for possessed classmates: any parry counts as an attack (a mild haunt)
                                        // (leave off for rivals, who respect parries up to the Haunt Line)
    public Color bodyColor = new Color(1f, 0.55f, 0.1f);

    [Header("Bars")]
    public float maxHealth = 100f;
    public float maxStamina = 100f;
    // How many times more dodges than strikes it takes to beat this enemy.
    // 1.5 = dodging takes 50% longer. The game works out the stamina drain per miss from this.
    public float dodgeRatio = 1.5f;

    [Header("Awareness")]
    public float noticeRange = 7f;      // how close the pupcat must get before this enemy wakes up and attacks.
                                        // 0 = always awake. About 9 is the edge of the screen. Click the enemy in
                                        // the Scene view to see the range as a yellow box.

    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float recoverChase = 0.5f;   // 0 = stands still after a swing, 1 = keeps chasing at full speed

    [Header("Attack")]
    public float attackRange = 2f;      // how close they get before winding up
    public float hitRange = 2.2f;       // how far the swing reaches sideways
    public float hitHeight = 1.2f;      // how high the swing reaches (jump over it!)
    public float attackDamage = 2f;     // damage of a FULL attack (all its swings). 2 = one heart.
                                        // Each swing deals its share: 2 swings = 1 point (half a heart) each.
    public float telegraphTime = 0.2f;  // wind-up warning for the first swing, in seconds
    public float recoverTime = 0.3f;    // pause after the last swing
    public float exhaustedTime = 2f;    // how long they walk away embarrassed

    [Header("Combo")]
    public int swingsPerAttack = 2;         // swings in a row before they pause
    public float comboTelegraphTime = 0.2f; // wind-up for each follow-up swing
    public float comboGap = 0.15f;          // pause between swings in a combo

    [Header("Parry")]
    public bool canBeParried = true;    // turn off for enemies whose swings can't be parried
    public float parryStunTime = 1.2f;  // how long a parried enemy is staggered
    public float parryDamage = 8f;      // health a parry takes off (parrying counts as attacking)
    public float hauntLine = 0.4f;      // rivals only: parry damage over this share of max health = a full haunt

    [Header("Spirit")]
    public bool leavesSpirit = true;          // a defeated enemy lets out a spirit flame that flies off the screen
    public GameObject spiritFlamePrefab;      // optional: drag the Spirit Flame prefab here. Empty = a simple built-in flame.

    [Header("Parry stagger (who gets pushed back)")]
    public float parryPushEnemy = 4f;       // how hard the enemy is shoved back when parried
    public float parryEnemyStagger = 0.25f; // how long that shove lasts
    public float parryPushPupcat = 2f;      // how hard the pupcat is shoved back
    public float parryPupcatStagger = 0.12f; // how long the pupcat staggers
                                            // Tip: for a rival, make the pupcat push bigger than the enemy push.
}