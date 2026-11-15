using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DNSParty.Services
{
    public class HotkeyService : NativeWindow, IMessageFilter, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        // RegisterHotKey modifiers
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private const int HOTKEY_ID = 0x444E; // "DN"

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private bool _isRegistered;
        private bool _disposed;
        private bool _filterAdded;

        public string CurrentShortcut { get; private set; }
        public bool IsRegistered { get { return _isRegistered; } }
        public string LastRegistrationError { get; private set; }

        public event EventHandler HotkeyPressed;

        public HotkeyService()
        {
            CurrentShortcut = string.Empty;
            LastRegistrationError = string.Empty;

            try
            {
                // Create a message-only / hidden window handle so RegisterHotKey has a valid target hWnd
                // even when the main form is hidden to the system tray.
                CreateHandle(new CreateParams());
            }
            catch
            {
                // Fallback to thread-level hotkey + IMessageFilter if handle creation fails
                try
                {
                    Application.AddMessageFilter(this);
                    _filterAdded = true;
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Registers a global hotkey from a string shortcut such as "Shift+Tab" or "Ctrl+Alt+D".
        /// Unregisters any previously registered hotkey first.
        /// Never throws on registration conflict (e.g. if Steam or another app already registered Shift+Tab).
        /// </summary>
        public bool RegisterShortcut(string shortcutString)
        {
            string error;
            return RegisterShortcut(shortcutString, out error);
        }

        public bool RegisterShortcut(string shortcutString, out string errorMessage)
        {
            errorMessage = string.Empty;
            LastRegistrationError = string.Empty;

            if (_disposed)
            {
                errorMessage = "HotkeyService is disposed.";
                LastRegistrationError = errorMessage;
                return false;
            }

            Unregister();

            if (string.IsNullOrWhiteSpace(shortcutString) ||
                string.Equals(shortcutString.Trim(), "None", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortcutString.Trim(), "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                CurrentShortcut = string.Empty;
                return true;
            }

            uint modifiers;
            Keys key;
            if (!TryParseShortcut(shortcutString, out modifiers, out key, out errorMessage))
            {
                LastRegistrationError = errorMessage;
                return false;
            }

            try
            {
                IntPtr targetHwnd = this.Handle;

                // Try with MOD_NOREPEAT first (supported on Windows 7+), fallback without MOD_NOREPEAT if needed
                bool ok = RegisterHotKey(targetHwnd, HOTKEY_ID, modifiers | MOD_NOREPEAT, (uint)key);
                if (!ok)
                {
                    ok = RegisterHotKey(targetHwnd, HOTKEY_ID, modifiers, (uint)key);
                }

                if (ok)
                {
                    _isRegistered = true;
                    CurrentShortcut = FormatShortcut(modifiers, key);
                    return true;
                }
                else
                {
                    int win32Err = Marshal.GetLastWin32Error();
                    // 1409 = ERROR_HOTKEY_ALREADY_REGISTERED
                    if (win32Err == 1409)
                    {
                        errorMessage = string.Format("Shortcut '{0}' is already in use by another application.", shortcutString.Trim());
                    }
                    else
                    {
                        errorMessage = string.Format("Could not register shortcut '{0}' (Error {1}).", shortcutString.Trim(), win32Err);
                    }

                    LastRegistrationError = errorMessage;
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to register global shortcut: " + ex.Message;
                LastRegistrationError = errorMessage;
                return false;
            }
        }

        public void Unregister()
        {
            if (!_isRegistered)
            {
                return;
            }

            try
            {
                UnregisterHotKey(this.Handle, HOTKEY_ID);
            }
            catch
            {
                // Ignore unregister errors
            }
            finally
            {
                _isRegistered = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                OnHotkeyPressed();
                return;
            }

            base.WndProc(ref m);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                OnHotkeyPressed();
                return true;
            }

            return false;
        }

        private void OnHotkeyPressed()
        {
            var handler = HotkeyPressed;
            if (handler != null)
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch
                {
                    // Prevent unhandled listener exceptions from crashing message loop
                }
            }
        }

        public static bool TryParseShortcut(string shortcutString, out uint modifiers, out Keys key, out string error)
        {
            modifiers = MOD_NONE;
            key = Keys.None;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(shortcutString))
            {
                error = "Shortcut cannot be empty.";
                return false;
            }

            string[] tokens = shortcutString.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                error = "Shortcut cannot be empty.";
                return false;
            }

            foreach (string rawToken in tokens)
            {
                string token = rawToken.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_CONTROL;
                    continue;
                }

                if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_SHIFT;
                    continue;
                }

                if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_ALT;
                    continue;
                }

                if (string.Equals(token, "Win", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "Windows", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "LWin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "RWin", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_WIN;
                    continue;
                }

                Keys parsedKey;
                if (TryParseSingleKeyToken(token, out parsedKey))
                {
                    key = parsedKey;
                }
                else
                {
                    error = string.Format("Unrecognized key '{0}' in shortcut.", token);
                    return false;
                }
            }

            if (key == Keys.None)
            {
                error = "Shortcut must include a non-modifier key (e.g., Shift+Tab or Ctrl+Alt+D).";
                return false;
            }

            return true;
        }

        private static bool TryParseSingleKeyToken(string token, out Keys key)
        {
            key = Keys.None;

            // Common aliases
            if (string.Equals(token, "Esc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Escape", StringComparison.OrdinalIgnoreCase))
            {
                key = Keys.Escape;
                return true;
            }

            if (string.Equals(token, "Enter", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Return", StringComparison.OrdinalIgnoreCase))
            {
                key = Keys.Return;
                return true;
            }

            if (string.Equals(token, "Space", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Spacebar", StringComparison.OrdinalIgnoreCase))
            {
                key = Keys.Space;
                return true;
            }

            if (string.Equals(token, "`", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "~", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Tilde", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Backtick", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Oemtilde", StringComparison.OrdinalIgnoreCase))
            {
                key = Keys.Oemtilde;
                return true;
            }

            if (string.Equals(token, "Plus", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "=", StringComparison.OrdinalIgnoreCase))
            {
                key = Keys.Oemplus;
                return true;
            }

            if (string.Equals(token, "Minus", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "-", StringComparison.OrdinalIgnoreCase))
            {
                key = Keys.OemMinus;
                return true;
            }

            // Single digit 0-9
            if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
            {
                key = (Keys)((int)Keys.D0 + (token[0] - '0'));
                return true;
            }

            // Single letter A-Z
            if (token.Length == 1 && char.IsLetter(token[0]))
            {
                char upper = char.ToUpperInvariant(token[0]);
                key = (Keys)((int)Keys.A + (upper - 'A'));
                return true;
            }

            // Standard Keys enum parsing (e.g., Tab, F1..F12, Insert, Delete, Home, End, PageUp, PageDown)
            Keys enumParsed;
            if (Enum.TryParse(token, true, out enumParsed) && enumParsed != Keys.None)
            {
                key = enumParsed & Keys.KeyCode;
                return key != Keys.None;
            }

            return false;
        }

        public static string FormatShortcut(uint modifiers, Keys key)
        {
            var parts = new List<string>();
            if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & MOD_WIN) != 0) parts.Add("Win");

            if (key != Keys.None)
            {
                parts.Add(FormatKeyName(key));
            }

            return string.Join("+", parts);
        }

        public static string FormatKeyEventArgs(KeyEventArgs e)
        {
            if (e == null)
            {
                return string.Empty;
            }

            Keys keyCode = e.KeyCode;
            if (keyCode == Keys.ControlKey ||
                keyCode == Keys.ShiftKey ||
                keyCode == Keys.Menu ||
                keyCode == Keys.LWin ||
                keyCode == Keys.RWin)
            {
                return string.Empty;
            }

            uint mods = MOD_NONE;
            if (e.Control) mods |= MOD_CONTROL;
            if (e.Alt) mods |= MOD_ALT;
            if (e.Shift) mods |= MOD_SHIFT;

            return FormatShortcut(mods, keyCode);
        }

        private static string FormatKeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return ((int)(key - Keys.D0)).ToString();
            }

            if (key == Keys.Oemtilde) return "`";
            if (key == Keys.OemMinus) return "-";
            if (key == Keys.Oemplus) return "=";
            if (key == Keys.Return) return "Enter";
            if (key == Keys.Escape) return "Esc";
            if (key == Keys.Prior) return "PageUp";
            if (key == Keys.Next) return "PageDown";

            return key.ToString();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Unregister();

            if (_filterAdded)
            {
                try
                {
                    Application.RemoveMessageFilter(this);
                }
                catch
                {
                }
                _filterAdded = false;
            }

            try
            {
                if (this.Handle != IntPtr.Zero)
                {
                    DestroyHandle();
                }
            }
            catch
            {
            }
        }
    }
}
