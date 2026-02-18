using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour {
    private PlayerInputActions input;
    private Rigidbody2D rb;

    [Header("��������")]
    public float moveSpeed = 5f;
    private Vector2 moveInput;

    [Header("������")]
    public float dodgeForce = 8f;
    public float dodgeCooldown = 1f;
    private bool canDodge = true;

    [Header("�����")]
    public GameObject projectilePrefab; // ������ ���� ��� ������� �����
    public float attackCooldown = 0.5f;
    private bool canAttack = true;

    private void Awake() {
        input = new PlayerInputActions();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable() {
        input.Player.Enable();

        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        input.Player.Attack.performed += _ => TryAttack();
    }

    private void OnDisable() {
        input.Player.Disable();
    }

    private void FixedUpdate() {
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void TryDodge() {
        if (!canDodge || moveInput == Vector2.zero)
            return;

        canDodge = false;
        rb.AddForce(moveInput.normalized * dodgeForce, ForceMode2D.Impulse);
        Invoke(nameof(ResetDodge), dodgeCooldown);
    }

    private void ResetDodge() => canDodge = true;

    private void TryAttack() {
        if (!canAttack)
            return;

        canAttack = false;

        if (projectilePrefab != null) {
            GameObject proj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            Rigidbody2D prb = proj.GetComponent<Rigidbody2D>();
            if (prb != null)
                prb.linearVelocity = moveInput.normalized * 10f;
        }

        Invoke(nameof(ResetAttack), attackCooldown);
    }

    private void ResetAttack() => canAttack = true;
}
