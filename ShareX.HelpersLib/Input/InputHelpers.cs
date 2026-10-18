#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Drawing;

namespace ShareX.HelpersLib
{
    public static class InputHelpers
    {
        public static bool SendKeyDown(VirtualKeyCode keyCode)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendKey(keyCode, true);

            InputManager inputManager = new InputManager();
            inputManager.AddKeyDown(keyCode);
            return inputManager.SendInputs();
        }

        public static bool SendKeyUp(VirtualKeyCode keyCode)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendKey(keyCode, false);

            InputManager inputManager = new InputManager();
            inputManager.AddKeyUp(keyCode);
            return inputManager.SendInputs();
        }

        public static bool SendKeyPress(VirtualKeyCode keyCode)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendKey(keyCode, true) && X11Input.SendKey(keyCode, false);

            InputManager inputManager = new InputManager();
            inputManager.AddKeyPress(keyCode);
            return inputManager.SendInputs();
        }

        public static bool SendKeyPressModifiers(VirtualKeyCode keyCode, params VirtualKeyCode[] modifiers)
        {
            if (!OperatingSystem.IsWindows()) return SendX11KeyPressModifiers(keyCode, modifiers);

            InputManager inputManager = new InputManager();
            inputManager.AddKeyPressModifiers(keyCode, modifiers);
            return inputManager.SendInputs();
        }

        public static bool SendKeyPressText(string text)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendText(text);

            InputManager inputManager = new InputManager();
            inputManager.AddKeyPressText(text);
            return inputManager.SendInputs();
        }

        public static bool SendMouseDown(InputMouseButton button = InputMouseButton.Left)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendMouseButton(button, true);

            InputManager inputManager = new InputManager();
            inputManager.AddMouseDown(button);
            return inputManager.SendInputs();
        }

        public static bool SendMouseUp(InputMouseButton button = InputMouseButton.Left)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendMouseButton(button, false);

            InputManager inputManager = new InputManager();
            inputManager.AddMouseUp(button);
            return inputManager.SendInputs();
        }

        public static bool SendMouseClick(InputMouseButton button = InputMouseButton.Left)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendMouseButton(button, true) && X11Input.SendMouseButton(button, false);

            InputManager inputManager = new InputManager();
            inputManager.AddMouseClick(button);
            return inputManager.SendInputs();
        }

        public static bool SendMouseClick(int x, int y, InputMouseButton button = InputMouseButton.Left)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendMouseMove(x, y) && SendMouseClick(button);

            InputManager inputManager = new InputManager();
            inputManager.AddMouseClick(x, y, button);
            return inputManager.SendInputs();
        }

        public static bool SendMouseClick(Point position, InputMouseButton button = InputMouseButton.Left)
        {
            return SendMouseClick(position.X, position.Y, button);
        }

        public static bool SendMouseMove(int x, int y)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendMouseMove(x, y);

            InputManager inputManager = new InputManager();
            inputManager.AddMouseMove(x, y);
            return inputManager.SendInputs();
        }

        public static bool SendMouseMove(Point position)
        {
            return SendMouseMove(position.X, position.Y);
        }

        private static bool SendX11KeyPressModifiers(VirtualKeyCode keyCode, VirtualKeyCode[] modifiers)
        {
            bool result = true;
            foreach (VirtualKeyCode modifier in modifiers) result &= X11Input.SendKey(modifier, true);
            result &= X11Input.SendKey(keyCode, true) && X11Input.SendKey(keyCode, false);
            for (int i = modifiers.Length - 1; i >= 0; i--) result &= X11Input.SendKey(modifiers[i], false);
            return result;
        }

        public static bool SendMouseWheel(int delta)
        {
            if (!OperatingSystem.IsWindows()) return X11Input.SendMouseWheel(delta);

            InputManager inputManager = new InputManager();
            inputManager.AddMouseWheel(delta);
            return inputManager.SendInputs();
        }
    }
}