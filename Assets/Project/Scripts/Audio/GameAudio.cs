using UnityEngine;

public class GameAudio : MonoBehaviour
{
    [Header("Sources (emisores de eventos)")]
    [SerializeField] private Weapon weapon;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerOxygen oxygen;
    [SerializeField] private ShipRepair shipRepair;
    [SerializeField] private EnemyFactory enemyFactory;
    [SerializeField] private Health playerHealth;

    [Header("Clips")]
    [SerializeField] private AudioClip shoot;
    [SerializeField] private AudioClip jump;
    [SerializeField] private AudioClip[] footsteps;
    [SerializeField] private AudioClip enemyDeath;
    [SerializeField] private AudioClip oxygenRefillLoop;
    [SerializeField] private AudioClip repair;
    [SerializeField] private AudioClip ambience;
    [SerializeField] private AudioClip hurt;
    [SerializeField] private AudioClip heal;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float shootVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float stepVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;

    private AudioSource sfx;
    private AudioSource fixedSfx;
    private AudioSource loop;
    private AudioSource music;
    private float lastHealth;

    private void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        fixedSfx = gameObject.AddComponent<AudioSource>();
        fixedSfx.playOnAwake = false;

        loop = gameObject.AddComponent<AudioSource>();
        loop.playOnAwake = false;
        loop.loop = true;
        loop.clip = oxygenRefillLoop;
        loop.volume = 0.5f;

        music = gameObject.AddComponent<AudioSource>();
        music.clip = ambience;
        music.loop = true;
        music.volume = ambienceVolume;
        music.ignoreListenerPause = true;
        music.Play();
    }

    private void OnEnable()
    {
        weapon.OnFired += PlayShoot;
        player.OnJumped += PlayJump;
        player.OnFootstep += PlayStep;
        oxygen.OnRefillingChanged += SetRefilling;
        shipRepair.OnProgressChanged += PlayRepair;
        enemyFactory.OnCreatureKilled += PlayEnemyDeath;
        playerHealth.OnHealthChanged += OnPlayerHealthChanged;
    }

    private void OnDisable()
    {
        weapon.OnFired -= PlayShoot;
        player.OnJumped -= PlayJump;
        player.OnFootstep -= PlayStep;
        oxygen.OnRefillingChanged -= SetRefilling;
        shipRepair.OnProgressChanged -= PlayRepair;
        enemyFactory.OnCreatureKilled -= PlayEnemyDeath;
        playerHealth.OnHealthChanged -= OnPlayerHealthChanged;
    }

    private void PlayShoot() => PlayVaried(shoot, shootVolume, 0.08f);
    private void PlayJump() => PlayVaried(jump, 0.8f, 0.05f);
    private void PlayRepair(float progress) => fixedSfx.PlayOneShot(repair, 0.9f);

    private void Start()
    {
        lastHealth = playerHealth.Current;
        GameManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnGameStateChanged;
        AudioListener.pause = false;
    }

    private void OnGameStateChanged(GameState state) => AudioListener.pause = state != GameState.Playing;

    private void PlayStep()
    {
        if (footsteps == null || footsteps.Length == 0) return;
        PlayVaried(footsteps[Random.Range(0, footsteps.Length)], stepVolume, 0.1f);
    }

    private void PlayEnemyDeath(Vector3 position) => AudioSource.PlayClipAtPoint(enemyDeath, position, 1f);

    private void OnPlayerHealthChanged(float current, float max)
    {
        if (current < lastHealth) PlayVaried(hurt, 0.9f, 0.08f);
        else if (current > lastHealth) fixedSfx.PlayOneShot(heal, 0.8f);
        lastHealth = current;
    }

    private void SetRefilling(bool refilling)
    {
        if (refilling && !loop.isPlaying) loop.Play();
        else if (!refilling) loop.Stop();
    }

    private void PlayVaried(AudioClip clip, float volume, float pitchRange)
    {
        sfx.pitch = 1f + Random.Range(-pitchRange, pitchRange);
        sfx.PlayOneShot(clip, volume);
    }
}
