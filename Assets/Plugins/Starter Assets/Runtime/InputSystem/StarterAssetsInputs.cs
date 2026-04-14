using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        [Header("Custom Actions")]
        public bool castSpell; // Nouvelle touche E pour lancer un sort
        public bool rKey;      // Nouvelle touche R
        public bool ICE;      // Nouvelle touche R

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
        public void OnMove(InputValue value)
        {
            MoveInput(value.Get<Vector2>());
        }

        public void OnLook(InputValue value)
        {
            if (cursorInputForLook)
            {
                LookInput(value.Get<Vector2>());
            }
        }

        public void OnJump(InputValue value)
        {
            JumpInput(value.isPressed);
        }

        public void OnSprint(InputValue value)
        {
            SprintInput(value.isPressed);
        }

        // Nouvelle action pour E
        public void OnCastSpell(InputValue value)
        {
            CastSpellInput(value.isPressed);
        }

        // Nouvelle action pour R
        public void OnRKey(InputValue value)
        {
            RKeyInput(value.isPressed);
        }

         // Nouvelle action pour V
        public void OnICE(InputValue value)
        {
            ICEInput(value.isPressed);
        }
#endif

        // Méthodes pour mettre à jour les valeurs
        public void MoveInput(Vector2 newMoveDirection)
        {
            move = newMoveDirection;
        }

        public void LookInput(Vector2 newLookDirection)
        {
            look = newLookDirection;
        }

        public void JumpInput(bool newJumpState)
        {
            jump = newJumpState;
        }

        public void SprintInput(bool newSprintState)
        {
            sprint = newSprintState;
        }

        public void CastSpellInput(bool newState)
        {
            castSpell = newState;
        }

        public void RKeyInput(bool newState)
        {
            rKey = newState;
        }

         public void ICEInput(bool newState)
        {
            ICE = newState;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorState(cursorLocked);
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
