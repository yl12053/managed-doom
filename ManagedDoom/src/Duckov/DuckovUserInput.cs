using System;
using Duckov.MiniGames;
using ManagedDoom.UserInput;
using UnityEngine;

namespace ManagedDoom.Duckov
{
    public class DuckovUserInput: IDisposable, IUserInput
    {
        private Config config;

        private bool[] weaponKeys;
        private int turnHeld;
        
        private bool mouseGrabbed;
        private float mouseX;
        private float mouseY;
        private float mousePrevX;
        private float mousePrevY;
        private float mouseDeltaX;
        private float mouseDeltaY;
        private MiniGame miniGame;

        public DuckovUserInput(Config config, DuckovDoom doom, bool useMouse, MiniGame mini)
        {
            try
            {
                Debug.Log("Initialize user input: ");

                miniGame = mini;

                this.config = config;

                weaponKeys = new bool[7];
                turnHeld = 0;

                if (useMouse)
                {
                    mouseGrabbed = false;
                }

                Debug.Log("OK");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                Dispose();
            }
        }
        
        public void BuildTicCmd(TicCmd cmd)
        {
            var keyForward = miniGame.GetButton(MiniGame.Button.Up);
            var keyBackward = miniGame.GetButton(MiniGame.Button.Down);
            var keyTurnLeft = Input.GetKey(config.key_turnleft);
            var keyTurnRight = Input.GetKey(config.key_turnright);
            var keyStrafeLeft = miniGame.GetButton(MiniGame.Button.Left);
            var keyStrafeRight = miniGame.GetButton(MiniGame.Button.Right);
            var keyFire = miniGame.GetButton(MiniGame.Button.A);
            var keyUse = miniGame.GetButton(MiniGame.Button.B) || miniGame.GetButton(MiniGame.Button.Start);
            var keyRun = Input.GetKey(config.key_run);
            var keyStrafe = Input.GetKey(config.key_strafe);

            weaponKeys[0] = Input.GetKey(KeyCode.Alpha1);
            weaponKeys[1] = Input.GetKey(KeyCode.Alpha2);
            weaponKeys[2] = Input.GetKey(KeyCode.Alpha3);
            weaponKeys[3] = Input.GetKey(KeyCode.Alpha4);
            weaponKeys[4] = Input.GetKey(KeyCode.Alpha5);
            weaponKeys[5] = Input.GetKey(KeyCode.Alpha6);
            weaponKeys[6] = Input.GetKey(KeyCode.Alpha7);

            cmd.Clear();

            var strafe = keyStrafe;
            var speed = keyRun ? 1 : 0;
            var forward = 0;
            var side = 0;

            if (config.game_alwaysrun)
            {
                speed = 1 - speed;
            }

            if (keyTurnLeft || keyTurnRight)
            {
                turnHeld++;
            }
            else
            {
                turnHeld = 0;
            }

            int turnSpeed;
            if (turnHeld < PlayerBehavior.SlowTurnTics)
            {
                turnSpeed = 2;
            }
            else
            {
                turnSpeed = speed;
            }

            if (strafe)
            {
                if (keyTurnRight)
                {
                    side += PlayerBehavior.SideMove[speed];
                }
                if (keyTurnLeft)
                {
                    side -= PlayerBehavior.SideMove[speed];
                }
            }
            else
            {
                if (keyTurnRight)
                {
                    cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
                if (keyTurnLeft)
                {
                    cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
            }

            if (keyForward)
            {
                forward += PlayerBehavior.ForwardMove[speed];
            }
            if (keyBackward)
            {
                forward -= PlayerBehavior.ForwardMove[speed];
            }

            if (keyStrafeLeft)
            {
                side -= PlayerBehavior.SideMove[speed];
            }
            if (keyStrafeRight)
            {
                side += PlayerBehavior.SideMove[speed];
            }

            if (keyFire)
            {
                cmd.Buttons |= TicCmdButtons.Attack;
            }

            if (keyUse)
            {
                cmd.Buttons |= TicCmdButtons.Use;
            }

            // Check weapon keys.
            for (var i = 0; i < weaponKeys.Length; i++)
            {
                if (weaponKeys[i])
                {
                    cmd.Buttons |= TicCmdButtons.Change;
                    cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
                    break;
                }
            }

            UpdateMouse();
            var ms = 0.5F * config.mouse_sensitivity * 0.5f; // double half
            var mx = (int)MathF.Round(ms * mouseDeltaX);
            var my = (int)MathF.Round(ms * -mouseDeltaY);
            forward += my;
            if (strafe)
            {
                side += mx * 2;
            }
            else
            {
                cmd.AngleTurn -= (short)(mx * 0x8);
            }

            if (forward > PlayerBehavior.MaxMove)
            {
                forward = PlayerBehavior.MaxMove;
            }
            else if (forward < -PlayerBehavior.MaxMove)
            {
                forward = -PlayerBehavior.MaxMove;
            }
            if (side > PlayerBehavior.MaxMove)
            {
                side = PlayerBehavior.MaxMove;
            }
            else if (side < -PlayerBehavior.MaxMove)
            {
                side = -PlayerBehavior.MaxMove;
            }

            cmd.ForwardMove += (sbyte)forward;
            cmd.SideMove += (sbyte)side;
        }

        private void UpdateMouse()
        {
            if (mouseGrabbed)
            {
                mousePrevX = mouseX;
                mousePrevY = mouseY;
                mouseX = Input.mousePosition.x;
                mouseY = Input.mousePosition.y;
                mouseDeltaX = miniGame.GetAxis(1).x;
                mouseDeltaY = miniGame.GetAxis(1).y * -1f;
                
                if (config.mouse_disableyaxis)
                {
                    mouseDeltaY = 0;
                }
            }
        }
        
        public void Reset()
        {
            mouseX = Input.mousePosition.x;
            mouseY = Input.mousePosition.y;
            mousePrevX = mouseX;
            mousePrevY = mouseY;
            mouseDeltaX = 0;
            mouseDeltaY = 0;
        }
        
        public void GrabMouse()
        {
            if (!mouseGrabbed)
            {
                mouseGrabbed = true;
                mouseX = Input.mousePosition.x;
                mouseY = Input.mousePosition.y;
                mousePrevX = mouseX;
                mousePrevY = mouseY;
                mouseDeltaX = 0;
                mouseDeltaY = 0;
            }
        }

        public void ReleaseMouse()
        {
            if (mouseGrabbed)
            {
                mouseGrabbed = false;
            }
        }

        public void Dispose()
        {
            
        }
        
        public int MaxMouseSensitivity
        {
            get
            {
                return 15;
            }
        }
        
        public int MouseSensitivity
        {
            get
            {
                return config.mouse_sensitivity;
            }

            set
            {
                config.mouse_sensitivity = value;
            }
        } 
        
        public static DoomKey CodeToDoom(KeyCode silkKey)
        {
            switch (silkKey)
            {
                case KeyCode.Space: return DoomKey.Space;
                // case KeyCode.Apostrophe: return DoomKey.Apostrophe;
                case KeyCode.Comma: return DoomKey.Comma;
                case KeyCode.Minus: return DoomKey.Subtract;
                case KeyCode.Period: return DoomKey.Period;
                case KeyCode.Slash: return DoomKey.Slash;
                case KeyCode.Alpha0: return DoomKey.Num0;
                // case KeyCode.D0: return DoomKey.D0;
                case KeyCode.Alpha1: return DoomKey.Num1;
                case KeyCode.Alpha2: return DoomKey.Num2;
                case KeyCode.Alpha3: return DoomKey.Num3;
                case KeyCode.Alpha4: return DoomKey.Num4;
                case KeyCode.Alpha5: return DoomKey.Num5;
                case KeyCode.Alpha6: return DoomKey.Num6;
                case KeyCode.Alpha7: return DoomKey.Num7;
                case KeyCode.Alpha8: return DoomKey.Num8;
                case KeyCode.Alpha9: return DoomKey.Num9;
                case KeyCode.Semicolon: return DoomKey.Semicolon;
                case KeyCode.Equals: return DoomKey.Equal;
                case KeyCode.A: return DoomKey.A;
                case KeyCode.B: return DoomKey.B;
                case KeyCode.C: return DoomKey.C;
                case KeyCode.D: return DoomKey.D;
                case KeyCode.E: return DoomKey.E;
                case KeyCode.F: return DoomKey.F;
                case KeyCode.G: return DoomKey.G;
                case KeyCode.H: return DoomKey.H;
                case KeyCode.I: return DoomKey.I;
                case KeyCode.J: return DoomKey.J;
                case KeyCode.K: return DoomKey.K;
                case KeyCode.L: return DoomKey.L;
                case KeyCode.M: return DoomKey.M;
                case KeyCode.N: return DoomKey.N;
                case KeyCode.O: return DoomKey.O;
                case KeyCode.P: return DoomKey.P;
                case KeyCode.Q: return DoomKey.Q;
                case KeyCode.R: return DoomKey.R;
                case KeyCode.S: return DoomKey.S;
                case KeyCode.T: return DoomKey.T;
                case KeyCode.U: return DoomKey.U;
                case KeyCode.V: return DoomKey.V;
                case KeyCode.W: return DoomKey.W;
                case KeyCode.X: return DoomKey.X;
                case KeyCode.Y: return DoomKey.Y;
                case KeyCode.Z: return DoomKey.Z;
                case KeyCode.LeftBracket: return DoomKey.LBracket;
                case KeyCode.Backslash: return DoomKey.Backslash;
                case KeyCode.RightBracket: return DoomKey.RBracket;
                case KeyCode.Tilde: return DoomKey.Escape;
                case KeyCode.BackQuote: return DoomKey.Escape;
                // case KeyCode.World1: return DoomKey.World1;
                // case KeyCode.World2: return DoomKey.World2;
                case KeyCode.Escape: return DoomKey.Escape;
                case KeyCode.Return: return DoomKey.Enter;
                case KeyCode.Tab: return DoomKey.Tab;
                case KeyCode.Backspace: return DoomKey.Backspace;
                case KeyCode.Insert: return DoomKey.Insert;
                case KeyCode.Delete: return DoomKey.Delete;
                case KeyCode.RightArrow: return DoomKey.Right;
                case KeyCode.LeftArrow: return DoomKey.Left;
                case KeyCode.DownArrow: return DoomKey.Down;
                case KeyCode.UpArrow: return DoomKey.Up;
                case KeyCode.PageUp: return DoomKey.PageUp;
                case KeyCode.PageDown: return DoomKey.PageDown;
                case KeyCode.Home: return DoomKey.Home;
                case KeyCode.End: return DoomKey.End;
                // case KeyCode.CapsLock: return DoomKey.CapsLock;
                // case KeyCode.ScrollLock: return DoomKey.ScrollLock;
                // case KeyCode.NumLock: return DoomKey.NumLock;
                // case KeyCode.PrintScreen: return DoomKey.PrintScreen;
                case KeyCode.Pause: return DoomKey.Pause;
                case KeyCode.F1: return DoomKey.F1;
                case KeyCode.F2: return DoomKey.F2;
                case KeyCode.F3: return DoomKey.F3;
                case KeyCode.F4: return DoomKey.F4;
                case KeyCode.F5: return DoomKey.F5;
                case KeyCode.F6: return DoomKey.F6;
                case KeyCode.F7: return DoomKey.F7;
                case KeyCode.F8: return DoomKey.F8;
                case KeyCode.F9: return DoomKey.F9;
                case KeyCode.F10: return DoomKey.F10;
                case KeyCode.F11: return DoomKey.F11;
                case KeyCode.F12: return DoomKey.F12;
                case KeyCode.F13: return DoomKey.F13;
                case KeyCode.F14: return DoomKey.F14;
                case KeyCode.F15: return DoomKey.F15;
                // case KeyCode.F16: return DoomKey.F16;
                // case KeyCode.F17: return DoomKey.F17;
                // case KeyCode.F18: return DoomKey.F18;
                // case KeyCode.F19: return DoomKey.F19;
                // case KeyCode.F20: return DoomKey.F20;
                // case KeyCode.F21: return DoomKey.F21;
                // case KeyCode.F22: return DoomKey.F22;
                // case KeyCode.F23: return DoomKey.F23;
                // case KeyCode.F24: return DoomKey.F24;
                // case KeyCode.F25: return DoomKey.F25;
                case KeyCode.Keypad0: return DoomKey.Numpad0;
                case KeyCode.Keypad1: return DoomKey.Numpad1;
                case KeyCode.Keypad2: return DoomKey.Numpad2;
                case KeyCode.Keypad3: return DoomKey.Numpad3;
                case KeyCode.Keypad4: return DoomKey.Numpad4;
                case KeyCode.Keypad5: return DoomKey.Numpad5;
                case KeyCode.Keypad6: return DoomKey.Numpad6;
                case KeyCode.Keypad7: return DoomKey.Numpad7;
                case KeyCode.Keypad8: return DoomKey.Numpad8;
                case KeyCode.Keypad9: return DoomKey.Numpad9;
                // case KeyCode.KeypadDecimal: return DoomKey.Decimal;
                case KeyCode.KeypadDivide: return DoomKey.Divide;
                case KeyCode.KeypadMultiply: return DoomKey.Multiply;
                case KeyCode.KeypadMinus: return DoomKey.Subtract;
                case KeyCode.KeypadPlus: return DoomKey.Add;
                case KeyCode.KeypadEnter: return DoomKey.Enter;
                case KeyCode.KeypadEquals: return DoomKey.Equal;
                case KeyCode.LeftShift: return DoomKey.LShift;
                case KeyCode.LeftControl: return DoomKey.LControl;
                case KeyCode.LeftAlt: return DoomKey.LAlt;
                // case KeyCode.SuperLeft: return DoomKey.SuperLeft;
                case KeyCode.RightShift: return DoomKey.RShift;
                case KeyCode.RightCommand: return DoomKey.RControl;
                case KeyCode.RightAlt: return DoomKey.RAlt;
                // case KeyCode.SuperRight: return DoomKey.SuperRight;
                case KeyCode.Menu: return DoomKey.Menu;
                default: return DoomKey.Unknown;
            }
        }
    }
}