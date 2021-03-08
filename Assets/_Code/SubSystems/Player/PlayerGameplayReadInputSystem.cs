using Latios;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace Lsss
{
    public class PlayerGameplayReadInputSystem : SubSystem
    {
        //Todo: Store this in a component or something.
        float2 m_integratedMouseDelta = 0f;

        protected override void OnCreate()
        {
            worldBlackboardEntity.AddComponentIfMissing<MouseLookMultiplier>();
        }

        protected override void OnUpdate()
        {
            Entities.WithAll<PlayerTag>().ForEach((ref ShipDesiredActions desiredActions) =>
            {
                var gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    desiredActions.turn = gamepad.leftStick.ReadValue();

                    bool  accelDown    = gamepad.aButton.isPressed;
                    bool  brakeDown    = gamepad.bButton.isPressed;
                    float stickGas     = gamepad.rightStick.ReadValue().y;
                    float accel        = math.select(0f, 1f, accelDown);
                    float brake        = math.select(0f, -1f, brakeDown);
                    desiredActions.gas = math.clamp(stickGas + accel + brake,
                                                    -1f,
                                                    1f);

                    desiredActions.boost = gamepad.leftTrigger.isPressed;

                    desiredActions.fire = gamepad.rightTrigger.isPressed;
                }
                else
                {
                    var mouse    = Mouse.current;
                    var keyboard = Keyboard.current;
                    if (mouse != null && keyboard != null)
                    {
                        UnityEngine.Cursor.lockState  = UnityEngine.CursorLockMode.Locked;
                        UnityEngine.Cursor.visible    = false;
                        float2 mouseDelta             = mouse.delta.ReadValue();
                        mouseDelta                   *= new float2(1920f, 1080f) / new float2(UnityEngine.Screen.width, UnityEngine.Screen.height);
                        mouseDelta                   *= math.radians(80f) / 1080f;  //FOV is 80
                        mouseDelta                   /= Time.DeltaTime;
                        mouseDelta                   *= worldBlackboardEntity.GetComponentData<MouseLookMultiplier>().multiplier;
                        if (mouse.rightButton.isPressed)
                        {
                            m_integratedMouseDelta += mouseDelta;
                            desiredActions.turn     = m_integratedMouseDelta;
                        }
                        else
                        {
                            m_integratedMouseDelta = 0f;
                            desiredActions.turn    = mouseDelta;
                        }

                        if (math.length(desiredActions.turn) > 1f)
                            desiredActions.turn = math.normalize(desiredActions.turn);

                        bool accelDown = keyboard.wKey.isPressed;
                        bool brakeDown = keyboard.sKey.isPressed;

                        desiredActions.gas = math.select(0f, 1f, accelDown) + math.select(0f, -1f, brakeDown);

                        desiredActions.boost = keyboard.spaceKey.isPressed;
                        desiredActions.fire  = mouse.leftButton.isPressed;
                    }
                }

                //Early quit
                {
                    var keyboard = Keyboard.current;
                    if (keyboard.nKey.isPressed && keyboard.digit0Key.isPressed)
                    {
                        var ecb                                                                 = latiosWorld.syncPoint.CreateEntityCommandBuffer();
                        ecb.AddComponent(sceneBlackboardEntity, new RequestLoadScene { newScene = "Title and Menu" });
                        UnityEngine.Cursor.lockState                                            = UnityEngine.CursorLockMode.None;
                        UnityEngine.Cursor.visible                                              = true;
                    }
                }
            }).WithoutBurst().Run();
        }
    }
}

