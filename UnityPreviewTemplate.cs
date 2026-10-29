namespace Landoria.ModelForge
{
    // Runtime controls and wind for scenes opened in the generated Unity project.
    internal static class UnityPreviewTemplate
    {
        internal const string Controller = @"using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class ModelForgePreviewController : MonoBehaviour
{
    public Camera view;
    public Animator animator;
    public AudioClip walkSound;
    public AudioClip runSound;
    public AudioClip jumpSound;
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpHeight = 1.3f;
    private CharacterController motor;
    private AudioSource audioSource;
    private float yaw;
    private float pitch = 15f;
    private float distance = 3f;
    private float verticalSpeed;
    private float stepTimer;

    private void Start()
    {
        motor = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); }
        if (view == null) { view = Camera.main; }
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Input.GetMouseButtonDown(0))
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (Cursor.lockState != CursorLockMode.Locked) { return; }
        yaw += Input.GetAxis(""Mouse X"") * 3f;
        pitch = Mathf.Clamp(pitch - Input.GetAxis(""Mouse Y"") * 3f, -70f, 75f);
        distance = Mathf.Clamp(distance - Input.GetAxis(""Mouse ScrollWheel"") * 4f, 0f, 8f);
        var heading = Quaternion.Euler(0f, yaw, 0f);
        var input = new Vector3(Input.GetAxisRaw(""Horizontal""), 0f,
            Input.GetAxisRaw(""Vertical""));
        input = Vector3.ClampMagnitude(input, 1f);
        var running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var speed = running ? runSpeed : walkSpeed;
        motor.Move(heading * input * speed * Time.deltaTime);
        if (motor.isGrounded)
        {
            verticalSpeed = -1f;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalSpeed = Mathf.Sqrt(2f * 20f * jumpHeight);
                if (animator != null) { animator.SetTrigger(""Jump""); }
                Play(jumpSound);
            }
        }
        verticalSpeed -= 20f * Time.deltaTime;
        motor.Move(Vector3.up * verticalSpeed * Time.deltaTime);
        transform.rotation = heading;
        if (animator != null)
        { animator.SetFloat(""Speed"", input.sqrMagnitude > .01f ? running ? 2f : 1f : 0f); }
        UpdateFootsteps(input.sqrMagnitude > .01f && motor.isGrounded, running);
        UpdateCamera();
    }

    private void UpdateFootsteps(bool moving, bool running)
    {
        if (!moving) { stepTimer = 0f; return; }
        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) { return; }
        Play(running ? runSound : walkSound);
        stepTimer = running ? .32f : .5f;
    }

    private void UpdateCamera()
    {
        if (view == null) { return; }
        var rotation = Quaternion.Euler(pitch, yaw, 0f);
        var target = transform.position + Vector3.up * 1.55f;
        var desired = distance;
        if (distance > 0f && Physics.SphereCast(target, .15f,
            rotation * Vector3.back, out var hit, distance))
        { desired = Mathf.Max(.05f, hit.distance - .1f); }
        view.transform.SetPositionAndRotation(target + rotation * Vector3.back * desired, rotation);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) { audioSource.PlayOneShot(clip); }
    }
}
";

        internal const string Wind = @"using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class ModelForgeWindPreview : MonoBehaviour
{
    public Vector4 wind1 = new Vector4(1f, 0f, 0f, .5f);
    public Vector4 wind2 = new Vector4(1f, 0f, 0f, .5f);
    public float windAlpha;
#if UNITY_EDITOR
    private double nextRepaint;
    private void OnEnable() { EditorApplication.update += EditorTick; }
    private void OnDisable() { EditorApplication.update -= EditorTick; }
    private void EditorTick()
    {
        if (Application.isPlaying || EditorApplication.timeSinceStartup < nextRepaint) { return; }
        nextRepaint = EditorApplication.timeSinceStartup + .1;
        SetWind();
        SceneView.RepaintAll();
    }
#endif
    private void Update() { SetWind(); }
    private void SetWind()
    {
        Shader.SetGlobalVector(""_GlobalWind1"", wind1);
        Shader.SetGlobalVector(""_GlobalWind2"", wind2);
        Shader.SetGlobalFloat(""_GlobalWindAlpha"", windAlpha);
        var blended = Vector4.Lerp(wind1, wind2, windAlpha);
        Shader.SetGlobalVector(""_GlobalWindForce"", new Vector4(
            blended.x * blended.w, blended.y * blended.w, blended.z * blended.w, 0f));
    }
}
";

        internal const string Ambient = @"using UnityEngine;

public sealed class ModelForgeAmbientActor : MonoBehaviour
{
    public AudioClip idleSound;
    public string bundlePath;
    public string clipPath;
    public float interval = 12f;
    private static AssetBundle loadedBundle;
    private AudioSource source;
    private float timer;

    private void Start()
    {
        if (idleSound == null && !string.IsNullOrEmpty(clipPath))
        {
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle.Contains(clipPath)) { loadedBundle = bundle; break; }
            }
            if (loadedBundle == null && System.IO.File.Exists(bundlePath))
            { loadedBundle = AssetBundle.LoadFromFile(bundlePath); }
            if (loadedBundle != null)
            { idleSound = loadedBundle.LoadAsset<AudioClip>(clipPath); }
        }
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.maxDistance = 18f;
        source.volume = .4f;
        timer = Random.Range(2f, interval);
    }

    private void Update()
    {
        if (idleSound == null) { return; }
        timer -= Time.deltaTime;
        if (timer > 0f) { return; }
        source.PlayOneShot(idleSound);
        timer = interval + Random.Range(-3f, 3f);
    }
}
";

        internal const string Animation = @"using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class ModelForgeRuntimeAnimator : MonoBehaviour
{
    public Animator animator;
    public string bundlePath;
    public string prefabPath;

    private void Awake()
    {
        if (animator == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrEmpty(prefabPath)) { return; }
        AssetBundle bundle = null;
        foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (loaded.Contains(prefabPath)) { bundle = loaded; break; }
        }
        if (bundle == null && File.Exists(bundlePath))
        { bundle = AssetBundle.LoadFromFile(bundlePath); }
        if (bundle == null) { return; }
        var original = bundle.LoadAsset<GameObject>(prefabPath);
        var source = original != null ? original.GetComponentInChildren<Animator>(true) : null;
        if (source == null || source.runtimeAnimatorController == null) { return; }
        var clips = source.runtimeAnimatorController.animationClips;
        var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        for (var i = 0; i < overrides.Count; i++)
        {
            var placeholder = overrides[i].Key;
            if (placeholder == null) { continue; }
            foreach (var clip in clips)
            {
                if (clip.name != placeholder.name) { continue; }
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(placeholder, clip);
                break;
            }
        }
        controller.ApplyOverrides(overrides);
        animator.runtimeAnimatorController = controller;
    }
}
";
    }
}
