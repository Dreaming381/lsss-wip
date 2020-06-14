using Latios;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace Lsss
{
    public class PlayerGameplayReadInputSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<PlayerTag>().ForEach((ref ShipDesiredActions desiredActions) =>
            {
                var gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    desiredActions.turn = gamepad.leftStick.ReadValue();

                    bool  accelDown    = gamepad.aButton.isPressed || gamepad.crossButton.isPressed;
                    bool  brakeDown    = gamepad.bButton.isPressed || gamepad.squareButton.isPressed;
                    float stickGas     = gamepad.rightStick.ReadValue().y;
                    float accel        = math.select(0f, 1f, accelDown);
                    float brake        = math.select(0f, -1f, brakeDown);
                    desiredActions.gas = math.clamp(stickGas + accel + brake, -1f, 1f);

                    desiredActions.boost = gamepad.leftTrigger.isPressed;

                    desiredActions.fire = gamepad.rightTrigger.isPressed;
                }
            }).WithoutBurst().Run();
        }
    }
}

